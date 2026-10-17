using System.ComponentModel.DataAnnotations;

namespace ContosoMvc.Models;

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

    public static CustomerInput From(Customer c) => new()
    {
        Name = c.Name, Company = c.Company, Email = c.Email, City = c.City, Revenue = c.Revenue, Active = c.Active,
    };
}
