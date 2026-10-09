using Modulus.Ui.Abstractions.Kanban;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The kanban board's model: column and card order, due dates, tones, avatars and the title rule.</summary>
public class KanbanModelTests
{
    private static readonly KanbanColumn[] Columns = [new("todo", "To do"), new("done", "Done")];
    private static readonly string[] Labels = ["UX", "Code review"];
    private static readonly DateTime Today = new(2026, 10, 9);

    private static KanbanCard Card(int id, string column, int order, DateTime? due = null, params string[] people) =>
        new(id, column, order, $"Task {id}", "UX", due, people);

    [Fact]
    public void Build_KeepsTheColumnsInOrder_AndSortsEachColumnsCardsByPlace()
    {
        var board = KanbanModel.Build(Columns, [Card(3, "todo", 1), Card(1, "todo", 0), Card(2, "done", 0)], Labels, "done", Today);

        Assert.Equal(["todo", "done"], board.Columns.Select(c => c.Column.Id));
        Assert.Equal([1, 3], board.Columns[0].Cards.Select(c => c.Card.Id));
        Assert.Equal([2], board.Columns[1].Cards.Select(c => c.Card.Id));
        Assert.Equal(Labels, board.Labels);
    }

    [Fact]
    public void Build_LeavesOutACardInAColumnThatIsNotOnTheBoard()
    {
        var board = KanbanModel.Build(Columns, [Card(1, "archived", 0), Card(2, "todo", 0)], Labels, "done", Today);

        Assert.Equal([2], board.Columns.SelectMany(c => c.Cards).Select(c => c.Card.Id));
    }

    [Fact]
    public void Build_MarksADueDateOverdue_UnlessTheCardIsDone()
    {
        var yesterday = Today.AddDays(-1);
        var board = KanbanModel.Build(Columns, [Card(1, "todo", 0, yesterday), Card(2, "done", 0, yesterday), Card(3, "todo", 1, Today)], Labels, "done", Today);

        var todo = board.Columns[0].Cards;
        Assert.True(todo[0].Late);
        Assert.Equal("Oct 8", todo[0].DueText);
        Assert.False(todo[1].Late);
        Assert.Equal("Oct 9", todo[1].DueText);
        Assert.False(board.Columns[1].Cards[0].Late);
    }

    [Fact]
    public void Build_WithNoDueDate_HasNoDueText()
    {
        var card = KanbanModel.Build(Columns, [Card(1, "todo", 0)], Labels, "done", Today).Columns[0].Cards.Single();

        Assert.Equal("", card.DueText);
        Assert.False(card.Late);
    }

    [Fact]
    public void Build_ShowsAtMostThreeAvatars_WithInitialsAndAStableTint()
    {
        var card = KanbanModel.Build(Columns, [Card(1, "todo", 0, null, "Ada Lovelace", "Lars Berg", "Mette Holm", "Sami Virtanen")], Labels, "done", Today)
            .Columns[0].Cards.Single();

        Assert.Equal(3, card.Avatars.Count);
        Assert.Equal(new KanbanAvatar("Ada Lovelace", "AL", KanbanModel.Tint("Ada Lovelace")), card.Avatars[0]);
        Assert.InRange(card.Avatars[0].Tint, 1, 8);
        Assert.Equal("LB", card.Avatars[1].Initials);
    }

    [Theory]
    [InlineData("UX", "green")]
    [InlineData("Code review", "red")]
    [InlineData("Charts & maps", "teal")]
    public void LabelTone_ColoursEachKnownLabel_AndFallsBackToTeal(string label, string tone) =>
        Assert.Equal(tone, KanbanModel.LabelTone(label));

    [Fact]
    public void Initials_TakeTheFirstTwoWords()
    {
        Assert.Equal("AL", KanbanModel.Initials("Ada Lovelace Mahamud"));
        Assert.Equal("A", KanbanModel.Initials("ada"));
    }

    [Fact]
    public void TitleError_RefusesABlankOrLongTitle()
    {
        Assert.Equal("Give the task a title", KanbanModel.TitleError("   "));
        Assert.Equal("Give the task a title", KanbanModel.TitleError(null));
        Assert.Equal("The title is longer than 120 characters", KanbanModel.TitleError(new string('x', 121)));
        Assert.Null(KanbanModel.TitleError(" Plan the sprint "));
    }
}
