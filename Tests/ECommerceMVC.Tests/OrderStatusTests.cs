using ECommerceMVC.Models;
using ECommerceMVC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Tests;

public class OrderStatusTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    public void CanMove_FollowsTheOrderLifecycle(OrderStatus from, OrderStatus to, bool expected)
    {
        Assert.Equal(expected, Order.CanMove(from, to));
    }

    [Fact]
    public async Task Cancel_PutsTheStockBack()
    {
        using var db = new TestDatabase();
        var (orderId, productId) = await SeedOrderAsync(db, stock: 3, quantityOrdered: 2);

        await using var context = db.CreateContext();
        var result = await new OrderStatusService(context).ChangeStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Cancelled);

        Assert.Equal(StatusChangeResult.Changed, result);
        await using var verify = db.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, (await verify.Orders.SingleAsync()).Status);
        Assert.Equal(5, (await verify.Products.SingleAsync(p => p.ProductId == productId)).Stock);
    }

    // A double click (or admin + customer at once) must not return the stock twice.
    [Fact]
    public async Task CancelTwice_PutsTheStockBackOnlyOnce()
    {
        using var db = new TestDatabase();
        var (orderId, productId) = await SeedOrderAsync(db, stock: 3, quantityOrdered: 2);

        await using (var first = db.CreateContext())
        {
            Assert.Equal(StatusChangeResult.Changed,
                await new OrderStatusService(first).ChangeStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Cancelled));
        }

        await using (var second = db.CreateContext())
        {
            // The second click still thinks the order is Pending, but it isn't any more.
            Assert.Equal(StatusChangeResult.Conflict,
                await new OrderStatusService(second).ChangeStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Cancelled));
        }

        await using var verify = db.CreateContext();
        Assert.Equal(5, (await verify.Products.SingleAsync(p => p.ProductId == productId)).Stock);
    }

    [Fact]
    public async Task ForbiddenMove_ChangesNothing()
    {
        using var db = new TestDatabase();
        var (orderId, _) = await SeedOrderAsync(db, stock: 3, quantityOrdered: 1);

        await using var context = db.CreateContext();
        var result = await new OrderStatusService(context).ChangeStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Delivered);

        Assert.Equal(StatusChangeResult.NotAllowed, result);
        await using var verify = db.CreateContext();
        Assert.Equal(OrderStatus.Pending, (await verify.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task CustomerCannotCancelSomeoneElsesOrder()
    {
        using var db = new TestDatabase();
        var (orderId, _) = await SeedOrderAsync(db, stock: 3, quantityOrdered: 1);

        await using var context = db.CreateContext();
        var controller = TestSupport.CreateOrderController(context, "someone-else");

        Assert.IsType<ForbidResult>(await controller.Cancel(orderId));
        Assert.Equal(OrderStatus.Pending, (await context.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task CustomerCannotCancelAfterPayment()
    {
        using var db = new TestDatabase();
        var (orderId, _) = await SeedOrderAsync(db, stock: 3, quantityOrdered: 1, status: OrderStatus.Paid);

        await using var context = db.CreateContext();
        var controller = TestSupport.CreateOrderController(context, "user-1");
        await controller.Cancel(orderId);

        Assert.Equal(OrderStatus.Paid, (await context.Orders.SingleAsync()).Status);
        Assert.True(controller.TempData.ContainsKey("Error"));
    }

    [Fact]
    public async Task CustomerCanCancelOwnPendingOrder()
    {
        using var db = new TestDatabase();
        var (orderId, _) = await SeedOrderAsync(db, stock: 3, quantityOrdered: 1);

        await using var context = db.CreateContext();
        var controller = TestSupport.CreateOrderController(context, "user-1");
        await controller.Cancel(orderId);

        await using var verify = db.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, (await verify.Orders.SingleAsync()).Status);
    }

    private static async Task<(int OrderId, int ProductId)> SeedOrderAsync(
        TestDatabase db, int stock, int quantityOrdered, OrderStatus status = OrderStatus.Pending)
    {
        await using var context = db.CreateContext();
        var product = new Product { Name = "Book", Price = 10m, Stock = stock };
        context.Users.Add(new IdentityUser { Id = "user-1", UserName = "user-1@test.local" });
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var order = new Order { UserId = "user-1", ShippingAddress = "1 Library Ave", Status = status, TotalAmount = 10m * quantityOrdered };
        order.Items.Add(new OrderItem { ProductId = product.ProductId, ProductName = product.Name, UnitPrice = 10m, Quantity = quantityOrdered });
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        return (order.OrderId, product.ProductId);
    }
}
