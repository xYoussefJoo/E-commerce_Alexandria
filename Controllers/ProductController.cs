using ECommerceMVC.Data;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Controllers;

public class ProductController : Controller
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB
    private const string UploadsRelativePath = "/uploads/products";

    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ProductController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private const int PageSize = 15;

    // GET: /Product
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _context.Products
            .Include(p => p.Categories)
            .OrderBy(p => p.Name);

        var products = await PagedResult<Product>.CreateAsync(query, page, PageSize);

        return View(products);
    }

    // GET: /Product/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id is null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product is null) return NotFound();

        product.ViewCount++;
        await _context.SaveChangesAsync();

        var categoryIds = product.Categories.Select(c => c.CategoryId).ToList();
        ViewBag.RelatedProducts = await _context.Products
            .Include(p => p.Categories)
            .Where(p => p.ProductId != id && p.Categories.Any(c => categoryIds.Contains(c.CategoryId)))
            .OrderBy(p => Guid.NewGuid())
            .Take(4)
            .ToListAsync();

        return View(product);
    }

    // GET: /Product/Create
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        var vm = new ProductFormViewModel
        {
            AllCategories = await GetLeafCategories()
        };
        return View(vm);
    }

    // POST: /Product/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ProductFormViewModel vm)
    {
        var uploadedImageUrl = await TryValidateAndSaveCoverImageAsync(vm.CoverImageFile);

        if (ModelState.IsValid)
        {
            var product = new Product
            {
                Name = vm.Name,
                Description = vm.Description,
                Author = vm.Author,
                CoverImageUrl = uploadedImageUrl ?? vm.CoverImageUrl,
                Price = vm.Price,
                Stock = vm.Stock,
                CreatedAt = DateTime.UtcNow
            };

            await AttachSelectedCategories(product, vm.SelectedCategoryIds);

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{product.Name}' added to the catalog.";
            return RedirectToAction(nameof(Index));
        }

        vm.AllCategories = await GetLeafCategories();
        return View(vm);
    }

    // GET: /Product/Edit/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product is null) return NotFound();

        var vm = new ProductFormViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Author = product.Author,
            CoverImageUrl = product.CoverImageUrl,
            Price = product.Price,
            Stock = product.Stock,
            SelectedCategoryIds = product.Categories.Select(c => c.CategoryId).ToList(),
            AllCategories = await GetLeafCategories()
        };

        return View(vm);
    }

    // POST: /Product/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel vm)
    {
        if (id != vm.ProductId) return NotFound();

        var uploadedImageUrl = await TryValidateAndSaveCoverImageAsync(vm.CoverImageFile);

        if (ModelState.IsValid)
        {
            var product = await _context.Products
                .Include(p => p.Categories)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product is null) return NotFound();

            var oldImageUrl = product.CoverImageUrl;

            product.Name = vm.Name;
            product.Description = vm.Description;
            product.Author = vm.Author;
            product.Price = vm.Price;
            product.Stock = vm.Stock;

            if (uploadedImageUrl is not null)
            {
                product.CoverImageUrl = uploadedImageUrl;
                DeleteUploadedImageIfLocal(oldImageUrl);
            }
            else
            {
                product.CoverImageUrl = vm.CoverImageUrl;
            }

            product.Categories.Clear();
            await AttachSelectedCategories(product, vm.SelectedCategoryIds);

            await _context.SaveChangesAsync();
            TempData["Success"] = $"'{product.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        vm.AllCategories = await GetLeafCategories();
        return View(vm);
    }

    // GET: /Product/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product is null) return NotFound();

        return View(product);
    }

    // POST: /Product/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is not null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            DeleteUploadedImageIfLocal(product.CoverImageUrl);
            TempData["Success"] = $"'{product.Name}' removed from the catalog.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task AttachSelectedCategories(Product product, List<int> selectedCategoryIds)
    {
        if (!selectedCategoryIds.Any()) return;

        var categories = await _context.Categories
            .Where(c => selectedCategoryIds.Contains(c.CategoryId))
            .ToListAsync();

        foreach (var category in categories)
        {
            product.Categories.Add(category);
        }
    }

    private async Task<List<Category>> GetLeafCategories() =>
        await _context.Categories
            .Where(c => !c.SubCategories.Any())
            .OrderBy(c => c.Name)
            .ToListAsync();

    // Validates an uploaded cover image and saves it to wwwroot/uploads/products.
    // Adds a ModelState error and returns null if the file fails validation; returns
    // null (with no error) if no file was uploaded at all, so the caller falls back
    // to the plain CoverImageUrl text field.
    private async Task<string?> TryValidateAndSaveCoverImageAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null;

        if (file.Length > MaxImageBytes)
        {
            ModelState.AddModelError(nameof(ProductFormViewModel.CoverImageFile), "Image must be 5 MB or smaller.");
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
        {
            ModelState.AddModelError(nameof(ProductFormViewModel.CoverImageFile), "Only JPG, PNG, GIF, or WEBP images are allowed.");
            return null;
        }

        if (!await HasValidImageSignatureAsync(file))
        {
            ModelState.AddModelError(nameof(ProductFormViewModel.CoverImageFile), "That file doesn't look like a valid image. Only real JPG, PNG, GIF, or WEBP files are allowed.");
            return null;
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"{UploadsRelativePath}/{fileName}";
    }

    // The extension alone is just a filename string an attacker controls - it doesn't
    // prove the bytes are actually an image. This checks the real file signature so a
    // renamed .exe/.html can't be uploaded into wwwroot and served back to visitors.
    private static async Task<bool> HasValidImageSignatureAsync(IFormFile file)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (bytesRead < 4) return false;

        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return true; // JPEG

        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) return true; // PNG

        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38) return true; // GIF87a/GIF89a

        if (bytesRead == 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // "RIFF"
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50) // "WEBP"
        {
            return true;
        }

        return false;
    }

    // Cleans up a previously-uploaded local file when it's replaced or the product is
    // deleted. Never touches external links (e.g. covers.openlibrary.org).
    private void DeleteUploadedImageIfLocal(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith(UploadsRelativePath)) return;

        var fileName = Path.GetFileName(imageUrl);
        var filePath = Path.Combine(_environment.WebRootPath, "uploads", "products", fileName);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}
