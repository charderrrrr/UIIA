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
                return BadRequest("Request body is required.");
            }

            var result = await _orchestrator.ExecuteTestAsync(request);
            return Json(new { status = "completed", reportPath = result });
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