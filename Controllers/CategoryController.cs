using ECommerceMVC.Data;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Controllers;

public class CategoryController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    private const int PageSize = 15;

    // GET: /Category
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _context.Categories
            .Include(c => c.ParentCategory)
            .OrderBy(c => c.Name);

        var categories = await PagedResult<Category>.CreateAsync(query, page, PageSize);

        return View(categories);
    }

    // GET: /Category/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();

        var category = await _context.Categories
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return NotFound();

        return View(category);
    }

    // GET: /Category/Create
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        ViewBag.ParentCategories = await GetParentCategoryOptions();
        return View();
    }

    // POST: /Category/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([Bind("Name,Description,ImageUrl,ParentCategoryId")] Category category)
    {
        if (ModelState.IsValid)
        {
            _context.Add(category);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Category '{category.Name}' created.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.ParentCategories = await GetParentCategoryOptions();
        return View(category);
    }

    // GET: /Category/Edit/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();

        var category = await _context.Categories.FindAsync(id);
        if (category is null) return NotFound();

        ViewBag.ParentCategories = await GetParentCategoryOptions(excludeId: category.CategoryId);
        return View(category);
    }

    // POST: /Category/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("CategoryId,Name,Description,ImageUrl,ParentCategoryId")] Category category)
    {
        if (id != category.CategoryId) return NotFound();

        if (category.ParentCategoryId == category.CategoryId)
        {
            ModelState.AddModelError(nameof(category.ParentCategoryId), "A category cannot be its own parent.");
        }
        else if (await WouldCreateLoopAsync(category.CategoryId, category.ParentCategoryId))
        {
            ModelState.AddModelError(nameof(category.ParentCategoryId),
                "That parent is inside this category already, so it would create a loop. Pick a different parent.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(category);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await CategoryExists(category.CategoryId)) return NotFound();
                throw;
            }

            TempData["Success"] = $"Category '{category.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.ParentCategories = await GetParentCategoryOptions(excludeId: category.CategoryId);
        return View(category);
    }

    // GET: /Category/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();

        var category = await _context.Categories
            .Include(c => c.ParentCategory)
            .Include(c => c.SubCategories)
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return NotFound();

        return View(category);
    }

    // POST: /Category/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _context.Categories
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return RedirectToAction(nameof(Index));

        if (category.SubCategories.Count > 0)
        {
            ModelState.AddModelError(string.Empty, "Cannot delete a category that still has subcategories. Reassign or delete them first.");
            TempData["Error"] = "Cannot delete this category while it still has subcategories.";
            var categoryWithDetails = await _context.Categories
                .Include(c => c.ParentCategory)
                .Include(c => c.SubCategories)
                .Include(c => c.Products)
                .FirstAsync(c => c.CategoryId == id);
            return View(categoryWithDetails);
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Category '{category.Name}' deleted.";
        return RedirectToAction(nameof(Index));
    }

    // Categories must stay a tree. Giving a category a new parent creates a loop only if that
    // parent is the category itself or one of its descendants, so walk UP from the new parent:
    // reaching the category means a loop, reaching the top (null) means it's safe.
    // O(depth) steps over an id -> parentId map loaded in one query.
    private async Task<bool> WouldCreateLoopAsync(int categoryId, int? newParentId)
    {
        if (newParentId is null) return false;

        var parentOf = await _context.Categories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.CategoryId, c => c.ParentCategoryId);

        var current = newParentId;
        // Safety limit: if existing data is already broken (a loop not involving this
        // category), stop instead of walking forever.
        for (var steps = 0; current is not null && steps <= parentOf.Count; steps++)
        {
            if (current == categoryId) return true;
            current = parentOf.GetValueOrDefault(current.Value);
        }

        return false;
    }

    private async Task<bool> CategoryExists(int id) =>
        await _context.Categories.AnyAsync(c => c.CategoryId == id);

    private async Task<List<Category>> GetParentCategoryOptions(int? excludeId = null) =>
        await _context.Categories
            .Where(c => excludeId == null || c.CategoryId != excludeId)
            .OrderBy(c => c.Name)
            .ToListAsync();
}
