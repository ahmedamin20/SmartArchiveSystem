using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartArchive.Data;
using SmartArchive.DTOs;
using SmartArchive.Models;

namespace SmartArchive.Services
{
    public class ArchiveService : IArchiveService
    {
        private readonly AppDbContext _db;
        private readonly IOllamaService _ollama;
        private readonly string _storageRoot;
        private readonly ILogger<ArchiveService> _log;

        public ArchiveService(AppDbContext db, IOllamaService ollama, IConfiguration config, ILogger<ArchiveService> log)
        {
            _db = db;
            _ollama = ollama;
            _log = log;
            _storageRoot = config["StorageRoot"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Storage");

            try
            {
                if (!Directory.Exists(_storageRoot))
                    Directory.CreateDirectory(_storageRoot);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to ensure storage root exists");
                throw;
            }
        }

        public async Task<(ExtractionResponse? Extraction, string? TempFilePath, string? Error)> AnalyzeAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (null, null, "File is empty");

            var tempFileName = Path.Combine(Path.GetTempPath(), $"smartarchive_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}");

            try
            {
                await using var fs = new FileStream(tempFileName, FileMode.Create);
                await file.CopyToAsync(fs);

                // convert to base64
                fs.Position = 0;
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms);
                var base64 = Convert.ToBase64String(ms.ToArray());

                var extraction = await _ollama.ExtractTextFromImageAsync(base64);
                return (extraction, tempFileName, null);
            }
            catch (IOException ioEx)
            {
                _log.LogError(ioEx, "IO failure during Analyze");
                return (null, null, "IO error while processing the file");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unexpected error during Analyze");
                return (null, null, "Unexpected error during analysis");
            }
        }

        public async Task<(bool Success, string? Error)> ConfirmAsync(ConfirmUploadRequest request)
        {
            if (request == null) return (false, "Request is null");
            if (string.IsNullOrWhiteSpace(request.TempFilePath) || !File.Exists(request.TempFilePath))
                return (false, "Temp file not found");

            try
            {
                // Normalize national id
                var nid = request.NationalId.Trim();
                var person = await _db.Persons.FirstOrDefaultAsync(p => p.NationalId == nid);
                string personFolder;

                if (person != null)
                {
                    personFolder = person.FolderPath;
                    if (!Directory.Exists(personFolder))
                        Directory.CreateDirectory(personFolder);
                }
                else
                {
                    // create folder using NationalId under storage root
                    personFolder = Path.Combine(_storageRoot, nid);
                    Directory.CreateDirectory(personFolder);

                    person = new Person
                    {
                        NationalId = nid,
                        FullName = request.FullName ?? string.Empty,
                        FolderPath = personFolder,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.Persons.Add(person);
                    await _db.SaveChangesAsync();
                }

                var destFileName = request.OriginalFileName ?? Path.GetFileName(request.TempFilePath);
                // avoid collisions
                var destPath = Path.Combine(personFolder, $"{DateTime.UtcNow:yyyyMMddHHmmss}_{destFileName}");

                File.Move(request.TempFilePath, destPath);

                return (true, null);
            }
            catch (IOException ioEx)
            {
                _log.LogError(ioEx, "IO failure during Confirm");
                return (false, "IO error while saving the file");
            }
            catch (DbUpdateException dbEx)
            {
                _log.LogError(dbEx, "Database failure during Confirm");
                return (false, "Database error while saving record");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unexpected error during Confirm");
                return (false, "Unexpected error while confirming upload");
            }
        }
    }
}
