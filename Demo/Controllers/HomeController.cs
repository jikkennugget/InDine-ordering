using Microsoft.AspNetCore.Mvc;

namespace Demo.Controllers;

public class HomeController : Controller
{
    private readonly DB db;

    public HomeController(DB db)
    {
        this.db = db;
    }

    // GET: Home/Index (redirect to appropriate page)
    public IActionResult Index()
    {
        if (!User.Identity!.IsAuthenticated)
            return RedirectToAction("Login", "Account");

        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Maintain", "Product");
        }
        else if (User.IsInRole("Staff"))
        {
            return RedirectToAction("TakeOrder", "Product");
        }

        return RedirectToAction("Login", "Account");
    }
}
