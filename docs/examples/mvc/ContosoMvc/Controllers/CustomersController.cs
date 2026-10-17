using ContosoMvc.Data;
using ContosoMvc.Models;
using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Ui.Abstractions;

namespace ContosoMvc.Controllers;

/// <summary>The customer list, its table fragment, and the add/edit drawer. Saves answer 204 and send a refresh event.</summary>
[Route("customers")]
public class CustomersController(CustomerStore store) : Controller
{
    // Sent to the page after a save or delete. The table listens for it and reloads itself.
    public const string RefreshEvent = "customers:refresh";

    [HttpGet("")]
    public IActionResult Index(string? keyword)
    {
        ViewData["Title"] = "Customers";
        ViewData["Keyword"] = keyword;
        return View(store.Search(keyword));
    }

    // The table on its own, used for refreshes.
    [HttpGet("table")]
    public IActionResult Table(string? keyword) => PartialView("_Table", store.Search(keyword));

    [HttpGet("form/{id:int?}")]
    public IActionResult Form(int? id)
    {
        if (id is null) return Drawer(null, new CustomerInput());
        return store.Find(id.Value) is { } customer ? Drawer(id, CustomerInput.From(customer)) : NotFound();
    }

    [HttpPost("save/{id:int?}")]
    public IActionResult Save(int? id, CustomerInput input)
    {
        // Invalid input: send the drawer back with the error messages. htmx swaps it into the drawer.
        if (!ModelState.IsValid) return Drawer(id, input);

        if (id is { } existing)
        {
            if (!store.Update(existing, input)) return NotFound();
        }
        else store.Add(input);

        Response.HxMessage(MessageType.Success, id is null ? $"Added {input.Name}" : $"Saved {input.Name}")
                .HxCloseDrawer()
                .HxTrigger(RefreshEvent);
        return NoContent();
    }

    [HttpPost("delete/{id:int}")]
    public IActionResult Delete(int id)
    {
        if (!store.Delete(id)) return NotFound();

        Response.HxMessage(MessageType.Success, "Customer deleted").HxTrigger(RefreshEvent);
        return NoContent();
    }

    private PartialViewResult Drawer(int? id, CustomerInput input)
    {
        ViewData["Id"] = id;
        return PartialView("_Form", input);
    }
}
