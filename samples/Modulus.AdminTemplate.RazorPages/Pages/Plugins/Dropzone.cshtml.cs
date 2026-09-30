using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Plugins;

// Room for the largest accepted file (the check below still rejects anything over the limit).
[RequestSizeLimit(PluginsContent.MaxUploadMb * 1024 * 1024 + 64 * 1024)]
public class DropzoneModel : SamplePageModel
{
    public void OnGet() { }

    // The dropzone's upload target (/plugins/dropzone/upload, the same URL as the MVC sample): checks the file and throws it away.
    public IActionResult OnPostUpload(IFormFile? file) =>
        PluginsContent.CheckUpload(file?.FileName, file?.Length ?? 0) is { } error ? BadRequest(error) : new OkResult();
}
