using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Pages;

public class HelpArticleModel : SamplePageModel
{
    public HelpCategory Category { get; private set; } = default!;
    public HelpArticle Article { get; private set; } = default!;

    public IActionResult OnGet(string slug)
    {
        if (SampleContent.FindArticle(slug, out var category) is not { } article) return NotFound();
        (Category, Article) = (category!, article);
        return Page();
    }
}
