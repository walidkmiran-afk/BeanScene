using BeanScene.Web.Models;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

/// <summary>Runs the ORM demonstration experiments (evidence for the ORM section of the assessment).</summary>
[Authorize(Roles = RoleNames.Manager)]
public class OrmDemoController : Controller
{
    private readonly OrmDemoService _demo;

    public OrmDemoController(OrmDemoService demo) => _demo = demo;

    [HttpGet]
    public IActionResult Index() => View(new List<Models.ViewModels.OrmDemoStep>());

    [HttpPost]
    public async Task<IActionResult> Index(bool run)
    {
        return View(await _demo.RunAllAsync());
    }
}
