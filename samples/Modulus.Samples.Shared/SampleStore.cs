using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Samples.Shared;

/// <summary>
/// Thread-safe in-memory data for the sample apps. Deterministic seed so screenshots and tests
/// are stable. Register as a singleton; swap for EF Core / an API client in a real app.
/// </summary>
public sealed partial class SampleStore
{
    internal static readonly string[] FirstNames = ["Olivia", "Liam", "Amelia", "Noah", "Sofia", "Mateo", "Aisha", "Ethan", "Yuki", "Lucas", "Priya", "Omar", "Chloe", "Hiro", "Elena", "Kwame", "Maya", "Diego", "Nora", "Arjun", "Ingrid", "Samuel", "Leila", "Tomás"];
    internal static readonly string[] LastNames = ["Bennett", "Okafor", "Lindqvist", "Tanaka", "Reyes", "Hartmann", "Khan", "Dubois", "Novak", "Silva", "Moreau", "Park", "Andersson", "Rossi", "Mensah", "Costa"];
    internal static readonly string[] Companies = ["Northwind", "Contoso", "Fabrikam", "Tailspin", "Adventure Works", "Litware", "Proseware", "Wingtip", "Lamna", "Alpine Ski House", "Coho Vineyard", "Fourth Coffee", "Margie's Travel", "Trey Research", "Woodgrove Bank", "Blue Yonder"];
    internal static readonly string[] Cities = ["London", "Berlin", "Stockholm", "Tokyo", "Toronto", "Lagos", "Singapore", "Dubai", "São Paulo", "Sydney", "Dhaka", "Austin"];

    private readonly Lock _gate = new();
    private readonly List<Customer> _customers;
    private readonly List<Product> _products;
    private int _nextCustomerId;
    private int _nextProductId;

    public SampleStore()
    {
        var rng = new Random(42);
        var now = DateTime.Today;
        _customers = Enumerable.Range(1, 86).Select(id =>
        {
            var first = FirstNames[rng.Next(FirstNames.Length)];
            var last = LastNames[rng.Next(LastNames.Length)];
            var company = Companies[rng.Next(Companies.Length)];
            var tier = (CustomerTier)(rng.Next(10) switch { < 5 => 0, < 8 => 1, _ => 2 });
            return new Customer
            {
                Id = id,
                Name = $"{first} {last}",
                Company = company,
                Email = $"{first.ToLowerInvariant().Replace("á", "a")}.{last.ToLowerInvariant()}@{company.ToLowerInvariant().Replace(" ", "").Replace("'", "")}.com",
                City = Cities[rng.Next(Cities.Length)],
                Tier = tier,
                Status = (CustomerStatus)(rng.Next(10) switch { < 7 => 0, < 9 => 1, _ => 2 }),
                Revenue = Math.Round((decimal)(rng.NextDouble() * (tier switch { CustomerTier.Enterprise => 180_000, CustomerTier.Growth => 40_000, _ => 8_000 })) + 500, 0),
                CreatedAt = now.AddDays(-rng.Next(1, 720)),
            };
        }).ToList();
        _nextCustomerId = _customers.Count + 1;

        var categories = ProductInput.Categories;
        (string Name, string Icon, string Description)[] catalog =
        [
            ("Edge Gateway X2", "hdd", "Industrial gateway with dual LTE uplinks and local rules engine."),
            ("Sensor Hub", "tb:cpu", "Collects readings from up to 64 wireless sensors."),
            ("Analytics Suite", "dot-chart", "Dashboards, alerts and exports for every site."),
            ("Cloud Backup", "cloud-upload", "Nightly encrypted backups with 90-day retention."),
            ("Onboarding Pack", "rocket", "Remote setup session and a tailored rollout plan."),
            ("Support Plus", "tb:lifebuoy", "Priority support with a four-hour response time."),
            ("Dock Pro", "desktop", "Charging dock for field tablets with Ethernet pass-through."),
            ("Rack Kit 42U", "cloud-server", "Floor-standing 42U rack with cable management."),
            ("Field Tablet", "tablet", "Rugged 10-inch tablet, IP67, glove-friendly screen."),
            ("Label Printer", "printer", "Thermal printer for shelf and shipping labels."),
            ("Barcode Scanner", "barcode", "Wireless 2D scanner with a 30 m range."),
            ("POS Terminal", "desktop", "Countertop terminal with contactless payments."),
            ("Inventory Cloud", "cloud", "Real-time stock across stores and warehouses."),
            ("Fleet Tracker", "truck", "GPS tracker with driver scoring and geofences."),
            ("Smart Lock", "lock", "Keyless lock with audit trail and remote unlock."),
            ("Access Card (100)", "idcard", "Pack of 100 encrypted access cards."),
        ];
        _products = catalog.Select((item, i) => new Product
        {
            Id = i + 1,
            Name = item.Name,
            Icon = item.Icon,
            Description = item.Description,
            Sku = $"MD-{1000 + i * 7}",
            Category = categories[i % categories.Length],
            Price = Math.Round((decimal)(rng.NextDouble() * 1800) + 19, 0) - 0.01m,
            Stock = i % 7 == 3 ? 0 : i % 5 == 2 ? rng.Next(4, 19) : rng.Next(20, 420),
            Sold = rng.Next(40, 2600),
            Status = i % 9 == 4 ? ProductStatus.Scheduled : i % 11 == 6 ? ProductStatus.Inactive : ProductStatus.Published,
        }).ToList();
        _nextProductId = _products.Count + 1;

        SeedApps();
    }

    public ProTableResponse<Customer> QueryCustomers(CustomerQuery query, ProTableRequest request)
    {
        lock (_gate)
        {
            IEnumerable<Customer> rows = _customers;
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim();
                rows = rows.Where(c => c.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                                       || c.Email.Contains(k, StringComparison.OrdinalIgnoreCase)
                                       || c.Company.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            if (query.Status is { } status) rows = rows.Where(c => c.Status == status);
            if (query.Tier is { } tier) rows = rows.Where(c => c.Tier == tier);
            if (!string.IsNullOrWhiteSpace(query.City)) rows = rows.Where(c => c.City == query.City);

            rows = request.SortField is null
                ? rows.OrderByDescending(c => c.CreatedAt)
                : rows.SortBy(request, nameof(Customer.Name), c => c.Name)
                      .SortBy(request, nameof(Customer.Revenue), c => c.Revenue)
                      .SortBy(request, nameof(Customer.CreatedAt), c => c.CreatedAt);

            return rows.ToPage(request);
        }
    }

    public Customer? FindCustomer(int id)
    {
        lock (_gate) return _customers.FirstOrDefault(c => c.Id == id);
    }

    /// <summary>Customers as select options (id, "Name · Company"), newest first, for pickers with "Add new".</summary>
    public IReadOnlyList<GridOption> CustomerOptions()
    {
        lock (_gate) return [.. _customers.OrderByDescending(c => c.Id).Select(CustomerOption)];
    }

    public static GridOption CustomerOption(Customer c) => new(c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"{c.Name} · {c.Company}");

    public Customer AddCustomer(CustomerInput input)
    {
        lock (_gate)
        {
            var customer = new Customer { Id = _nextCustomerId++, CreatedAt = DateTime.Now };
            Apply(customer, input);
            _customers.Add(customer);
            return customer;
        }
    }

    public bool UpdateCustomer(int id, CustomerInput input)
    {
        lock (_gate)
        {
            var customer = _customers.FirstOrDefault(c => c.Id == id);
            if (customer is null) return false;
            Apply(customer, input);
            return true;
        }
    }

    public int DeleteCustomers(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        lock (_gate) return _customers.RemoveAll(c => set.Contains(c.Id));
    }

    public IReadOnlyList<string> CustomerCities()
    {
        lock (_gate) return _customers.Select(c => c.City).Distinct().Order().ToList();
    }

    public IReadOnlyList<Product> Products()
    {
        lock (_gate) return _products.ToList();
    }

    public CustomerStats CustomerStats()
    {
        lock (_gate)
            return new(_customers.Count, _customers.Count(c => c.Status == CustomerStatus.Active), _customers.Count(c => c.Status == CustomerStatus.Trial),
                _customers.Count(c => c.Status == CustomerStatus.Churned), _customers.Sum(c => c.Revenue));
    }

    // Products -----------------------------------------------------------------

    public ProTableResponse<Product> QueryProducts(ProductQuery query, ProTableRequest request)
    {
        lock (_gate)
        {
            IEnumerable<Product> rows = _products;
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var k = query.Keyword.Trim();
                rows = rows.Where(p => p.Name.Contains(k, StringComparison.OrdinalIgnoreCase) || p.Sku.Contains(k, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(query.Category)) rows = rows.Where(p => p.Category == query.Category);
            if (query.Status is { } status) rows = rows.Where(p => p.Status == status);
            rows = query.Stock switch
            {
                "in" => rows.Where(p => p.Stock >= 20),
                "low" => rows.Where(p => p.LowStock),
                "out" => rows.Where(p => p.OutOfStock),
                _ => rows,
            };
            rows = request.SortField is null
                ? rows.OrderBy(p => p.Id)
                : rows.SortBy(request, nameof(Product.Name), p => p.Name)
                      .SortBy(request, nameof(Product.Category), p => p.Category)
                      .SortBy(request, nameof(Product.Price), p => p.Price)
                      .SortBy(request, nameof(Product.Stock), p => p.Stock)
                      .SortBy(request, nameof(Product.Sold), p => p.Sold);
            return rows.ToPage(request);
        }
    }

    public Product? FindProduct(int id)
    {
        lock (_gate) return _products.FirstOrDefault(p => p.Id == id);
    }

    public Product SaveProduct(int? id, ProductInput input)
    {
        lock (_gate)
        {
            var product = id is { } existing ? _products.First(p => p.Id == existing) : new Product { Id = _nextProductId++ };
            product.Name = input.Name?.Trim() ?? "";
            product.Sku = input.Sku?.Trim().ToUpperInvariant() ?? "";
            product.Category = input.Category ?? "";
            product.Description = input.Description?.Trim() ?? "";
            product.Price = input.Price ?? 0;
            product.Stock = input.Stock ?? 0;
            product.Status = input.Status;
            if (id is null) _products.Add(product);
            return product;
        }
    }

    public int DeleteProducts(IEnumerable<int> ids)
    {
        var set = ids.ToHashSet();
        lock (_gate) return _products.RemoveAll(p => set.Contains(p.Id));
    }

    public ProductStats ProductStats()
    {
        lock (_gate)
            return new(_products.Count, _products.Count(p => p.Status == ProductStatus.Published), _products.Count(p => p.LowStock),
                _products.Count(p => p.OutOfStock), _products.Sum(p => p.Price * p.Stock));
    }

    private static void Apply(Customer c, CustomerInput input)
    {
        // MVC model binding turns empty fields into null; normalise here for every caller.
        c.Name = input.Name?.Trim() ?? "";
        c.Company = input.Company?.Trim() ?? "";
        c.Email = input.Email?.Trim() ?? "";
        c.City = input.City?.Trim() ?? "";
        c.Tier = input.Tier;
        c.Status = input.Status;
    }
}
