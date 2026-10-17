using ContosoMvc.Models;

namespace ContosoMvc.Data;

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

    public int Count => _customers.Count;
    public int ActiveCount => _customers.Count(c => c.Active);
    public decimal Revenue => _customers.Sum(c => c.Revenue);

    /// <summary>Customers whose name or company contains the keyword (all of them when it is empty).</summary>
    public IReadOnlyList<Customer> Search(string? keyword) =>
        string.IsNullOrWhiteSpace(keyword)
            ? _customers.OrderBy(c => c.Name).ToList()
            : _customers.Where(c => c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                 || c.Company.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(c => c.Name).ToList();

    public Customer? Find(int id) => _customers.FirstOrDefault(c => c.Id == id);

    public void Add(CustomerInput input) =>
        _customers.Add(new Customer
        {
            Id = _nextId++, Name = input.Name, Company = input.Company, Email = input.Email,
            City = input.City, Revenue = input.Revenue, Active = input.Active,
        });

    public bool Update(int id, CustomerInput input)
    {
        if (Find(id) is not { } c) return false;
        c.Name = input.Name;
        c.Company = input.Company;
        c.Email = input.Email;
        c.City = input.City;
        c.Revenue = input.Revenue;
        c.Active = input.Active;
        return true;
    }

    public bool Delete(int id) => _customers.RemoveAll(c => c.Id == id) > 0;
}
