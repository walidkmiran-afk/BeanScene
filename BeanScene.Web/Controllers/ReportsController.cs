using BeanScene.Web.Models;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

/// <summary>Reports are restricted to the Manager.</summary>
[Authorize(Roles = RoleNames.Manager)]
public class ReportsController : Controller
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.Today).AddDays(-30);
        var end = to ?? DateOnly.FromDateTime(DateTime.Today).AddDays(60);
        if (end < start) (start, end) = (end, start);
        return View(await _reports.RunAsync(start, end));
    }
}
