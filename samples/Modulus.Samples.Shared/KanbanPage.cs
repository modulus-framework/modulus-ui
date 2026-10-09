using Modulus.Ui.Abstractions.Kanban;

namespace Modulus.Samples.Shared;

/// <summary>The kanban sample's board: the store's tasks as the library's cards, with the sample's columns and labels.</summary>
public static class KanbanPage
{
    /// <summary>The column whose cards are finished (never overdue).</summary>
    public const string DoneColumn = "done";

    /// <summary>The store's tasks as cards.</summary>
    public static IReadOnlyList<KanbanCard> Cards(IEnumerable<KanbanTask> tasks) =>
        tasks.Select(t => new KanbanCard(t.Id, t.Column, t.Order, t.Title, t.Label, t.Due, t.Assignees, t.Comments, t.Attachments)).ToList();

    /// <summary>The board to draw from the store's tasks.</summary>
    public static KanbanBoardView Board(IEnumerable<KanbanTask> tasks) =>
        KanbanModel.Build(SampleStore.KanbanColumns, Cards(tasks), Lookups.KanbanLabels, DoneColumn, DateTime.Today);
}
