namespace ECommerceMVC.Models;

// One row on the admin Orders screen: the order plus the customer's email.
public class AdminOrderRow
{
    public int OrderId { get; set; }
    public string? Email { get; set; }
    public DateTime OrderDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
}
