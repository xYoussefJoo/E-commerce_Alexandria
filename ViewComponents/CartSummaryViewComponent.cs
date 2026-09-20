using ECommerceMVC.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMVC.ViewComponents;

public class CartSummaryViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public CartSummaryViewComponent(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
        {
            return View(0);
        }

        var userId = _userManager.GetUserId(UserClaimsPrincipal)!;
        var itemCount = await _context.CartItems
            .Where(ci => ci.UserId == userId)
            .SumAsync(ci => (int?)ci.Quantity) ?? 0;

        return View(itemCount);
    }
}
