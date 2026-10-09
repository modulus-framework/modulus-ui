using System.Globalization;

namespace Modulus.Ui.Abstractions.Kanban;

/// <summary>A board column: its id (the card's column), its title.</summary>
public sealed record KanbanColumn(string Id, string Title);

/// <summary>
/// A card as the board stores it: its id, the column it sits in and its place there (<see cref="Order"/>), its title,
/// a label, an optional due date, the people on it and its comment and attachment counts.
/// </summary>
public sealed record KanbanCard(
    int Id,
    string ColumnId,
    int Order,
    string Title,
    string Label,
    DateTime? Due,
    IReadOnlyList<string> Assignees,
    int Comments = 0,
    int Attachments = 0);

/// <summary>A card moved: its id, the column it goes to and its place there.</summary>
public sealed record KanbanMove(int CardId, string ColumnId, int Index);

/// <summary>A card to add: the column, its title and its label.</summary>
public sealed record KanbanNewCard(string ColumnId, string Title, string Label);

/// <summary>A person on a card: the name, the initials shown in the avatar, and the avatar's tint (1 to 8).</summary>
public sealed record KanbanAvatar(string Name, string Initials, int Tint);

/// <summary>A card ready to draw: its due chip (text and whether it is overdue), the tone of its label, and its first three people.</summary>
public sealed record KanbanCardView(KanbanCard Card, string DueText, bool Late, string LabelTone, IReadOnlyList<KanbanAvatar> Avatars);

/// <summary>A column ready to draw: its cards in order.</summary>
public sealed record KanbanColumnView(KanbanColumn Column, IReadOnlyList<KanbanCardView> Cards);

/// <summary>The whole board ready to draw, with the labels a card can take.</summary>
public sealed record KanbanBoardView(IReadOnlyList<KanbanColumnView> Columns, IReadOnlyList<string> Labels);

/// <summary>
/// The board's model, shared by the Blazor <c>KanbanBoard</c> and the MVC <c>app-kanban</c>: columns in order, each
/// with its cards sorted by their place, due dates marked overdue unless the card is in the done column, and labels and
/// avatars given their tones and tints. Both front ends draw from this view.
/// </summary>
public static class KanbanModel
{
    /// <summary>The most people one card shows before the rest are left out.</summary>
    public const int MaxAvatars = 3;

    /// <summary>The longest title a card may have.</summary>
    public const int MaxTitleLength = 120;

    /// <summary>Builds the board. A card whose column is unknown is left out (it is not on the board).</summary>
    public static KanbanBoardView Build(IReadOnlyList<KanbanColumn> columns, IEnumerable<KanbanCard> cards, IReadOnlyList<string> labels, string doneColumnId, DateTime today)
    {
        var known = columns.Select(c => c.Id).ToHashSet();
        var byColumn = cards.Where(c => known.Contains(c.ColumnId)).GroupBy(c => c.ColumnId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Order).ThenBy(c => c.Id).ToList());
        var views = columns.Select(column => new KanbanColumnView(column,
            byColumn.TryGetValue(column.Id, out var list)
                ? list.Select(card => Card(card, doneColumnId, today)).ToList()
                : [])).ToList();
        return new KanbanBoardView(views, labels);
    }

    /// <summary>The problem with a new card's title, or null when it is usable.</summary>
    public static string? TitleError(string? title) => string.IsNullOrWhiteSpace(title)
        ? "Give the task a title"
        : title.Trim().Length > MaxTitleLength ? $"The title is longer than {MaxTitleLength} characters" : null;

    /// <summary>The label's tone (a colour name for its badge).</summary>
    public static string LabelTone(string label) => label switch
    {
        "UX" => "green",
        "Code review" => "red",
        "Dashboard" => "azure",
        "Images" => "orange",
        "App" => "purple",
        _ => "teal",
    };

    /// <summary>The initials of a name: the first letter of its first two words.</summary>
    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    /// <summary>A name's avatar tint, 1 to 8: the same name always gets the same colour.</summary>
    public static int Tint(string name) => Math.Abs(name.Aggregate(17, (h, c) => h * 31 + c)) % 8 + 1;

    private static KanbanCardView Card(KanbanCard card, string doneColumnId, DateTime today)
    {
        var due = card.Due is { } d ? d.ToString("MMM d", CultureInfo.InvariantCulture) : "";
        var late = card.Due is { } date && date.Date < today.Date && card.ColumnId != doneColumnId;
        var avatars = card.Assignees.Take(MaxAvatars).Select(name => new KanbanAvatar(name, Initials(name), Tint(name))).ToList();
        return new KanbanCardView(card, due, late, LabelTone(card.Label), avatars);
    }
}
