using System.Security.Claims;
using ECommerceMVC.Controllers;
using ECommerceMVC.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerceMVC.Tests;

internal static class TestSupport
{
    // A real UserManager backed by a real UserStore, built by hand instead of through the app's
    // DI container. CartController only calls GetUserId(ClaimsPrincipal), which reads a claim
    // and never touches the store, so which context backs this doesn't matter for these tests.
    public static UserManager<IdentityUser> CreateUserManager(ApplicationDbContext context) => new(
        new UserStore<IdentityUser, IdentityRole, ApplicationDbContext, string>(context),
        Options.Create(new IdentityOptions()),
        new PasswordHasher<IdentityUser>(),
        Array.Empty<IUserValidator<IdentityUser>>(),
        Array.Empty<IPasswordValidator<IdentityUser>>(),
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        new ServiceCollection().BuildServiceProvider(),
        NullLogger<UserManager<IdentityUser>>.Instance);

    public static CartController CreateCartController(ApplicationDbContext context, UserManager<IdentityUser> userManager, string userId)
    {
        var controller = new CartController(context, userManager);
        Configure(controller, userId);
        return controller;
    }

    public static ProductController CreateProductController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        var controller = new ProductController(context, environment);
        Configure(controller, userId: null);
        return controller;
    }

    private static void Configure(Controller controller, string? userId)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
