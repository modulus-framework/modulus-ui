using System.ComponentModel.DataAnnotations;
using Modulus.Ui.Abstractions;

namespace ContosoAdmin.Data;

/// <summary>A row in the customer table.</summary>
public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Email { get; set; } = "";
    public string City { get; set; } = "";
    public decimal Revenue { get; set; }
    public bool Active { get; set; }
}

/// <summary>The form's fields, with the validation rules the drawer enforces.</summary>
public sealed class CustomerInput
{
    [Required(ErrorMessage = "Enter the contact's name")]
    [StringLength(80)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter a company")]
    [StringLength(80)]
    public string Company { get; set; } = "";

    [Required(ErrorMessage = "Enter an email address")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter a city")]
    public string City { get; set; } = "";

    [Range(0, 1_000_000_000, ErrorMessage = "Revenue cannot be negative")]
    public decimal Revenue { get; set; }

    public bool Active { get; set; } = true;
}

/// <summary>In-memory data. Replace it with your database context; keep the method shapes.</summary>
public sealed class CustomerStore
{
    private readonly List<Customer> _customers =
    [
        new() { Id = 1, Name = "Olivia Bennett", Company = "Northwind", Email = "olivia@northwind.com", City = "London", Revenue = 48200, Active = true },
        new() { Id = 2, Name = "Liam Okafor", Company = "Fabrikam", Email = "liam@fabrikam.com", City = "Lagos", Revenue = 12750, Active = true },
        new() { Id = 3, Name = "Mei Tanaka", Company = "Contoso", Email = "mei@contoso.jp", City = "Tokyo", Revenue = 91300, Active = false },
        new() { Id = 4, Name = "Sofia Rossi", Company = "Litware", Email = "sofia@litware.it", City = "Milan", Revenue = 33040, Active = true },
    ];

    private int _nextId = 5;

    public IReadOnlyList<Customer> All => _customers;

    /// <summary>Filters, sorts and pages for a ProTable request. Sorting keys are the column names the table sends.</summary>
    public ProTableResponse<Customer> Query(ProTableRequest request, string? keyword)
    {
        IEnumerable<Customer> rows = _customers;

        if (!string.IsNullOrWhiteSpace(keyword))
            rows = rows.Where(c => c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                || c.Company.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        return rows.SortBy(request, new Dictionary<string, Func<Customer, object?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = c => c.Name,
                ["company"] = c => c.Company,
                ["revenue"] = c => c.Revenue,
            })
            .ToPage(request);
    }

    public Customer? Find(int id) => _customers.FirstOrDefault(c => c.Id == id);

    public void Add(CustomerInput input) =>
        _customers.Add(new Customer { Id = _nextId++, Name = input.Name, Company = input.Company, Email = input.Email,
                                      City = input.City, Revenue = input.Revenue, Active = input.Active });

    public void Update(int id, CustomerInput input)
    {
        if (Find(id) is not { } c) return;
        c.Name = input.Name;
        c.Company = input.Company;
        c.Email = input.Email;
        c.City = input.City;
        c.Revenue = input.Revenue;
        c.Active = input.Active;
    }

    public void Delete(IEnumerable<int> ids) => _customers.RemoveAll(c => ids.Contains(c.Id));
}
