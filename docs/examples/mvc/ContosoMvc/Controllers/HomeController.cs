using ContosoMvc.Data;
using ContosoMvc.Models;
using Microsoft.AspNetCore.Mvc;

namespace ContosoMvc.Controllers;

public class HomeController(CustomerStore store) : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Dashboard";
        return View(new DashboardStats(store.Count, store.ActiveCount, store.Revenue));
    }

    public IActionResult Error()
    {
        ViewData["Title"] = "Error";
        return View();
    }
}
