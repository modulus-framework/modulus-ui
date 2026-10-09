namespace Modulus.Samples.Shared;

// The form elements page (/forms/elements): every control on the Tabler form-elements page, bound to one model. Read-only demo:
// the page submits with GET and keeps no state, so every sample shows the same defaults.

/// <summary>The values the form elements page binds to.</summary>
public sealed class FormElementsInput
{
    public string? Text { get; set; } = "Serena Park";
    public string? Password { get; set; } = "modulus";
    public string? Search { get; set; }
    public string? Notes { get; set; } = "A few lines of free text.";
    public string? Topic { get; set; } = "Billing";
    public string? Language { get; set; } = "C#";
    public bool Notify { get; set; } = true;
    public string? Plan { get; set; } = "Team";
    public string[] Channels { get; set; } = ["Email"];
    public bool Terms { get; set; }
    public double Volume { get; set; } = 40;
    public DateTime? Starts { get; set; } = new DateTime(2026, 10, 15);
    public DateTime? Reminder { get; set; } = new DateTime(2026, 10, 15, 9, 30, 0);
    public string Color { get; set; } = "#1677ff";
}

/// <summary>The form elements page: the model and the choices the controls list.</summary>
public sealed class FormElementsPage
{
    public FormElementsInput Input { get; init; } = new();

    public static readonly string[] Topics = ["Sales", "Billing", "Technical support", "Partnerships"];
    public static readonly string[] Languages = ["C#", "F#", "Go", "Rust", "TypeScript"];
    public static readonly string[] Plans = ["Free", "Team", "Business"];
    public static readonly string[] Channels = ["Email", "SMS", "Push"];

    public static FormElementsPage Load() => new();
}
