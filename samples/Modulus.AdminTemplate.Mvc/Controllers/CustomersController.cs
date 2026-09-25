using Microsoft.AspNetCore.Mvc;

namespace Modulus.AdminTemplate.Mvc.Controllers;

public class CustomersController : Controller
{
    private static List<Customer> Customers = new()
    {
        new() { Id = 1, Name = "John Doe", Email = "john@example.com", Phone = "+1 555-0101", City = "New York", Status = "active" },
        new() { Id = 2, Name = "Jane Smith", Email = "jane@example.com", Phone = "+1 555-0102", City = "Los Angeles", Status = "active" },
        new() { Id = 3, Name = "Bob Johnson", Email = "bob@example.com", Phone = "+1 555-0103", City = "Chicago", Status = "inactive" },
        new() { Id = 4, Name = "Alice Brown", Email = "alice@example.com", Phone = "+1 555-0104", City = "Houston", Status = "active" },
    };

    public IActionResult Index()
    {
        return View(Customers);
    }

    public IActionResult Create()
    {
        return View(new Customer { Status = "active" });
    }

    [HttpPost]
    public IActionResult Create(Customer customer)
    {
        if (ModelState.IsValid)
        {
            customer.Id = Customers.Max(c => c.Id) + 1;
            Customers.Add(customer);
            return RedirectToAction(nameof(Index));
        }
        return View(customer);
    }

    public IActionResult Edit(int id)
    {
        var customer = Customers.FirstOrDefault(c => c.Id == id);
        if (customer is null)
            return NotFound();
        return View(customer);
    }

    [HttpPost]
    public IActionResult Edit(Customer customer)
    {
        if (ModelState.IsValid)
        {
            var existing = Customers.FirstOrDefault(c => c.Id == customer.Id);
            if (existing is not null)
            {
                existing.Name = customer.Name;
                existing.Email = customer.Email;
                existing.Phone = customer.Phone;
                existing.City = customer.City;
                existing.Status = customer.Status;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(customer);
    }

    public IActionResult Delete(int id)
    {
        var customer = Customers.FirstOrDefault(c => c.Id == id);
        if (customer is not null)
        {
            Customers.Remove(customer);
        }
        return RedirectToAction(nameof(Index));
    }
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
}
