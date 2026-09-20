using ECommerceMVC.Data;
using ECommerceMVC.Models;
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

    public OrderController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
        if (order.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        return View(order);
    }
}
