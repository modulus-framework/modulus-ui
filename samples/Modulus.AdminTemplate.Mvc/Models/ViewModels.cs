using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Models;

public sealed class ProfileInput
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a display name")]
    public string Name { get; set; } = "Serena Park";

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter an email")]
    [System.ComponentModel.DataAnnotations.RegularExpression(EmailPattern.Value, ErrorMessage = "Enter a valid email")]
    public string Email { get; set; } = "serena.park@modulus.dev";

    public string? Bio { get; set; } = "Operations lead. Keeps the lights on.";

    public string TimeZone { get; set; } = "Europe/London";
}
