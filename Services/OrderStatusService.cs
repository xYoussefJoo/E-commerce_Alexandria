using ECommerceMVC.Data;
using ECommerceMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Services;

public enum StatusChangeResult
{
    Changed,
    // The move isn't allowed by the order lifecycle (e.g. Delivered -> Pending).
    NotAllowed,
    // The order no longer has the status the caller saw (someone else changed it first,
    // or it doesn't exist). Nothing was changed.
    Conflict
}

// Every order status change goes through here, so the lifecycle rules and the side effects
// (putting stock back on cancel) live in one place instead of in each controller.
public class OrderStatusService
{
    private readonly ApplicationDbContext _context;

    public OrderStatusService(ApplicationDbContext context)
    {
        _context = context;
    }

    // `from` is the status the caller saw on screen. The update only happens if the order is
    // still in that status (optimistic concurrency), so a double click, or an admin and a
    // customer acting at the same time, can't apply a change twice.
    public async Task<StatusChangeResult> ChangeStatusAsync(int orderId, OrderStatus from, OrderStatus to)
    {
        if (!Order.CanMove(from, to)) return StatusChangeResult.NotAllowed;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var rowsUpdated = await _context.Orders
            .Where(o => o.OrderId == orderId && o.Status == from)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, to));

        if (rowsUpdated == 0)
        {
            await transaction.RollbackAsync();
            return StatusChangeResult.Conflict;
        }

        if (to == OrderStatus.Cancelled)
        {
            // Put the stock back. Same transaction as the status change, so it happens exactly
            // once. IgnoreQueryFilters: an archived book still gets its stock back.
            var items = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .Select(oi => new { oi.ProductId, oi.Quantity })
                .ToListAsync();

            foreach (var item in items)
            {
                await _context.Products
                    .IgnoreQueryFilters()
                    .Where(p => p.ProductId == item.ProductId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity));
            }
        }

        await transaction.CommitAsync();
        return StatusChangeResult.Changed;
    }
}
