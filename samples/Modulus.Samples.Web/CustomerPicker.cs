using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;

namespace Modulus.Samples.Web;

/// <summary>
/// The gallery's customer picker (<c>app-creatable-select add-modal="customers"</c>): after its modal saves a
/// customer, every picker of that list adds the option and the one that opened the modal selects it.
/// </summary>
public static class CustomerPicker
{
    public const string List = "customers";
    public const string FormView = "~/Views/Shared/Ui/Ui/_CustomerPickerForm.cshtml";

    public static void Saved(HttpResponse response, Customer customer) =>
        response.HxOptionAdded(List, SampleStore.CustomerOption(customer))
            .HxCloseModal()
            .HxMessage(MessageType.Success, $"Added {customer.Name}");
}
