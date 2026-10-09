using Microsoft.AspNetCore.Http;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Import;

namespace Modulus.Samples.Web;

/// <summary>
/// The import wizard's server side, behind MVC <c>TablesController</c> and the Razor Pages <c>Tables/Import</c> page. Each
/// step posts the file's text (<c>csv</c>), the mapping (<c>m.{key}</c>) and a <c>step</c> button; the import itself runs
/// here, once the checked file is posted with <c>step=import</c>.
/// </summary>
public static class ImportPageActions
{
    public static async Task<ImportWizardView> RespondAsync(HttpRequest request, CustomerImportStore store)
    {
        var fields = CustomerImport.Fields;
        if (!request.HasFormContentType) return ImportWizardView.Start(fields);
        var form = request.Form;

        switch (form["step"].ToString())
        {
            case "upload":
                var file = form.Files["file"];
                if (file is null || file.Length == 0) return ImportFlow.Load(null, fields);
                if (file.Length > ImportLimits.MaxBytes) return ImportFlow.Refuse(ImportFlow.TooLarge, fields);
                using (var reader = new StreamReader(file.OpenReadStream()))
                    return ImportFlow.Load(await reader.ReadToEndAsync(), fields);

            case "map":
            case "preview":
            case "import":
                var csv = form["csv"].ToString();
                if (csv.Length == 0) return ImportWizardView.Start(fields);
                var table = CsvReader.Parse(csv);
                var mapping = ImportMapping.Read(fields, key => form[key].ToString() is { Length: > 0 } v ? v : null, table.Headers.Count);
                if (form["step"] == "map") return new ImportWizardView(ImportStep.Map, fields, csv, table, mapping);

                var check = ImportFlow.Check(csv, mapping, fields);
                if (form["step"] != "import") return check;
                if (!check.Preview!.CanCommit) return check with { Error = "Fix the rows with problems, then upload the file again." };
                var total = store.Add(check.Preview.Rows);
                return new ImportWizardView(ImportStep.Done, fields, Message: CustomerImport.Saved(check.Preview.Rows.Count, total));

            default:
                return ImportWizardView.Start(fields);
        }
    }
}
