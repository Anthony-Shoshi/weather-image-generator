using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Azure.Storage.Blobs;

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
            req.Headers.Add("Authorization", $"Client-ID {accessKey}");

            var resp = await _httpClient.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var imageJson = await resp.Content.ReadAsStringAsync();

            using var imageDoc = JsonDocument.Parse(imageJson);
            
            if (!imageDoc.RootElement.TryGetProperty("urls", out var urls))
            {
                _logger.LogError("Unsplash response missing 'urls'");
                return;
            }

            var imageUrl = urls.GetProperty("regular").GetString();
            _logger.LogInformation($"Downloading image from {imageUrl}");

            // Download image bytes
            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);

            // Upload to Blob Storage
            var blobService = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var container = blobService.GetBlobContainerClient("generated-images");
            await container.CreateIfNotExistsAsync();

            var blobName = $"{processId}/{stationName.Replace(" ", "_")}.jpg";
            var blobClient = container.GetBlobClient(blobName);

            await using var stream = new MemoryStream(imageBytes);
            await blobClient.UploadAsync(stream, overwrite: true);

            _logger.LogInformation($"Uploaded {blobName} to 'generated-images' container.");
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