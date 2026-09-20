namespace ECommerceMVC.Models;

public class CartItem
{
    public int CartItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
}
