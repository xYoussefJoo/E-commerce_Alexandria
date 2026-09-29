using ECommerceMVC.Data;
using ECommerceMVC.Models;
using ECommerceMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceMVC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class OrdersController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _context;
    private readonly OrderStatusService _orderStatus;

    public OrdersController(ApplicationDbContext context, OrderStatusService orderStatus)
    {
        _context = context;
        _orderStatus = orderStatus;
    }

    // GET: /Admin/Orders
    public async Task<IActionResult> Index(int page = 1)
    {
        var query =
            from o in _context.Orders
            join u in _context.Users on o.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby o.OrderDate descending
            select new AdminOrderRow
            {
                OrderId = o.OrderId,
                Email = u.Email,
                OrderDate = o.OrderDate,
                PaymentMethod = o.PaymentMethod,
                Status = o.Status,
                TotalAmount = o.TotalAmount
            };

        return View(await PagedResult<AdminOrderRow>.CreateAsync(query, page, PageSize));
    }

    // POST: /Admin/Orders/UpdateStatus
    // `from` is the status the admin saw on the page, so a stale page can't overwrite a
    // change someone else made in the meantime (see OrderStatusService).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus from, OrderStatus to, int page = 1)
    {
        var result = await _orderStatus.ChangeStatusAsync(id, from, to);

        switch (result)
        {
            case StatusChangeResult.Changed:
                TempData["Success"] = $"Order #{id} is now {to}.";
                break;
            case StatusChangeResult.NotAllowed:
                TempData["Error"] = $"An order can't go from {from} to {to}.";
                break;
            default:
                TempData["Error"] = $"Order #{id} was changed by someone else. The list has been refreshed.";
                break;
        }

        return RedirectToAction(nameof(Index), new { page });
    }
}
