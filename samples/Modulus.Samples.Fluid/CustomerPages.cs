using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Modulus.AspNetCore.Fluid;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

/// <summary>
/// Customers on the Fluid sample: a list with search and paging, a create form, an edit form and delete. They use the sample store
/// and the same validation as the Blazor form (<see cref="CustomerInput"/>). Every route needs a signed-in user.
/// </summary>
internal static class CustomerPages
{
    private const int PageSize = 10;

    // The messages after a save or delete, by the code the redirect carries (never free text from the URL).
    private static readonly Dictionary<string, string> Done = new(StringComparer.Ordinal)
    {
        ["created"] = "Customer created.",
        ["updated"] = "Customer saved.",
        ["deleted"] = "Customer deleted.",
    };

    public static void Map(WebApplication app, string templates)
    {
        app.MapGet("/customers", (HttpContext context, SampleStore store, ModulusLiquid liquid, IAntiforgery antiforgery) =>
        {
            var keyword = context.Request.Query["q"].ToString().Trim();
            var page = ParsePage(context.Request.Query["p"].ToString());
            var result = store.QueryCustomers(new CustomerQuery { Keyword = keyword }, new ProTableRequest { Current = page, PageSize = PageSize });
            var done = context.Request.Query["done"].ToString();
            return Render(liquid, templates, "customers/index", "Customers", context, antiforgery, new Dictionary<string, object?>
            {
                ["rows"] = result.Data.Select(Row).ToList(),
                ["keyword"] = keyword,
                ["page"] = page,
                ["has_prev"] = page > 1,
                ["prev_page"] = page - 1,
                ["has_next"] = page * PageSize < result.Total,
                ["next_page"] = page + 1,
                ["total"] = result.Total,
                ["message"] = Done.GetValueOrDefault(done),
            });
        }).RequireAuthorization();

        app.MapGet("/customers/new", (HttpContext context, ModulusLiquid liquid, IAntiforgery antiforgery) =>
            Form(liquid, templates, context, antiforgery, null, new CustomerInput(), [])).RequireAuthorization();

        app.MapPost("/customers/new", async (HttpContext context, SampleStore store, ModulusLiquid liquid, IAntiforgery antiforgery) =>
        {
            if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
            var input = await ReadInput(context);
            var errors = Validate(input);
            if (errors.Count > 0) return Form(liquid, templates, context, antiforgery, null, input, errors);
            store.AddCustomer(input);
            return Results.LocalRedirect("/customers?done=created");
        }).RequireAuthorization();

        app.MapGet("/customers/{id:int}/edit", (int id, HttpContext context, SampleStore store, ModulusLiquid liquid, IAntiforgery antiforgery) =>
        {
            var customer = store.FindCustomer(id);
            return customer is null
                ? Results.NotFound()
                : Form(liquid, templates, context, antiforgery, id, CustomerInput.From(customer), []);
        }).RequireAuthorization();

        app.MapPost("/customers/{id:int}/edit", async (int id, HttpContext context, SampleStore store, ModulusLiquid liquid, IAntiforgery antiforgery) =>
        {
            if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
            var input = await ReadInput(context);
            var errors = Validate(input);
            if (errors.Count > 0) return Form(liquid, templates, context, antiforgery, id, input, errors);
            return store.UpdateCustomer(id, input) ? Results.LocalRedirect("/customers?done=updated") : Results.NotFound();
        }).RequireAuthorization();

        app.MapPost("/customers/delete", async (HttpContext context, SampleStore store, IAntiforgery antiforgery) =>
        {
            if (!await ValidAntiforgery(context, antiforgery)) return Results.BadRequest();
            var form = await context.Request.ReadFormAsync();
            if (!int.TryParse(form["id"].ToString(), out var id)) return Results.BadRequest();
            store.DeleteCustomers([id]);
            return Results.LocalRedirect("/customers?done=deleted");
        }).RequireAuthorization();
    }

    private static IResult Form(ModulusLiquid liquid, string templates, HttpContext context, IAntiforgery antiforgery, int? id, CustomerInput input, List<string> errors)
    {
        var editing = id is not null;
        return Render(liquid, templates, "customers/form", editing ? "Edit customer" : "New customer", context, antiforgery, new Dictionary<string, object?>
        {
            ["id"] = id,
            ["action"] = editing ? $"/customers/{id}/edit" : "/customers/new",
            ["name"] = input.Name,
            ["company"] = input.Company,
            ["email"] = input.Email,
            ["city"] = input.City,
            ["tiers"] = Enum.GetValues<CustomerTier>().Select(t => Option(t.ToString(), input.Tier == t)).ToList(),
            ["statuses"] = Enum.GetValues<CustomerStatus>().Select(s => Option(s.ToString(), input.Status == s)).ToList(),
            ["errors"] = errors,
        });
    }

    private static async Task<CustomerInput> ReadInput(HttpContext context)
    {
        var form = await context.Request.ReadFormAsync();
        return new CustomerInput
        {
            Name = form["name"].ToString().Trim(),
            Company = form["company"].ToString().Trim(),
            Email = form["email"].ToString().Trim(),
            City = form["city"].ToString().Trim(),
            Tier = Enum.TryParse<CustomerTier>(form["tier"].ToString(), out var tier) ? tier : CustomerTier.Starter,
            Status = Enum.TryParse<CustomerStatus>(form["status"].ToString(), out var status) ? status : CustomerStatus.Trial,
        };
    }

    // The data-annotation messages of CustomerInput, in the order the fields appear on the form.
    private static List<string> Validate(CustomerInput input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "Check this form.").Distinct(StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, object?> Row(Customer c) => new()
    {
        ["id"] = c.Id,
        ["name"] = c.Name,
        ["company"] = c.Company,
        ["email"] = c.Email,
        ["city"] = c.City,
        ["tier"] = c.Tier.ToString(),
        ["status"] = c.Status.ToString(),
    };

    private static Dictionary<string, object?> Option(string value, bool selected) => new() { ["value"] = value, ["selected"] = selected };

    private static int ParsePage(string text) => int.TryParse(text, out var page) && page >= 1 ? page : 1;

    private static IResult Render(ModulusLiquid liquid, string templates, string page, string title, HttpContext context, IAntiforgery antiforgery, Dictionary<string, object?> values)
    {
        values["title"] = title;
        values["antiforgery_token"] = antiforgery.GetAndStoreTokens(context).RequestToken;
        var html = liquid.RenderPage(
            liquid.Load(Path.Combine(templates, page + ".liquid")),
            liquid.Load(Path.Combine(templates, "_layout.liquid")),
            values);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static async Task<bool> ValidAntiforgery(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
