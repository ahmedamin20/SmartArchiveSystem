using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartArchive.DTOs;
using SmartArchive.Services;

namespace SmartArchive.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArchiveController : ControllerBase
    {
        private readonly IArchiveService _archive;
        private readonly ILogger<ArchiveController> _log;

        public ArchiveController(IArchiveService archive, ILogger<ArchiveController> log)
        {
            _archive = archive;
            _log = log;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromForm] Microsoft.AspNetCore.Http.IFormFile file)
        {
            try
            {
                var (extraction, tempPath, error) = await _archive.AnalyzeAsync(file);
                if (!string.IsNullOrEmpty(error))
                    return BadRequest(new { error });

                if (extraction == null)
                    return StatusCode(502, new { error = "Failed to extract data from image" });

                // return extracted data plus temp path for confirmation step
                return Ok(new { extraction.NationalId, extraction.FullName, TempFilePath = tempPath });
            }
            catch (System.Exception ex)
            {
                _log.LogError(ex, "Analyze endpoint failed");
                return Problem("Internal server error during analyze");
            }
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> Confirm([FromBody] ConfirmUploadRequest request)
        {
            try
            {
                var (success, error) = await _archive.ConfirmAsync(request);
                if (!success)
                    return BadRequest(new { error });

                return Ok(new { message = "File saved" });
            }
            catch (System.Exception ex)
            {
                _log.LogError(ex, "Confirm endpoint failed");
                return Problem("Internal server error during confirm");
            }
        }
    }
}
