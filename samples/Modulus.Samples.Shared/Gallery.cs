namespace Modulus.Samples.Shared;

/// <summary>Sample data for the component gallery page in both sample apps.</summary>
public sealed record GalleryInvoice(string Number, string Customer, decimal Amount, string Status);

public static class GalleryData
{
    public static readonly GalleryInvoice[] Invoices =
    [
        new("INV-1042", "Acme Corporation", 12_400, "Paid"),
        new("INV-1043", "Globex", 3_250, "Pending"),
        new("INV-1044", "Initech", 8_900, "Overdue"),
        new("INV-1045", "Umbrella Health", 1_120, "Paid"),
    ];

    public static readonly string[] Plans = ["Starter", "Business", "Enterprise"];
    public static readonly string[] Views = ["List", "Board", "Calendar"];
    public static readonly string[] Icons = ["home", "user", "team", "setting", "search", "bell", "mail", "calendar", "file-text", "cloud-upload", "delete", "edit"];
}

/// <summary>Form model behind the gallery's data-entry demos.</summary>
public sealed class GalleryForm
{
    public string? Name { get; set; }
    public string? Website { get; set; }
    public string? Password { get; set; }
    public string? Query { get; set; }
    public string? Notes { get; set; }
    public string Plan { get; set; } = "Business";
    public int Seats { get; set; } = 25;
    public DateTime? Start { get; set; } = new DateTime(2026, 10, 1);
    public DateTime?[] Period { get; set; } = [new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)];
    public DateTime? ReportAt { get; set; } = new DateTime(2026, 1, 1, 9, 0, 0);
    public bool Alerts { get; set; } = true;
    public bool Email { get; set; } = true;
    public bool Sms { get; set; }
    public string Priority { get; set; } = "normal";
    public string Size { get; set; } = "m";
    public string View { get; set; } = "List";
    public double Discount { get; set; } = 20;
    public decimal Rating { get; set; } = 4;
}
