using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using pemdas.Models;
using pemdas.Services;

namespace pemdas.Controllers;

public class HomeController : Controller
{
    private readonly PemdasSolver _solver;

    public HomeController(PemdasSolver solver)
    {
        _solver = solver;
    }

    public IActionResult Index()
    {
        return View(new PemdasPageViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(PemdasPageViewModel model)
    {
        var result = _solver.Solve(model.Expression);
        return View(result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
