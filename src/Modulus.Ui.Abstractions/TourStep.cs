namespace Modulus.Ui.Abstractions;

/// <summary>
/// One step of a guided tour (Ant Design 5 Tour), shared by the Blazor <c>AppTour</c> and the MVC
/// <c>&lt;app-tour&gt;</c>/<c>&lt;tour-step&gt;</c>.
/// </summary>
/// <param name="Title">Heading of the step.</param>
/// <param name="Description">Body text.</param>
/// <param name="Target">CSS selector of the element to highlight; null centres the step on the screen.</param>
/// <param name="Cover">Optional image URL shown above the title.</param>
/// <param name="Placement">top, bottom, left or right of the target.</param>
public sealed record TourStep(string Title, string? Description = null, string? Target = null, string? Cover = null,
    string Placement = "bottom");
