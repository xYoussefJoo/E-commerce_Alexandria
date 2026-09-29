using ECommerceMVC.Data;
using ECommerceMVC.Models;
using ECommerceMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly OrderStatusService _orderStatus;

    public OrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager, OrderStatusService orderStatus)
    {
        _context = context;
        _userManager = userManager;
        _orderStatus = orderStatus;
    }

    private const int PageSize = 10;

    // GET: /Order
    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = _userManager.GetUserId(User)!;
        var query = _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate);

        var orders = await PagedResult<Order>.CreateAsync(query, page, PageSize);

        return View(orders);
    }

    // GET: /Order/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order is null) return NotFound();

        var userId = _userManager.GetUserId(User)!;
        var isOwner = order.UserId == userId;
        if (!isOwner && !User.IsInRole("Admin")) return Forbid();

        // Only the customer who placed the order sees the Cancel button.
        ViewBag.CanCancel = isOwner && order.CanBeCancelledByCustomer;
        return View(order);
    }

    // POST: /Order/Cancel/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order is null) return NotFound();
        if (order.UserId != userId) return Forbid();

        if (!order.CanBeCancelledByCustomer)
        {
            TempData["Error"] = "This order can't be cancelled any more.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var result = await _orderStatus.ChangeStatusAsync(id, order.Status, OrderStatus.Cancelled);

        if (result == StatusChangeResult.Changed)
            TempData["Success"] = $"Order #{id} was cancelled.";
        else
            TempData["Error"] = "This order changed in the meantime and couldn't be cancelled. Please check it again.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
