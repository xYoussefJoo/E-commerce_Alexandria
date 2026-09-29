namespace ECommerceMVC.Models;

public class Order
{
    public int OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string ShippingAddress { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    // The order lifecycle as a small state machine: which status can follow which.
    // Anything not listed (e.g. Delivered -> Pending, Shipped -> Cancelled) is not allowed.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedMoves = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
    };

    public static bool CanMove(OrderStatus from, OrderStatus to) =>
        AllowedMoves.TryGetValue(from, out var next) && next.Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) =>
        AllowedMoves.TryGetValue(from, out var next) ? next : [];

    // Customers may cancel only before the order is paid; after that it goes through the admin.
    public bool CanBeCancelledByCustomer => Status == OrderStatus.Pending;
}
