using System.Threading.Tasks;
using SmartArchive.DTOs;

namespace SmartArchive.Services
{
    public interface IOllamaService
    {
        Task<(ExtractionResponse? Extraction, string? Error)> ExtractTextFromImageAsync(string base64Image);
    }
}
