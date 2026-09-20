using System.ComponentModel.DataAnnotations;

namespace ECommerceMVC.Models;

public class CheckoutViewModel
{
    public List<CartItem> Items { get; set; } = new();
    public decimal Total { get; set; }

    [Required]
    [Display(Name = "Shipping address")]
    public string ShippingAddress { get; set; } = string.Empty;

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;
}
