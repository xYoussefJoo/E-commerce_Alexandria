using ECommerceMVC.Data;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Controllers;

[Authorize(Roles = "Customer")]
public class CartController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public CartController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: /Cart
    public async Task<IActionResult> Index()
    {
        var items = await GetCartItems();
        return View(items);
    }

    // GET: /Cart/Count - lightweight JSON endpoint so the nav badge can resync
    // itself after any htmx-boosted navigation, without needing a full re-render.
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Count()
    {
        if (User.Identity?.IsAuthenticated != true) return Json(new { count = 0 });

        var userId = _userManager.GetUserId(User)!;
        var count = await _context.CartItems
            .Where(ci => ci.UserId == userId)
            .SumAsync(ci => (int?)ci.Quantity) ?? 0;

        return Json(new { count });
    }

    // POST: /Cart/Add
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null)
    {
        bool IsAjax() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        var product = await _context.Products.FindAsync(productId);
        if (product is null)
        {
            if (IsAjax()) return Json(new { success = false, message = "That book could not be found." });
            return NotFound();
        }

        if (product.Stock <= 0)
        {
            if (IsAjax()) return Json(new { success = false, message = $"'{product.Name}' is out of stock." });
            TempData["Error"] = $"'{product.Name}' is out of stock.";
            return RedirectToAction(nameof(Index));
        }

        var userId = _userManager.GetUserId(User)!;
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ProductId == productId);

        if (cartItem is null)
        {
            cartItem = new CartItem { UserId = userId, ProductId = productId, Quantity = 0 };
            _context.CartItems.Add(cartItem);
        }

        cartItem.Quantity = Math.Clamp(cartItem.Quantity + quantity, 1, product.Stock);

        await _context.SaveChangesAsync();

        if (IsAjax())
        {
            var cartCount = await _context.CartItems
                .Where(ci => ci.UserId == userId)
                .SumAsync(ci => (int?)ci.Quantity) ?? 0;

            return Json(new { success = true, cartCount, productName = product.Name });
        }

        TempData["Success"] = $"'{product.Name}' added to your cart.";
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/UpdateQuantity
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
    {
        var userId = _userManager.GetUserId(User)!;
        var cartItem = await _context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.UserId == userId);

        if (cartItem is null) return NotFound();

        if (quantity <= 0)
        {
            _context.CartItems.Remove(cartItem);
        }
        else
        {
            cartItem.Quantity = Math.Clamp(quantity, 1, Math.Max(cartItem.Product.Stock, 1));
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /Cart/Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int cartItemId)
    {
        var userId = _userManager.GetUserId(User)!;
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId && ci.UserId == userId);

        if (cartItem is not null)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: /Cart/Checkout
    public async Task<IActionResult> Checkout()
    {
        var items = await GetCartItems();
        if (items.Count == 0)
        {
            TempData["Error"] = "Your cart is empty.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new CheckoutViewModel
        {
            Items = items,
            Total = items.Sum(ci => ci.Product.Price * ci.Quantity)
        };
        return View(vm);
    }

    // POST: /Cart/PlaceOrder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(CheckoutViewModel vm)
    {
        var userId = _userManager.GetUserId(User)!;
        var items = await GetCartItems();

        if (items.Count == 0)
        {
            TempData["Error"] = "Your cart is empty.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            vm.Items = items;
            vm.Total = items.Sum(ci => ci.Product.Price * ci.Quantity);
            return View("Checkout", vm);
        }

        // Fast-path check for a friendly error message before opening a transaction.
        // Not authoritative by itself - see the atomic decrement below, which is what
        // actually prevents two concurrent checkouts from overselling the same stock.
        foreach (var item in items)
        {
            if (item.Quantity > item.Product.Stock)
            {
                TempData["Error"] = $"'{item.Product.Name}' only has {item.Product.Stock} left in stock.";
                return RedirectToAction(nameof(Index));
            }
        }

        var order = new Order
        {
            UserId = userId,
            OrderDate = DateTime.UtcNow,
            ShippingAddress = vm.ShippingAddress,
            PaymentMethod = vm.PaymentMethod,
            Status = vm.PaymentMethod == PaymentMethod.CashOnDelivery ? OrderStatus.Pending : OrderStatus.Paid,
            TotalAmount = items.Sum(ci => ci.Product.Price * ci.Quantity)
        };

        foreach (var item in items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                UnitPrice = item.Product.Price,
                Quantity = item.Quantity
            });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // A conditional, single-statement UPDATE per product: it only decrements stock
        // if enough is still available, and the DB evaluates + applies it atomically.
        // This is what actually closes the race two simultaneous checkouts could hit
        // between the fast-path check above and the write - unlike loading Stock into
        // memory and writing it back, this can't lose a concurrent decrement.
        foreach (var item in items)
        {
            var rowsUpdated = await _context.Products
                .Where(p => p.ProductId == item.ProductId && p.Stock >= item.Quantity)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Stock, p => p.Stock - item.Quantity));

            if (rowsUpdated == 0)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"'{item.Product.Name}' no longer has enough stock. Please review your cart.";
                return RedirectToAction(nameof(Index));
            }
        }

        _context.Orders.Add(order);
        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = "Order placed! Thank you for shopping with Alexandria.";
        return RedirectToAction("Details", "Order", new { id = order.OrderId });
    }

    private async Task<List<CartItem>> GetCartItems()
    {
        var userId = _userManager.GetUserId(User)!;
        return await _context.CartItems
            .Include(ci => ci.Product)
            .Where(ci => ci.UserId == userId)
            .OrderBy(ci => ci.Product.Name)
            .ToListAsync();
    }
}
