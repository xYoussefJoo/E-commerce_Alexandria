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
        // Everything below is counted and picked in SQL. The old version loaded every product
        // (twice: once through the categories, once directly) just to show 8 books and a few
        // counts, which gets slower and heavier as the catalog grows.
        var featuredCategories = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var bookCounts = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .Select(c => new
            {
                c.CategoryId,
                Count = c.Products.Count() + c.SubCategories.Sum(s => s.Products.Count())
            })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        // One book per category (the first by name), plus one uncategorized book if any.
        var featuredIds = await _context.Categories
            .Where(c => c.Products.Any())
            .Select(c => c.Products.OrderBy(p => p.Name).Select(p => p.ProductId).First())
            .Distinct()
            .ToListAsync();

        var uncategorizedId = await _context.Products
            .Where(p => !p.Categories.Any())
            .OrderBy(p => p.Name)
            .Select(p => (int?)p.ProductId)
            .FirstOrDefaultAsync();

        if (uncategorizedId is int id) featuredIds.Add(id);

        var featuredBooks = await _context.Products
            .Include(p => p.Categories)
            .Where(p => featuredIds.Contains(p.ProductId))
            .OrderBy(p => p.Name)
            .Take(8)
            .ToListAsync();

        var model = new HomeIndexViewModel
        {
            FeaturedCategories = featuredCategories,
            BookCounts = bookCounts,
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
