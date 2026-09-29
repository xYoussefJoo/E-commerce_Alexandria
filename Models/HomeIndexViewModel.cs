namespace ECommerceMVC.Models;

public class HomeIndexViewModel
{
    public List<Category> FeaturedCategories { get; set; } = new();

    // CategoryId -> number of books in it and its subcategories, counted in SQL.
    public Dictionary<int, int> BookCounts { get; set; } = new();

    public List<Product> FeaturedBooks { get; set; } = new();
}
