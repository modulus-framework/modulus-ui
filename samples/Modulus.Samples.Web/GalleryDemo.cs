using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web;

/// <summary>Server-driven feedback for the UI pages (POST /ui/components/demo/{kind}, /ui/cards/demo/{kind}), shared by MVC and Razor Pages.</summary>
public static class GalleryDemo
{
    public static void Respond(HttpRequest request, string kind)
    {
        var response = request.HttpContext.Response;
        switch (kind)
        {
            case "notification":
                response.HxNotify(MessageType.Success, "Export ready", "customers-2026-09.csv (1,204 rows) is ready to download.");
                break;
            case "invite":
                response.HxMessage(MessageType.Success, "Invites sent").HxCloseModal();
                break;
            case "delete":
                response.HxMessage(MessageType.Success, "Invoice deleted");
                break;
            case "alerts":
                response.HxMessage(MessageType.Info, request.Form["Alerts"] == "true" ? "Email alerts on" : "Email alerts off");
                break;
            case "approve":
                response.HxMessage(MessageType.Success, "Refund approved");
                break;
            case "decline":
                response.HxMessage(MessageType.Warning, "Refund declined");
                break;
            case "report":
                response.HxMessage(MessageType.Success, "Report scheduled for Mondays");
                break;
            default:
                response.HxMessage(MessageType.Success, "Changes saved");
                break;
        }
    }
}
