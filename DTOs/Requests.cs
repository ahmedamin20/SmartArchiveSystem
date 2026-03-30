using System;
using Microsoft.AspNetCore.Http;

namespace SmartArchive.DTOs
{
    public record ExtractionResponse(string NationalId, string FullName);

    public class ConfirmUploadRequest
    {
        public required string NationalId { get; set; }
        public required string FullName { get; set; }
        // Path to temp file produced by /analyze step
        public required string TempFilePath { get; set; }
        // Optional original filename to preserve extension
        public string? OriginalFileName { get; set; }
    }
}
