using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Azure.Storage.Blobs;
using WeatherImageGenerator.Helpers;
using Azure;
using Azure.Data.Tables;

namespace WeatherImageGenerator;

public class GenerateImage
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient = new();

    public GenerateImage(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GenerateImage>();
    }

    [Function(nameof(GenerateImage))]
    public async Task Run([QueueTrigger("image-queue", Connection = "AzureWebJobsStorage")] string queueItem)
    {
        try
        {
            _logger.LogInformation($"GenerateImage triggered with message: {queueItem}");

            string payload = queueItem;
            try
            {
                var bytes = Convert.FromBase64String(queueItem);
                var maybe = System.Text.Encoding.UTF8.GetString(bytes);
                if (!string.IsNullOrWhiteSpace(maybe) && (maybe.TrimStart().StartsWith('{') || maybe.TrimStart().StartsWith('[')))
                {
                    payload = maybe;
                }
            }
            catch
            {
                // not base64 -> ignore
            }

            // Parse JSON robustly and extract station element directly
            using var payloadDoc = JsonDocument.Parse(payload);
            var root = payloadDoc.RootElement;

            // extract processId (case-insensitive)
            string processId = Guid.NewGuid().ToString();
            if (TryGetPropertyIgnoreCase(root, "processId", out var pidElem) && pidElem.ValueKind == JsonValueKind.String)
            {
                processId = pidElem.GetString() ?? processId;
            }

            if (!TryGetPropertyIgnoreCase(root, "station", out var stationElem) || stationElem.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Invalid queue message: Station data is null");
                return;
            }

            // Determine station name with tolerant property name matching
            string stationName = "UnknownStation";
            if (TryGetPropertyIgnoreCase(stationElem, "stationName", out var sname) || TryGetPropertyIgnoreCase(stationElem, "stationname", out sname))
            {
                stationName = sname.GetString() ?? stationName;
            }

            _logger.LogInformation($"Processing station {stationName} for {processId}");

            // Fetch image from Unsplash
            var accessKey = Environment.GetEnvironmentVariable("UNSPLASH_ACCESS_KEY");
            if (string.IsNullOrEmpty(accessKey))
            {
                _logger.LogError("UNSPLASH_ACCESS_KEY not found in environment variables");
                return;
            }

            var requestUrl = "https://api.unsplash.com/photos/random?query=winter%20forest";
            var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            // Authorization as Client-ID is required for the Unsplash public API
            req.Headers.Add("Authorization", $"Client-ID {accessKey}");
            // Helpful headers for more informative responses from the API
            req.Headers.Add("Accept-Version", "v1");
            req.Headers.Add("User-Agent", "WeatherImageGenerator/1.0");

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                // Read body to get a clearer explanation (rate limit, invalid key, etc.)
                var errBody = await resp.Content.ReadAsStringAsync();
                _logger.LogError("Unsplash API returned {StatusCode}: {Body}", resp.StatusCode, errBody);
                return;
            }

            var imageJson = await resp.Content.ReadAsStringAsync();

            using var imageDoc = JsonDocument.Parse(imageJson);

            if (!imageDoc.RootElement.TryGetProperty("urls", out var urls))
            {
                _logger.LogError("Unsplash response missing 'urls'");
                return;
            }

            if (!urls.TryGetProperty("regular", out var regularUrlElem) || regularUrlElem.ValueKind != JsonValueKind.String)
            {
                _logger.LogError("Unsplash 'urls' missing 'regular' URL");
                return;
            }

            var imageUrl = regularUrlElem.GetString();
            _logger.LogInformation($"Downloading image from {imageUrl}");

            // Download image bytes defensively so we can log non-success responses (403, etc.)
            var imgReq = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            // Some hosts require a User-Agent; reuse the same.
            imgReq.Headers.Add("User-Agent", "WeatherImageGenerator/1.0");
            var imgResp = await _httpClient.SendAsync(imgReq);
            if (!imgResp.IsSuccessStatusCode)
            {
                var errBody = await imgResp.Content.ReadAsStringAsync();
                _logger.LogError("Failed to download image {Url} returned {StatusCode}: {Body}", imageUrl, imgResp.StatusCode, errBody);
                return;
            }

            var imageBytes = await imgResp.Content.ReadAsByteArrayAsync();
            using var imageStream = new MemoryStream(imageBytes);

            // Safely extract temperature (may be number or string) and weather description
            string temperatureText = "Temperature: N/A";
            if (TryGetPropertyIgnoreCase(stationElem, "temperature", out var tempElem))
            {
                if (tempElem.ValueKind == JsonValueKind.Number)
                {
                    temperatureText = $"Temperature: {tempElem.GetDouble():0.0}°C";
                }
                else if (tempElem.ValueKind == JsonValueKind.String && double.TryParse(tempElem.GetString(), out var tval))
                {
                    temperatureText = $"Temperature: {tval:0.0}°C";
                }
                else
                {
                    temperatureText = $"Temperature: {tempElem.ToString()}";
                }
            }

            string weatherText = "Weather: N/A";
            if (TryGetPropertyIgnoreCase(stationElem, "weatherdescription", out var weatherElem) || TryGetPropertyIgnoreCase(stationElem, "weatherDescription", out weatherElem))
            {
                if (weatherElem.ValueKind == JsonValueKind.String)
                {
                    weatherText = $"Weather: {weatherElem.GetString()}";
                }
                else
                {
                    weatherText = $"Weather: {weatherElem.ToString()}";
                }
            }

            var texts = new[]
            {
                ($"Station: {stationName}", (10f, 10f), 24, "#FFFFFF"),
                (temperatureText, (10f, 40f), 24, "#FFFFFF"),
                (weatherText, (10f, 70f), 24, "#FFFFFF")
            };

            using var editedImageStream = ImageHelper.AddTextToImage(imageStream, texts);

            // Upload to Blob Storage
            var blobService = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var container = blobService.GetBlobContainerClient("generated-images");
            await container.CreateIfNotExistsAsync();

            var blobName = $"{processId}/{stationName.Replace(" ", "_")}.png";
            var blobClient = container.GetBlobClient(blobName);

            await blobClient.UploadAsync(editedImageStream, overwrite: true);

            _logger.LogInformation($"Uploaded {blobName} to 'generated-images' container.");

            // <-- Add status increment code here
            var table = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"))
                .GetTableClient("ProcessStatus");
            await table.CreateIfNotExistsAsync();

            try
            {
                var entity = await table.GetEntityAsync<TableEntity>("Process", processId);
                int completed = entity.Value.GetInt32("Completed") ?? 0;
                int total = entity.Value.GetInt32("Total") ?? 50;
                entity.Value["Completed"] = completed + 1;
                entity.Value["Status"] = (completed + 1 == total) ? "completed" : "processing";
                entity.Value["LastUpdated"] = DateTime.UtcNow;
                await table.UpsertEntityAsync(entity.Value);
            }
            catch
            {
                await StatusHelper.UpdateStatusAsync(processId, "processing", 1, 50);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateImage");
        }
    }

    // Helper: try to get a property ignoring case
    private static bool TryGetPropertyIgnoreCase(JsonElement elem, string propName, out JsonElement value)
    {
        foreach (var p in elem.EnumerateObject())
        {
            if (string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    // Match actual JSON keys (camelCase) and allow mapping
    private class QueueMessage
    {
        public string? ProcessId { get; set; }
        public StationData? Station { get; set; }
    }

    private class StationData
    {
        public int StationId { get; set; }
        public string? StationName { get; set; }
        public double Temperature { get; set; }
        public string? WeatherDescription { get; set; }
        public string? IconUrl { get; set; }
    }
}