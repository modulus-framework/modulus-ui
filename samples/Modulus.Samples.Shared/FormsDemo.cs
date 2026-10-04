using System.ComponentModel.DataAnnotations;

namespace Modulus.Samples.Shared;

// Inputs for the Forms pages (/forms/layouts, /forms/wizard, /forms/validation). Every sample binds the
// same classes, so the validation messages are identical in Blazor, MVC and Razor Pages.

/// <summary>Vertical layout: contact form.</summary>
public sealed class ContactFormInput
{
    [Required(ErrorMessage = "Enter your full name"), StringLength(60)]
    public string? FullName { get; set; }

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [StringLength(60)]
    public string? Company { get; set; }

    [Required(ErrorMessage = "Choose a topic")]
    public string? Topic { get; set; }

    [Required(ErrorMessage = "Write a message"), StringLength(500, MinimumLength = 10, ErrorMessage = "Write at least 10 characters")]
    public string? Message { get; set; }

    public static readonly string[] Topics = ["Sales", "Billing", "Technical support", "Partnerships"];
}

/// <summary>Horizontal layout: sign-in details.</summary>
public sealed class SignupFormInput
{
    [Required(ErrorMessage = "Choose a username"), StringLength(20, MinimumLength = 3, ErrorMessage = "Use 3 to 20 characters")]
    [RegularExpression("^[a-zA-Z0-9_.]+$", ErrorMessage = "Use letters, numbers, dots and underscores")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter a password"), StringLength(64, MinimumLength = 8, ErrorMessage = "Use at least 8 characters")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Repeat the password"), Compare(nameof(Password), ErrorMessage = "The passwords don't match")]
    public string? ConfirmPassword { get; set; }

    public bool Newsletter { get; set; } = true;
}

/// <summary>Multi-column layout with sections: shipping details.</summary>
public sealed class ShippingFormInput
{
    [Required(ErrorMessage = "Enter a first name"), StringLength(40)]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Enter a last name"), StringLength(40)]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Enter a street address"), StringLength(120)]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Enter a city"), StringLength(60)]
    public string? City { get; set; }

    [Required(ErrorMessage = "Enter a postal code"), StringLength(12)]
    public string? Zip { get; set; }

    [Required(ErrorMessage = "Choose a country")]
    public string? Country { get; set; }

    [Required(ErrorMessage = "Choose a delivery option")]
    public string Delivery { get; set; } = "standard";

    public static readonly (string Value, string Label, string Note)[] DeliveryOptions =
        [("standard", "Standard", "3–5 business days · free"), ("express", "Express", "1–2 business days · $12"), ("overnight", "Overnight", "Next business day · $29")];
}

/// <summary>Four-step account wizard. Each step validates only its own fields.</summary>
public sealed class WizardInput
{
    public static readonly (string Title, string Description, string Icon)[] Steps =
        [("Account", "Sign-in details", "user"), ("Personal", "About you", "id"), ("Social", "Profile links", "link"), ("Review", "Confirm and submit", "tb:checks")];

    /// <summary>Property names validated on each step (index = step).</summary>
    public static readonly string[][] StepFields =
    [
        [nameof(Username), nameof(Email), nameof(Password), nameof(ConfirmPassword)],
        [nameof(FirstName), nameof(LastName), nameof(Country), nameof(Language)],
        [nameof(Twitter), nameof(LinkedIn), nameof(GitHub)],
        [nameof(AcceptTerms)],
    ];

    [Required(ErrorMessage = "Choose a username"), StringLength(20, MinimumLength = 3, ErrorMessage = "Use 3 to 20 characters")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter a password"), StringLength(64, MinimumLength = 8, ErrorMessage = "Use at least 8 characters")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Repeat the password"), Compare(nameof(Password), ErrorMessage = "The passwords don't match")]
    public string? ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Enter your first name"), StringLength(40)]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Enter your last name"), StringLength(40)]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Choose a country")]
    public string? Country { get; set; }

    [Required(ErrorMessage = "Choose a language")]
    public string? Language { get; set; } = "English";

    [Url(ErrorMessage = "Enter a full URL, e.g. https://x.com/you")]
    public string? Twitter { get; set; }

    [Url(ErrorMessage = "Enter a full URL, e.g. https://linkedin.com/in/you")]
    public string? LinkedIn { get; set; }

    [Url(ErrorMessage = "Enter a full URL, e.g. https://github.com/you")]
    public string? GitHub { get; set; }

    [MustBeTrue(ErrorMessage = "Accept the terms to continue")]
    public bool AcceptTerms { get; set; }

    /// <summary>Validates the fields of one step; returns errors keyed by property name.</summary>
    public Dictionary<string, string[]> Validate(int step)
    {
        var fields = StepFields[Math.Clamp(step, 0, StepFields.Length - 1)];
        return ModelValidation.Validate(this).Where(e => fields.Contains(e.Key)).ToDictionary();
    }
}

/// <summary>Data-annotation validation outside a form framework (the Blazor pages that build their own form items).</summary>
public static class ModelValidation
{
    /// <summary>Errors keyed by property name; empty when the model is valid.</summary>
    public static Dictionary<string, string[]> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results
            .SelectMany(r => r.MemberNames.Select(m => (Member: m, r.ErrorMessage)))
            .GroupBy(e => e.Member)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage ?? "Invalid value").ToArray());
    }
}

/// <summary>Every common rule on one form (/forms/validation).</summary>
public sealed class ValidationDemoInput
{
    [Required(ErrorMessage = "Enter your name"), StringLength(40, MinimumLength = 2, ErrorMessage = "Use 2 to 40 characters")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Enter an email address")]
    [RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email address")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Enter a password")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Use 8+ characters with upper and lower case letters and a number")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Repeat the password"), Compare(nameof(Password), ErrorMessage = "The passwords don't match")]
    public string? ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Enter your age"), Range(18, 99, ErrorMessage = "You must be between 18 and 99")]
    public int? Age { get; set; }

    [Url(ErrorMessage = "Enter a full URL starting with http:// or https://")]
    public string? Website { get; set; }

    [RegularExpression(@"^\+?[0-9 ()-]{7,20}$", ErrorMessage = "Enter a phone number, e.g. +1 202 555 0111")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Pick a date"), FutureDate(ErrorMessage = "Pick a date after today")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Choose a plan")]
    public string? Plan { get; set; }

    [Required(ErrorMessage = "Choose a billing cycle")]
    public string? Billing { get; set; }

    [MinCount(1, ErrorMessage = "Pick at least one interest")]
    public List<string> Interests { get; set; } = [];

    [Required(ErrorMessage = "Tell us about yourself"), StringLength(300, MinimumLength = 20, ErrorMessage = "Write 20 to 300 characters")]
    public string? Bio { get; set; }

    [MustBeTrue(ErrorMessage = "Accept the terms to continue")]
    public bool AcceptTerms { get; set; }

    public static readonly string[] Plans = ["Starter", "Team", "Business", "Enterprise"];
    public static readonly (string Value, string Label)[] BillingCycles = [("monthly", "Monthly"), ("yearly", "Yearly (save 20%)")];
    public static readonly string[] InterestOptions = ["Design", "Development", "Marketing", "Sales", "Support"];

    /// <summary>The rules shown beside the form.</summary>
    public static readonly (string Rule, string Where)[] Rules =
    [
        ("Required", "Name, email, password, age, date, plan, billing, bio"),
        ("Length", "Name 2–40 characters, bio 20–300"),
        ("Format", "Email, website URL and phone number patterns"),
        ("Password strength", "8+ characters with upper and lower case letters and a number"),
        ("Compare", "Confirm password must match"),
        ("Range", "Age between 18 and 99"),
        ("Custom: future date", "Start date after today"),
        ("Custom: at least one", "Pick one or more interests"),
        ("Custom: must be true", "Accept the terms"),
    ];
}

/// <summary>A checkbox that must be ticked (terms, consent).</summary>
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}

/// <summary>A date strictly after today.</summary>
public sealed class FutureDateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not DateTime d || d.Date > DateTime.Today;
}

/// <summary>A collection with at least <see cref="Min"/> items.</summary>
public sealed class MinCountAttribute(int min) : ValidationAttribute
{
    public int Min { get; } = min;

    public override bool IsValid(object? value) => value is System.Collections.ICollection c && c.Count >= Min;
}
