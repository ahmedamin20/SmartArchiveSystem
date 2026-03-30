using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SmartArchive.DTOs;

namespace SmartArchive.Services
{
    public interface IArchiveService
    {
        Task<(ExtractionResponse? Extraction, string? TempFilePath, string? Error)> AnalyzeAsync(IFormFile file);
        Task<(bool Success, string? Error)> ConfirmAsync(ConfirmUploadRequest request);
    }
}
