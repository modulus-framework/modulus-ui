using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Shell;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The shared sample logic the three flavours build their pages from: the employee directory, the wizard, validation and the reference pages.</summary>
public class SampleLogicTests
{
    [Fact]
    public void EmployeeDirectory_FiltersByKeywordDepartmentAndStatus()
    {
        var first = EmployeeDirectory.All[0];

        var byKeyword = EmployeeDirectory.Filter(new EmployeeQuery(Keyword: first.Name.ToUpperInvariant())).ToList();
        Assert.Contains(first, byKeyword);
        Assert.All(byKeyword, e => Assert.True(
            e.Name.Contains(first.Name, StringComparison.OrdinalIgnoreCase) || e.Email.Contains(first.Name, StringComparison.OrdinalIgnoreCase)
            || e.Position.Contains(first.Name, StringComparison.OrdinalIgnoreCase)));

        var engineers = EmployeeDirectory.Filter(new EmployeeQuery(Department: "Engineering", Status: EmployeeStatus.Active)).ToList();
        Assert.NotEmpty(engineers);
        Assert.All(engineers, e => { Assert.Equal("Engineering", e.Department); Assert.Equal(EmployeeStatus.Active, e.Status); });
    }

    [Fact]
    public void EmployeeDirectory_QuerySortsAndPages_AndSummaryAverages()
    {
        var page = EmployeeDirectory.Query(new EmployeeQuery(), new ProTableRequest { Current = 2, PageSize = 10, SortField = nameof(Employee.Salary), SortOrder = SortDirection.Descending });

        Assert.Equal(EmployeeDirectory.All.Count, page.Total);
        Assert.Equal(10, page.Data.Count);
        Assert.Equal(page.Data.OrderByDescending(e => e.Salary).Select(e => e.Id), page.Data.Select(e => e.Id));
        Assert.True(page.Data[0].Salary <= EmployeeDirectory.All.OrderByDescending(e => e.Salary).ElementAt(9).Salary);

        var (count, average) = EmployeeDirectory.Summary(new EmployeeQuery(Office: "Tokyo"));
        var tokyo = EmployeeDirectory.All.Where(e => e.Office == "Tokyo").ToList();
        Assert.Equal(tokyo.Count, count);
        Assert.Equal(tokyo.Count == 0 ? 0 : Math.Round(tokyo.Average(e => e.Salary)), average);
    }

    [Fact]
    public void EmployeeDirectory_CsvQuotesCommasAndQuotes_AndExportPrefersTheSelection()
    {
        var e = new Employee(7, "Ann \"Nan\" Lee", "ann@x.dev", "Lead, Design", "Design", "Berlin", new DateTime(2020, 3, 1), 81500m, EmployeeStatus.OnLeave);

        var csv = EmployeeDirectory.Csv([e]);

        Assert.StartsWith("Id,Name,Email,Position,Department,Office,Start date,Salary,Status\n", csv);
        Assert.EndsWith("7,\"Ann \"\"Nan\"\" Lee\",ann@x.dev,\"Lead, Design\",Design,Berlin,2020-03-01,81500,On leave\n", csv);
        Assert.Equal([2, 5], EmployeeDirectory.ForExport(new EmployeeQuery(Department: "Nowhere"), [5, 2]).Select(x => x.Id).Order());
        Assert.Empty(EmployeeDirectory.ForExport(new EmployeeQuery(Department: "Nowhere"), []));
    }

    [Fact]
    public void EmployeeDirectory_DetailsShowTheTenure()
    {
        var e = EmployeeDirectory.All[0] with { StartDate = new DateTime(2024, 1, 15) };

        var details = EmployeeDirectory.Details(e, new DateTime(2026, 4, 20)).ToDictionary(d => d.Label, d => d.Value);

        Assert.EndsWith("· 2 yr 3 mo", details["Start date"]);
        Assert.Equal($"EMP-{e.Id:0000}", details["Employee ID"]);
    }

    [Fact]
    public void ModelValidation_GroupsErrorsByProperty()
    {
        var errors = ModelValidation.Validate(new WizardInput { Username = "ab", Password = "12345678", ConfirmPassword = "x" });

        Assert.Equal(["Use 3 to 20 characters"], errors[nameof(WizardInput.Username)]);
        Assert.Equal(["The passwords don't match"], errors[nameof(WizardInput.ConfirmPassword)]);
        Assert.Equal(["Enter an email address"], errors[nameof(WizardInput.Email)]);
        Assert.False(errors.ContainsKey(nameof(WizardInput.Password)));
        Assert.False(errors.ContainsKey(nameof(WizardInput.Language))); // defaults to English
    }

    [Fact]
    public void WizardInput_ValidatesOnlyTheFieldsOfTheStep()
    {
        var input = new WizardInput { Twitter = "not a url" };

        var account = input.Validate(0);
        Assert.Equal([nameof(WizardInput.ConfirmPassword), nameof(WizardInput.Email), nameof(WizardInput.Password), nameof(WizardInput.Username)], account.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([nameof(WizardInput.Twitter)], input.Validate(2).Keys);
        Assert.Equal([nameof(WizardInput.AcceptTerms)], input.Validate(3).Keys);
        Assert.Equal(input.Validate(3).Keys, input.Validate(99).Keys); // clamped to the last step

        input.AcceptTerms = true;
        Assert.Empty(input.Validate(3));
        Assert.Equal(WizardInput.Steps.Length, WizardInput.StepFields.Length);
    }

    [Fact]
    public void Icons_SearchesPagesAndKeepsTheThemeInTheQuery()
    {
        var all = UiContent.Icons(null, null, 1);
        Assert.Equal("outline", all.Theme.Key);
        Assert.Equal(AntIconSvg.Names().Count, all.Total);
        Assert.Equal(UiContent.IconPageSize, all.Names.Count);
        Assert.Equal("", all.QueryString());

        var arrows = UiContent.Icons("ARROW", "fill", 99);
        Assert.Equal("fill", arrows.Theme.Key);
        Assert.Equal(arrows.Pages, arrows.Page); // clamped
        Assert.All(arrows.Names, n => Assert.Contains("arrow", n));
        Assert.Equal("q=ARROW&theme=fill", arrows.QueryString());
        Assert.Equal("q=ARROW", arrows.QueryString("outline"));
        Assert.Equal("<Icon Type=\"x\" Theme=\"IconThemeType.Fill\" />", arrows.BlazorSnippet("x"));
        Assert.Equal("<app-icon type=\"x\" theme=\"Fill\" />", arrows.MvcSnippet("x"));
        Assert.Equal("<app-icon type=\"x\" />", all.MvcSnippet("x"));

        Assert.Equal(0, UiContent.Icons("no-such-icon-anywhere", null, 1).Total);
    }

    [Fact]
    public void Palette_HasTenShadesPerPreset_WithTheSeedAsShadeSix()
    {
        Assert.Equal(12, UiContent.Palette.Count);
        Assert.All(UiContent.Palette, c =>
        {
            Assert.Equal(10, c.Shades.Count);
            Assert.Equal(c.Seed, c.Shades[5].Hex);
            Assert.Equal(6, c.Shades[5].Index);
        });
        var blue = UiContent.Palette[0];
        Assert.True(blue.Shades[0].DarkText);  // the lightest shade takes dark text
        Assert.False(blue.Shades[9].DarkText); // the darkest takes white
    }

    [Fact]
    public void Changelog_IsNewestFirst()
    {
        Assert.Equal(UiContent.Changelog.OrderByDescending(r => r.Date), UiContent.Changelog);
        Assert.All(UiContent.Changelog, r => Assert.NotEmpty(r.Changes));
    }

    [Fact]
    public void UserGrid_FiltersByRole_SearchesAndPages()
    {
        var store = new SampleStore();
        var users = store.Users();

        var all = UserGridPage.Load(store, null, null, 1);
        Assert.Equal(users.Count, all.Total);
        Assert.Equal(Math.Min(UserGridPage.Size, users.Count), all.Users.Count);
        Assert.Equal(users.Count, all.Roles.Sum(r => r.Count));
        Assert.Equal(all.Users.OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).Select(u => u.Id), all.Users.Select(u => u.Id));

        var admins = UserGridPage.Load(store, null, "admin", 1);
        Assert.Equal("Admin", admins.Role);
        Assert.All(admins.Users, u => Assert.Equal("Admin", u.Role));
        Assert.Equal("role=Admin", admins.Filters);

        var someone = users[0];
        var found = UserGridPage.Load(store, someone.Email, "no such role", 5);
        Assert.Null(found.Role);
        Assert.Equal(1, found.Page);
        Assert.Contains(found.Users, u => u.Id == someone.Id);
        Assert.Equal("q=" + Uri.EscapeDataString(someone.Email) + "&role=Editor", found.QueryString("Editor"));
    }

    [Fact]
    public void SampleAuth_SignsInActiveUsersWithTheDemoPassword()
    {
        var store = new SampleStore();

        var user = SampleAuth.Check(store, " SERENA.PARK@modulus.dev ", SampleAuth.DemoPassword);
        Assert.Equal(CurrentUser.Id, user!.Id);
        Assert.Null(SampleAuth.Check(store, CurrentUser.Email, "wrong"));
        Assert.Null(SampleAuth.Check(store, "nobody@modulus.dev", SampleAuth.DemoPassword));
        Assert.Null(SampleAuth.Check(store, null, SampleAuth.DemoPassword));
        if (store.Users().FirstOrDefault(u => u.Status != UserStatus.Active) is { } inactive)
            Assert.Null(SampleAuth.Check(store, inactive.Email, SampleAuth.DemoPassword));

        var principal = SampleAuth.Principal(user);
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal(CurrentUser.Name, principal.Identity.Name);
        Assert.True(principal.IsInRole("Admin"));
    }

    [Theory]
    [InlineData("/apps/users/list?role=Admin", "/apps/users/list?role=Admin")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData(@"/\evil.example/", "/")]
    [InlineData("apps/users", "/")]
    public void SampleAuth_ReturnsOnlyToThisSite(string? returnUrl, string expected) =>
        Assert.Equal(expected, SampleAuth.SafeReturnUrl(returnUrl));
}
