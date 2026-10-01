using System.ComponentModel.DataAnnotations;

namespace Modulus.Samples.Shared;

/// <summary>
/// Email shape check. Unlike [EmailAddress], an empty value is left to [Required], so users
/// see one message at a time.
/// </summary>
public static class EmailPattern
{
    public const string Value = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
}

public enum CustomerStatus
{
    Active,
    Trial,
    Churned,
}

public enum CustomerTier
{
    Starter,
    Growth,
    Enterprise,
}

public sealed class Customer
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Email { get; set; } = "";
    public string City { get; set; } = "";
    public CustomerTier Tier { get; set; }
    public CustomerStatus Status { get; set; }
    public decimal Revenue { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Create/edit payload with validation, shared by the Blazor form and MVC model binding.</summary>
public sealed class CustomerInput
{
    [Required(ErrorMessage = "Enter the contact's name")]
    [StringLength(80)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter a company")]
    [StringLength(80)]
    public string Company { get; set; } = "";

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = "";

    [StringLength(60)]
    public string City { get; set; } = "";

    public CustomerTier Tier { get; set; } = CustomerTier.Starter;

    public CustomerStatus Status { get; set; } = CustomerStatus.Trial;

    public static CustomerInput From(Customer c) => new()
    {
        Name = c.Name, Company = c.Company, Email = c.Email, City = c.City, Tier = c.Tier, Status = c.Status,
    };
}

/// <summary>Customer list filters (QueryFilter model / MVC query string).</summary>
public sealed class CustomerQuery
{
    public string? Keyword { get; set; }
    public CustomerStatus? Status { get; set; }
    public CustomerTier? Tier { get; set; }
    public string? City { get; set; }
}

public enum ProductStatus
{
    Published,
    Scheduled,
    Inactive,
}

public sealed class Product
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int Sold { get; set; }
    public ProductStatus Status { get; set; }
    public string Icon { get; set; } = "product";

    public bool LowStock => Stock is > 0 and < 20;
    public bool OutOfStock => Stock == 0;
    public int Tint => Array.IndexOf(ProductInput.Categories, Category) + 1;
}

/// <summary>Product list filters (MVC query string / toolbar selects).</summary>
public sealed record ProductQuery(string? Keyword = null, string? Category = null, string? Stock = null, ProductStatus? Status = null)
{
    /// <summary>Stock filter values: in stock, low (under 20) and out of stock.</summary>
    public static readonly (string Key, string Label)[] StockLevels = [("in", "In stock"), ("low", "Low stock"), ("out", "Out of stock")];
}

/// <summary>Add / edit product payload (drawer form).</summary>
public sealed class ProductInput
{
    public static readonly string[] Categories = ["Hardware", "Software", "Services", "Accessories"];

    [Required(ErrorMessage = "Enter a product name"), StringLength(80)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter a SKU"), RegularExpression(@"^[A-Z]{2}-\d{3,6}$", ErrorMessage = "Use the format MD-1234")]
    public string Sku { get; set; } = "";

    [Required(ErrorMessage = "Pick a category")]
    public string Category { get; set; } = "";

    [StringLength(400)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Enter a price"), Range(0.01, 1_000_000, ErrorMessage = "Enter a price above zero")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Enter the stock level"), Range(0, 100_000, ErrorMessage = "Enter 0 or more")]
    public int? Stock { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Published;

    public static ProductInput From(Product p) => new()
    {
        Name = p.Name, Sku = p.Sku, Category = p.Category, Description = p.Description, Price = p.Price, Stock = p.Stock, Status = p.Status,
    };
}

public sealed record ProductStats(int Total, int Published, int LowStock, int OutOfStock, decimal InventoryValue);

public sealed record CustomerStats(int Total, int Active, int Trial, int Churned, decimal Revenue);

public sealed record OrderStats(int PendingPayment, int Completed, int Refunded, int Failed);

public sealed record ActivityItem(string Who, string What, string Target, DateTime At, string Icon);

public sealed record RecentOrder(string Number, string Customer, decimal Amount, string Status, DateTime PlacedAt);

/// <summary>Launch moment the "coming soon" countdown runs to (stable for the day, in every flavour).</summary>
public static class LaunchDate
{
    public static DateTime Value => DateTime.UtcNow.Date.AddDays(24).AddHours(9);
}
