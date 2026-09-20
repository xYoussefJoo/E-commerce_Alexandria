using ECommerceMVC.Data;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel
        {
            TotalBooks = await _context.Products.CountAsync(),
            TotalCategories = await _context.Categories.CountAsync(),
            TotalViews = await _context.Products.SumAsync(p => p.ViewCount),
            LowStockBooks = await _context.Products
                .Where(p => p.Stock < 10)
                .OrderBy(p => p.Stock)
                .ToListAsync(),
            MostViewedBooks = await _context.Products
                .OrderByDescending(p => p.ViewCount)
                .Take(5)
                .ToListAsync(),
            RecentlyAddedBooks = await _context.Products
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToListAsync()
        };

        var revenueOrders = _context.Orders.Where(o => o.Status != OrderStatus.Cancelled);

        vm.TotalOrders = await _context.Orders.CountAsync();
        vm.TotalRevenue = await revenueOrders.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        vm.AverageOrderValue = vm.TotalOrders > 0 ? vm.TotalRevenue / vm.TotalOrders : 0m;
        vm.TotalCustomers = await _context.Orders.Select(o => o.UserId).Distinct().CountAsync();

        vm.OrdersByStatus = (await _context.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync())
            .Select(g => (g.Status, g.Count))
            .ToList();

        vm.OrdersByPaymentMethod = (await _context.Orders
            .GroupBy(o => o.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count() })
            .ToListAsync())
            .Select(g => (g.Method, g.Count))
            .ToList();

        var since = DateTime.UtcNow.Date.AddDays(-13);
        var rawRevenueByDay = await revenueOrders
            .Where(o => o.OrderDate >= since)
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync();

        vm.RevenueByDay = Enumerable.Range(0, 14)
            .Select(offset => since.AddDays(offset))
            .Select(day => (day, rawRevenueByDay.FirstOrDefault(r => r.Date == day)?.Revenue ?? 0m))
            .ToList();

        vm.TopCustomers = (await (
            from o in _context.Orders
            join u in _context.Users on o.UserId equals u.Id
            group o by new { u.Id, u.Email } into g
            orderby g.Sum(x => x.TotalAmount) descending
            select new { g.Key.Email, OrderCount = g.Count(), TotalSpent = g.Sum(x => x.TotalAmount) })
            .Take(5)
            .ToListAsync())
            .Select(c => (c.Email ?? "(unknown)", c.OrderCount, c.TotalSpent))
            .ToList();

        vm.BestSellingBooks = (await _context.OrderItems
            .GroupBy(oi => oi.ProductName)
            .Select(g => new { ProductName = g.Key, QuantitySold = g.Sum(oi => oi.Quantity) })
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToListAsync())
            .Select(b => (b.ProductName, b.QuantitySold))
            .ToList();

        vm.RecentOrders = (await (
            from o in _context.Orders
            join u in _context.Users on o.UserId equals u.Id
            orderby o.OrderDate descending
            select new { o.OrderId, u.Email, o.OrderDate, o.Status, o.TotalAmount })
            .Take(8)
            .ToListAsync())
            .Select(o => (o.OrderId, o.Email ?? "(unknown)", o.OrderDate, o.Status, o.TotalAmount))
            .ToList();

        return View(vm);
    }
}
