namespace ECommerceMVC.Models;

public class DashboardViewModel
{
    public int TotalBooks { get; set; }
    public int TotalCategories { get; set; }
    public int TotalViews { get; set; }
    public List<Product> LowStockBooks { get; set; } = new();
    public List<Product> MostViewedBooks { get; set; } = new();
    public List<Product> RecentlyAddedBooks { get; set; } = new();

    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalCustomers { get; set; }
    public decimal AverageOrderValue { get; set; }

    public List<(DateTime Date, decimal Revenue)> RevenueByDay { get; set; } = new();
    public List<(OrderStatus Status, int Count)> OrdersByStatus { get; set; } = new();
    public List<(PaymentMethod Method, int Count)> OrdersByPaymentMethod { get; set; } = new();
    public List<(string Email, int OrderCount, decimal TotalSpent)> TopCustomers { get; set; } = new();
    public List<(string ProductName, int QuantitySold)> BestSellingBooks { get; set; } = new();
    public List<(int OrderId, string Email, DateTime OrderDate, OrderStatus Status, decimal TotalAmount)> RecentOrders { get; set; } = new();
}
