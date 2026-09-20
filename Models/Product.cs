namespace ECommerceMVC.Models;

public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? CoverImageUrl { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
