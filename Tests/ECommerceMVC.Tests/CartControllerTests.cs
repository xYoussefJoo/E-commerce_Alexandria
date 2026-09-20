using ECommerceMVC.Controllers;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Tests;

public class CartControllerTests
{
    private static async Task<Product> SeedProductAndUsersAsync(ApplicationDbContextSeed seed)
    {
        await using var context = seed.Database.CreateContext();
        var product = new Product { Name = seed.ProductName, Price = 20m, Stock = seed.Stock };
        context.Products.Add(product);
        foreach (var userId in seed.UserIds)
        {
            context.Users.Add(new IdentityUser { Id = userId, UserName = $"{userId}@test.local" });
        }
        await context.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task Add_ClampsQuantityToAvailableStock()
    {
        using var db = new TestDatabase();
        var product = await SeedProductAndUsersAsync(new ApplicationDbContextSeed(db, "Clamped Book", 3, "user-1"));

        await using var context = db.CreateContext();
        var userManager = TestSupport.CreateUserManager(context);
        var controller = TestSupport.CreateCartController(context, userManager, "user-1");

        await controller.Add(product.ProductId, quantity: 10);

        var cartItem = await context.CartItems.SingleAsync(ci => ci.ProductId == product.ProductId);
        Assert.Equal(3, cartItem.Quantity);
    }

    [Fact]
    public async Task Add_OutOfStock_DoesNotCreateCartItem()
    {
        using var db = new TestDatabase();
        var product = await SeedProductAndUsersAsync(new ApplicationDbContextSeed(db, "Sold Out Book", 0, "user-1"));

        await using var context = db.CreateContext();
        var userManager = TestSupport.CreateUserManager(context);
        var controller = TestSupport.CreateCartController(context, userManager, "user-1");

        var result = await controller.Add(product.ProductId, quantity: 1);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(await context.CartItems.AnyAsync());
    }

    [Fact]
    public async Task PlaceOrder_DecrementsStockAndClearsCart()
    {
        using var db = new TestDatabase();
        var product = await SeedProductAndUsersAsync(new ApplicationDbContextSeed(db, "Checkout Book", 5, "user-1"));

        await using (var seedContext = db.CreateContext())
        {
            seedContext.CartItems.Add(new CartItem { UserId = "user-1", ProductId = product.ProductId, Quantity = 2 });
            await seedContext.SaveChangesAsync();
        }

        await using var context = db.CreateContext();
        var userManager = TestSupport.CreateUserManager(context);
        var controller = TestSupport.CreateCartController(context, userManager, "user-1");

        var vm = new CheckoutViewModel { ShippingAddress = "1 Library Ave", PaymentMethod = PaymentMethod.CashOnDelivery };
        var result = await controller.PlaceOrder(vm);

        Assert.IsType<RedirectToActionResult>(result);

        await using var verify = db.CreateContext();
        var refreshedProduct = await verify.Products.SingleAsync(p => p.ProductId == product.ProductId);
        Assert.Equal(3, refreshedProduct.Stock);
        Assert.False(await verify.CartItems.AnyAsync());
        Assert.Equal(1, await verify.Orders.CountAsync());
    }

    // Regression test for the checkout stock race: two shoppers both have the last copy in
    // their cart. The first checkout must succeed and exhaust stock; the second must fail
    // cleanly (its cart item kept, no order created, stock never goes negative) instead of
    // silently overselling. See CartController.PlaceOrder's atomic ExecuteUpdateAsync guard.
    [Fact]
    public async Task PlaceOrder_SecondCheckoutForLastCopy_FailsWithoutOverselling()
    {
        using var db = new TestDatabase();
        var product = await SeedProductAndUsersAsync(new ApplicationDbContextSeed(db, "Last Copy", 1, "user-a", "user-b"));

        await using (var seedContext = db.CreateContext())
        {
            seedContext.CartItems.Add(new CartItem { UserId = "user-a", ProductId = product.ProductId, Quantity = 1 });
            seedContext.CartItems.Add(new CartItem { UserId = "user-b", ProductId = product.ProductId, Quantity = 1 });
            await seedContext.SaveChangesAsync();
        }

        var vm = new CheckoutViewModel { ShippingAddress = "1 Library Ave", PaymentMethod = PaymentMethod.CashOnDelivery };

        await using (var contextA = db.CreateContext())
        {
            var userManagerA = TestSupport.CreateUserManager(contextA);
            var controllerA = TestSupport.CreateCartController(contextA, userManagerA, "user-a");
            var resultA = await controllerA.PlaceOrder(vm);
            Assert.IsType<RedirectToActionResult>(resultA);
        }

        await using (var contextB = db.CreateContext())
        {
            var userManagerB = TestSupport.CreateUserManager(contextB);
            var controllerB = TestSupport.CreateCartController(contextB, userManagerB, "user-b");
            var resultB = await controllerB.PlaceOrder(vm);

            var redirect = Assert.IsType<RedirectToActionResult>(resultB);
            Assert.Equal(nameof(CartController.Index), redirect.ActionName);
            Assert.True(controllerB.TempData.ContainsKey("Error"));
        }

        await using var verify = db.CreateContext();
        var finalStock = (await verify.Products.SingleAsync()).Stock;
        Assert.Equal(0, finalStock);
        Assert.Equal(1, await verify.Orders.CountAsync());
        Assert.True(await verify.CartItems.AnyAsync(ci => ci.UserId == "user-b"));
    }

    private sealed record ApplicationDbContextSeed(TestDatabase Database, string ProductName, int Stock, params string[] UserIds);
}
