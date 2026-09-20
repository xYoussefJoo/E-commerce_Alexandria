namespace ECommerceMVC.Models;

public class HomeIndexViewModel
{
    public List<Category> FeaturedCategories { get; set; } = new();
    public List<Product> FeaturedBooks { get; set; } = new();
}
