using System.Globalization;
using ECommerceMVC.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// HTML <input type="number"> always submits period-decimal values (e.g. "19.99")
// regardless of the browser's locale. Without this, decimal model binding (and
// price formatting) follows the server machine's OS culture, which can use a
// comma decimal separator and reject every price/stock submission.
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);

    // Login/Register are standalone pages with no #main-content, so an htmx-boosted
    // request that hits an auth challenge can't be swapped in - it would try to
    // extract #main-content from a page that doesn't have one. HX-Redirect tells
    // htmx to do a full browser navigation instead of attempting a partial swap.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Headers["HX-Request"] == "true")
        {
            context.Response.Headers["HX-Redirect"] = context.RedirectUri;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Headers["HX-Request"] == "true")
        {
            context.Response.Headers["HX-Redirect"] = context.RedirectUri;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// MapStaticAssets() below only serves assets known at build time (its fingerprinted
// manifest). User-uploaded files (book cover images) are written to wwwroot at
// runtime, so classic UseStaticFiles() is needed to actually serve them.
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    const string adminRole = "Admin";
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(adminRole));
    }

    const string customerRole = "Customer";
    if (!await roleManager.RoleExistsAsync(customerRole))
    {
        await roleManager.CreateAsync(new IdentityRole(customerRole));
    }

    const string adminEmail = "admin@alexandria.local";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser is null)
    {
        adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(adminUser, "Alexandria#2026");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, adminRole);
        }
    }
}

app.Run();
