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

    /// <summary>
    /// htmx endpoint: Returns filtered/paginated customer table body as partial
    /// </summary>
    [HttpGet]
    public IActionResult GetTableBody(string? search = null, int page = 1, int pageSize = 10)
    {
        var filtered = Customers.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLower();
            filtered = filtered.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.Email.ToLower().Contains(term) ||
                c.City.ToLower().Contains(term));
        }

        // Apply pagination
        var total = filtered.Count();
        var paginated = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        ViewBag.Total = total;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;

        return PartialView("~/Views/Components/ProTableBody.cshtml", paginated);
    }

    /// <summary>
    /// htmx endpoint: Delete customer via htmx and return empty response (row removed)
    /// </summary>
    [HttpDelete]
    public IActionResult Delete(int id)
    {
        var customer = Customers.FirstOrDefault(c => c.Id == id);
        if (customer is not null)
        {
            Customers.Remove(customer);
        }
        return Ok(); // htmx will remove the row after this response
    }

    /// <summary>
    /// htmx endpoint: Return form content for modal
    /// </summary>
    [HttpGet]
    public IActionResult FormPartial(int? id = null)
    {
        var customer = id.HasValue ? Customers.FirstOrDefault(c => c.Id == id.Value) : new Customer { Status = "active" };
        return PartialView("~/Views/Components/ProFormContent.cshtml", customer);
    }

    /// <summary>
    /// htmx endpoint: Handle form submission and return validation errors or success
    /// </summary>
    [HttpPost]
    public IActionResult FormSubmit(Customer customer)
    {
        if (!ModelState.IsValid)
        {
            // Return form with validation errors
            return PartialView("~/Views/Components/ProFormContent.cshtml", customer);
        }

        if (customer.Id == 0)
        {
            // Create new
            customer.Id = Customers.Max(c => c.Id) + 1;
            Customers.Add(customer);
        }
        else
        {
            // Update existing
            var existing = Customers.FirstOrDefault(c => c.Id == customer.Id);
            if (existing is not null)
            {
                existing.Name = customer.Name;
                existing.Email = customer.Email;
                existing.Phone = customer.Phone;
                existing.City = customer.City;
                existing.Status = customer.Status;
            }
        }

        // Signal modal close and table refresh
        Response.Headers.Append("HX-Trigger", "formSuccess");
        return Ok();
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
