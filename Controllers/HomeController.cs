using KalkulatorFOweb.Data;
using KalkulatorFOweb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Globalization;

namespace KalkulatorFOweb.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public HomeController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var model = new CalculatorViewModel();

        if (User.Identity.IsAuthenticated)
        {
            var userId = _userManager.GetUserId(User);
            model.History = await _context.CalculationHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CreatedAt)
                .Take(10)
                .ToListAsync();
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Calculate(string expression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return Json(new { success = false, error = "Brak wyrażenia do obliczenia" });
            }

            string originalExpression = expression;

            double result = EvaluateExpression(expression);

            string resultString = result.ToString(CultureInfo.InvariantCulture);

            if (User.Identity.IsAuthenticated)
            {
                var userId = _userManager.GetUserId(User);
                var history = new CalculationHistory
                {
                    UserId = userId,
                    Expression = originalExpression,
                    Result = resultString
                };

                _context.CalculationHistories.Add(history);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, result = resultString });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Błąd w obliczeniu: " + ex.Message });
        }
    }

    private double EvaluateExpression(string expression)
    {
        expression = expression.Replace(" ", "");

        expression = expression.Replace("π", Math.PI.ToString(CultureInfo.InvariantCulture));
        expression = expression.Replace("e", Math.E.ToString(CultureInfo.InvariantCulture));

        expression = ProcessAllFunctions(expression);
        expression = ProcessPowerOperator(expression);

        var table = new DataTable();
        var result = table.Compute(expression, null);

        if (result == null || result == DBNull.Value)
            throw new InvalidOperationException("Nie można obliczyć wyrażenia");

        double finalValue = Convert.ToDouble(result);

        if (double.IsInfinity(finalValue) || double.IsNaN(finalValue))
            throw new DivideByZeroException("Dzielenie przez zero nie jest dozwolone");

        return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    private string ProcessAllFunctions(string expression)
    {
        var functions = new Dictionary<string, Func<double, double>>
        {
            ["sin"] = x => Math.Sin(x * Math.PI / 180),
            ["cos"] = x => Math.Cos(x * Math.PI / 180),
            ["tan"] = x => Math.Tan(x * Math.PI / 180),
            ["log"] = Math.Log10,
            ["ln"] = Math.Log,
            ["sqrt"] = Math.Sqrt
        };

        bool changed = true;
        int maxIterations = 20;
        int iteration = 0;

        while (changed && iteration < maxIterations)
        {
            changed = false;
            iteration++;

            foreach (var func in functions)
            {
                string pattern = func.Key + @"\(([^()]+)\)";
                var matches = Regex.Matches(expression, pattern);

                foreach (Match match in matches.Cast<Match>().Reverse())
                {
                    string innerExpr = match.Groups[1].Value;

                    try
                    {
                        double value;

                        if (double.TryParse(innerExpr, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                        {
                            double result = func.Value(value);
                            expression = expression.Substring(0, match.Index) +
                                         result.ToString(CultureInfo.InvariantCulture) +
                                         expression.Substring(match.Index + match.Length);
                            changed = true;
                        }
                        else
                        {
                            var table = new DataTable();
                            var computed = table.Compute(innerExpr, null);
                            if (computed != null && double.TryParse(computed.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                            {
                                double result = func.Value(value);
                                expression = expression.Substring(0, match.Index) +
                                             result.ToString(CultureInfo.InvariantCulture) +
                                             expression.Substring(match.Index + match.Length);
                                changed = true;
                            }
                        }
                    }
                    catch
                    {
                        // ignoruj błędne funkcje
                    }
                }
            }
        }

        return expression;
    }

    private string ProcessPowerOperator(string expression)
    {
        var powerPattern = @"(\d+(?:\.\d+)?)\^(\d+(?:\.\d+)?)";

        while (Regex.IsMatch(expression, powerPattern))
        {
            expression = Regex.Replace(expression, powerPattern, match =>
            {
                string baseValue = match.Groups[1].Value;
                string exponentValue = match.Groups[2].Value;

                if (double.TryParse(baseValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double baseResult) &&
                    double.TryParse(exponentValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double expResult))
                {
                    double result = Math.Pow(baseResult, expResult);
                    return result.ToString(CultureInfo.InvariantCulture);
                }

                return match.Value;
            });
        }

        return expression;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetHistory()
    {
        var userId = _userManager.GetUserId(User);
        var history = await _context.CalculationHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(20)
            .ToListAsync();

        return Json(history);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ClearHistory()
    {
        var userId = _userManager.GetUserId(User);
        var userHistory = await _context.CalculationHistories
            .Where(h => h.UserId == userId)
            .ToListAsync();

        _context.CalculationHistories.RemoveRange(userHistory);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }
     
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}