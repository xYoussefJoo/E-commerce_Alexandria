using ECommerceMVC.Areas.Admin.Controllers;
using ECommerceMVC.Controllers;
using ECommerceMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.Tests;

// Category loops, the home page queries and the dashboard average.
public class CatalogFixesTests
{
    // Tree: Fiction (1) -> Fantasy (2) -> Epic (3)
    private static async Task SeedCategoryChainAsync(TestDatabase db)
    {
        await using var context = db.CreateContext();
        var fiction = new Category { Name = "Fiction" };
        var fantasy = new Category { Name = "Fantasy", ParentCategory = fiction };
        var epic = new Category { Name = "Epic", ParentCategory = fantasy };
        context.Categories.AddRange(fiction, fantasy, epic);
        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData("Fiction", "Fantasy")] // Fiction under its own child: 2-step loop
    [InlineData("Fiction", "Epic")]    // Fiction under its grandchild: 3-step loop
    public async Task Edit_ParentThatWouldCreateALoop_IsRejected(string categoryName, string newParentName)
    {
        using var db = new TestDatabase();
        await SeedCategoryChainAsync(db);

        await using var context = db.CreateContext();
        var category = await context.Categories.AsNoTracking().SingleAsync(c => c.Name == categoryName);
        var newParent = await context.Categories.AsNoTracking().SingleAsync(c => c.Name == newParentName);
        var controller = TestSupport.Configured(new CategoryController(context));

        category.ParentCategoryId = newParent.CategoryId;
        var result = await controller.Edit(category.CategoryId, category);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        await using var verify = db.CreateContext();
        Assert.Null((await verify.Categories.SingleAsync(c => c.Name == categoryName)).ParentCategoryId);
    }

    [Fact]
    public async Task Edit_ParentThatKeepsATree_IsSaved()
    {
        using var db = new TestDatabase();
        await SeedCategoryChainAsync(db);

        await using var context = db.CreateContext();
        var epic = await context.Categories.AsNoTracking().SingleAsync(c => c.Name == "Epic");
        var fiction = await context.Categories.AsNoTracking().SingleAsync(c => c.Name == "Fiction");
        var controller = TestSupport.Configured(new CategoryController(context));

        epic.ParentCategoryId = fiction.CategoryId; // move Epic up one level: still a tree
        var result = await controller.Edit(epic.CategoryId, epic);

        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task Home_PicksOneBookPerCategoryAndCountsInSql()
    {
        using var db = new TestDatabase();
        await using (var seed = db.CreateContext())
        {
            var fiction = new Category { Name = "Fiction" };
            var fantasy = new Category { Name = "Fantasy", ParentCategory = fiction };
            var history = new Category { Name = "History" };
            seed.Categories.AddRange(fiction, fantasy, history);
            seed.Products.AddRange(
                new Product { Name = "B Fiction", Price = 1m, Categories = { fiction } },
                new Product { Name = "A Fiction", Price = 1m, Categories = { fiction } },
                new Product { Name = "Z Fantasy", Price = 1m, Categories = { fantasy } },
                new Product { Name = "M History", Price = 1m, Categories = { history } },
                new Product { Name = "No Category", Price = 1m },
                new Product { Name = "AA Archived", Price = 1m, IsArchived = true, Categories = { history } });
            await seed.SaveChangesAsync();
        }

        await using var context = db.CreateContext();
        var result = await TestSupport.Configured(new HomeController(context)).Index();

        var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(["A Fiction", "M History", "No Category", "Z Fantasy"], model.FeaturedBooks.Select(b => b.Name));

        var fictionId = model.FeaturedCategories.Single(c => c.Name == "Fiction").CategoryId;
        var historyId = model.FeaturedCategories.Single(c => c.Name == "History").CategoryId;
        Assert.Equal(3, model.BookCounts[fictionId]); // 2 direct + 1 in Fantasy
        Assert.Equal(1, model.BookCounts[historyId]); // archived book not counted
    }

    [Fact]
    public async Task Dashboard_AverageOrderValue_IgnoresCancelledOrders()
    {
        using var db = new TestDatabase();
        await using (var seed = db.CreateContext())
        {
            seed.Users.Add(new IdentityUser { Id = "user-1", UserName = "user-1@test.local", Email = "user-1@test.local" });
            seed.Orders.AddRange(
                new Order { UserId = "user-1", OrderDate = DateTime.UtcNow, TotalAmount = 100m, Status = OrderStatus.Pending },
                new Order { UserId = "user-1", OrderDate = DateTime.UtcNow, TotalAmount = 100m, Status = OrderStatus.Cancelled });
            await seed.SaveChangesAsync();
        }

        await using var context = db.CreateContext();
        var result = await TestSupport.Configured(new DashboardController(context)).Index();

        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(100m, model.TotalRevenue);
        Assert.Equal(100m, model.AverageOrderValue); // was 50: 100 revenue / 2 orders
    }
}
