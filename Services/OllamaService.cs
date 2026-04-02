using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartArchive.DTOs;

namespace SmartArchive.Services
{
    // Service to call a local Ollama instance (llama3-vision) and extract fields
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<OllamaService> _log;
        private readonly string _model;

        public OllamaService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaService> log)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _log = log;
            _model = configuration["OllamaModel"] ?? "llava:latest";
        }

        public async Task<(ExtractionResponse? Extraction, string? Error)> ExtractTextFromImageAsync(string base64Image)
        {
            if (string.IsNullOrWhiteSpace(base64Image))
                return (null, "Image payload is empty");

            try
            {
                const string prompt = "Extract National ID and full name from this ID image. Return ONLY valid JSON with exactly: {\"nationalId\":\"...\",\"fullName\":\"...\"}. If not readable, return empty strings.";

                var payload = new
                {
                    model = _model,
                    prompt,
                    images = new[] { base64Image },
                    stream = false,
                    format = "json"
                };

                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _http.PostAsync("/api/generate", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("Ollama returned {StatusCode}. Body: {Body}", (int)response.StatusCode, responseJson);
                    return (null, $"Ollama call failed with status {(int)response.StatusCode}. Ensure model '{_model}' exists and Ollama is running.");
                }

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("response", out var modelResponseProp) || modelResponseProp.ValueKind != JsonValueKind.String)
                    return (null, "Ollama returned an unexpected response shape");

                var modelResponseText = modelResponseProp.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(modelResponseText))
                    return (null, "Ollama returned empty text");

                var jsonText = ExtractJsonObject(modelResponseText);
                if (string.IsNullOrWhiteSpace(jsonText))
                {
                    _log.LogInformation("Model response did not contain JSON object. Raw: {Raw}", modelResponseText);
                    return (null, "Model output was not valid JSON");
                }

                using var extractedDoc = JsonDocument.Parse(jsonText);
                var extracted = extractedDoc.RootElement;

                var nationalId = GetString(extracted, "nationalId", "national_id", "id", "nationalid");
                var fullName = GetString(extracted, "fullName", "full_name", "name");

                if (string.IsNullOrWhiteSpace(nationalId) && string.IsNullOrWhiteSpace(fullName))
                {
                    _log.LogInformation("Parsed JSON but fields were empty. JSON: {Json}", jsonText);
                    return (null, "Could not extract readable National ID or full name from the image");
                }

                return (new ExtractionResponse(nationalId ?? string.Empty, fullName ?? string.Empty), null);
            }
            catch (HttpRequestException ex)
            {
                _log.LogError(ex, "Failed to connect to Ollama at {BaseAddress}", _http.BaseAddress);
                return (null, "Cannot connect to Ollama. Ensure Ollama is running on the configured URL.");
            }
            catch (JsonException ex)
            {
                _log.LogError(ex, "Failed to parse Ollama response");
                return (null, "Failed to parse model response");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unexpected error during OCR extraction");
                return (null, "Unexpected error during extraction");
            }
        }

        private static string? GetString(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetPropertyIgnoreCase(element, name, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString();
                }
            }

            return null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
        {
            foreach (var p in element.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static string? ExtractJsonObject(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                return trimmed;

            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed.Substring(start, end - start + 1);

            return null;
            }
    }
}
