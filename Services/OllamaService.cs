using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SmartArchive.DTOs;

namespace SmartArchive.Services
{
    // Service to call a local Ollama instance (llama3-vision) and extract fields
    public class OllamaService : IOllamaService
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOptions;

        public OllamaService(HttpClient httpClient)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task<ExtractionResponse?> ExtractTextFromImageAsync(string base64Image)
        {
            if (string.IsNullOrWhiteSpace(base64Image)) return null;

            try
            {
                // Payload schema depends on local model's expected input. Keep this straightforward and safe.
                var payload = new
                {
                    model = "llama3-vision",
                    input = new
                    {
                        // many local llama servers expect either `image` or `data` fields; adapt if needed.
                        image = base64Image
                    }
                };

                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Assumed endpoint for local Ollama. Adjust `BaseAddress` on HttpClient in Program.cs if different.
                using var response = await _http.PostAsync("/api/generate", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                // The local model output format may vary. Try to parse the most common shapes.
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // Try common flattened properties first
                if (root.TryGetProperty("nationalId", out var nidProp) || root.TryGetProperty("national_id", out nidProp))
                {
                    var nid = nidProp.GetString() ?? string.Empty;
                    string fullName = string.Empty;
                    if (root.TryGetProperty("fullName", out var fnProp) || root.TryGetProperty("full_name", out fnProp))
                        fullName = fnProp.GetString() ?? string.Empty;

                    return new ExtractionResponse(nid, fullName);
                }

                // Fallback: search text blocks for patterns (very conservative)
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            var v = prop.Value.GetString() ?? string.Empty;
                            if (v.Contains("NationalId", StringComparison.OrdinalIgnoreCase) || v.Length >= 6)
                            {
                                // best-effort - this is speculative. Prefer model to return structured json.
                                return new ExtractionResponse(v, string.Empty);
                            }
                        }
                    }
                }

                return null;
            }
            catch (HttpRequestException)
            {
                // bubble up as null for caller to handle
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
