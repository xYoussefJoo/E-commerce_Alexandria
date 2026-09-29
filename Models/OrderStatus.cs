namespace ECommerceMVC.Models;

// Stored as an int, so existing values keep their numbers and new ones are added at the end.
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Cancelled = 2,
    Shipped = 3,
    Delivered = 4
}

public static class OrderStatusStyles
{
    // One place for the status colour used on the order pages and the admin screens.
    public static string TextClass(OrderStatus status) => status switch
    {
        OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Delivered => "text-green-600",
        OrderStatus.Cancelled => "text-red-600",
        _ => "text-gold-600"
    };
}
