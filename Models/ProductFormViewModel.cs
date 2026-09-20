using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ECommerceMVC.Models;

public class ProductFormViewModel
{
    public int ProductId { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Author { get; set; }

    [MaxLength(500)]
    [Url(ErrorMessage = "Enter a real image link (https://...), not a pasted image.")]
    public string? CoverImageUrl { get; set; }

    [Display(Name = "Cover Image")]
    public IFormFile? CoverImageFile { get; set; }

    public decimal Price { get; set; }
    public int Stock { get; set; }

    public List<int> SelectedCategoryIds { get; set; } = new();
    public List<Category> AllCategories { get; set; } = new();
}
