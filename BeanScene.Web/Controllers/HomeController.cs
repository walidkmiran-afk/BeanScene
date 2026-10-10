using System.Diagnostics;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

public class HomeController : Controller
{
    private readonly ISittingService _sittings;

    public HomeController(ISittingService sittings) => _sittings = sittings;

    public async Task<IActionResult> Index()
    {
        var upcoming = await _sittings.GetOpenUpcomingAsync();
        return View(upcoming.Take(6).ToList());
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new Models.ViewModels.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
