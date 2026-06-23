using System;
using System.IO;
using System.Threading.Tasks;
using UIIA.Models;
using UIIA.Services;
using Microsoft.AspNetCore.Mvc;

namespace UIIA.Controllers
{
    public class HomeController : Controller
    {
        private readonly TestOrchestrator _orchestrator;

        public HomeController(TestOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> StartTest([FromBody] TestRunRequest request)
        {
            if (request == null)
            {
                return BadRequest("Тело запроса является обязательным.");
            }

            try
            {
                var result = await _orchestrator.ExecuteTestAsync(request);
                return Json(new { status = "completed", reportPath = result });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("уже выполняется"))
            {
                return Conflict(new { title = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { title = ex.Message });
            }
            catch (CsvHelper.HeaderValidationException ex)
            {
                var expected = "time_ms, method, endpoint, body_file";
                var actual = string.Join(", ", ex.Message.Split("Headers: ")[1].Split('\n')[0]);
                return BadRequest(new
                {
                    title = $"Неверный формат CSV-файла. Ожидаются колонки: {expected}. В файле: {actual}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { title = $"Внутренняя ошибка: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult DownloadReport(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return NotFound();
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/'));
            
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var bytes = System.IO.File.ReadAllBytes(fullPath);
            return File(bytes, "text/html", "report.html");
        }
    }
}