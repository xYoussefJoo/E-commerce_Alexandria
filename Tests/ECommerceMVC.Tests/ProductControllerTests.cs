using ECommerceMVC.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace ECommerceMVC.Tests;

public class ProductControllerTests : IDisposable
{
    private readonly string _webRootPath = Path.Combine(Path.GetTempPath(), "ecommercemvc-tests-" + Guid.NewGuid().ToString("N"));

    public ProductControllerTests() => Directory.CreateDirectory(_webRootPath);

    public void Dispose()
    {
        if (Directory.Exists(_webRootPath)) Directory.Delete(_webRootPath, recursive: true);
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, stream.Length, "CoverImageFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    // Regression test: the extension alone used to be enough to pass upload validation, letting
    // a renamed non-image file land in wwwroot/uploads/products and be served back to visitors.
    [Fact]
    public async Task Create_RejectsFileWhoseContentDoesNotMatchItsImageExtension()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var environment = new FakeWebHostEnvironment(_webRootPath);
        var controller = TestSupport.CreateProductController(context, environment);

        var fakeImage = CreateFormFile("<script>alert(1)</script>"u8.ToArray(), "cover.jpg");
        var vm = new ProductFormViewModel { Name = "Suspicious Upload", Price = 10m, Stock = 1, CoverImageFile = fakeImage };

        var result = await controller.Create(vm);

        Assert.IsType<ViewResult>(result);
        Assert.False(await context.Products.AnyAsync());
        Assert.False(controller.ModelState.IsValid);
        // The signature check happens before the uploads folder is even created, so a rejected
        // file never gets close to landing on disk.
        Assert.False(Directory.Exists(Path.Combine(_webRootPath, "uploads", "products")));
    }

    [Fact]
    public async Task Create_AcceptsGenuineJpegUpload()
    {
        using var db = new TestDatabase();
        await using var context = db.CreateContext();
        var environment = new FakeWebHostEnvironment(_webRootPath);
        var controller = TestSupport.CreateProductController(context, environment);

        byte[] jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
        var realImage = CreateFormFile(jpegHeader, "cover.jpg");
        var vm = new ProductFormViewModel { Name = "Real Book", Price = 12.5m, Stock = 4, CoverImageFile = realImage };

        var result = await controller.Create(vm);

        Assert.IsType<RedirectToActionResult>(result);
        var product = await context.Products.SingleAsync();
        Assert.Equal("Real Book", product.Name);
        Assert.StartsWith("/uploads/products/", product.CoverImageUrl);
        Assert.Single(Directory.GetFiles(Path.Combine(_webRootPath, "uploads", "products")));
    }

    [Fact]
    public async Task Index_ReturnsRequestedPageAtConfiguredSize()
    {
        using var db = new TestDatabase();
        await using (var seedContext = db.CreateContext())
        {
            for (var i = 1; i <= 30; i++)
            {
                seedContext.Products.Add(new Product { Name = $"Book {i:D2}", Price = 1m, Stock = 1 });
            }
            await seedContext.SaveChangesAsync();
        }

        await using var context = db.CreateContext();
        var controller = TestSupport.CreateProductController(context, new FakeWebHostEnvironment(_webRootPath));

        var result = await controller.Index(page: 2);

        var view = Assert.IsType<ViewResult>(result);
        var page = Assert.IsType<PagedResult<Product>>(view.Model);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(15, page.Items.Count); // PageSize=15, and page 2 of 30 items is a full page
        Assert.Equal(30, page.TotalCount);
    }

    private sealed class FakeWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "ECommerceMVC.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Development";
    }
}
