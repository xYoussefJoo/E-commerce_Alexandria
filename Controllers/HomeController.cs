using System.Diagnostics;
using ECommerceMVC.Data;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var featuredCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .Include(c => c.Products)
            .Include(c => c.SubCategories).ThenInclude(s => s.Products)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var allProducts = await _context.Products
            .Include(p => p.Categories)
            .ToListAsync();

        var featuredBooks = allProducts
            .GroupBy(p => p.Categories.FirstOrDefault()?.Name ?? "Other")
            .Select(g => g.OrderBy(p => p.Name).First())
            .OrderBy(p => p.Name)
            .Take(8)
            .ToList();

        var model = new HomeIndexViewModel
        {
            FeaturedCategories = featuredCategories,
            FeaturedBooks = featuredBooks
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
