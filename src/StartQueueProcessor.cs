using System;
using Azure.Storage.Queues.Models;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace WeatherImageGenerator;

public class StartQueueProcessor
{
    private readonly ILogger _logger;
    private static readonly HttpClient _http = new HttpClient();

    public StartQueueProcessor(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<StartQueueProcessor>();
    }
    [Function(nameof(StartQueueProcessor))]
    public async Task Run([QueueTrigger("start-queue", Connection = "AzureWebJobsStorage")] string queueMessage, FunctionContext context)
    {
        try
        {
            string decoded;
            try
            {
                var bytes = Convert.FromBase64String(queueMessage);
                decoded = Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                decoded = queueMessage;
            }

            using var root = JsonDocument.Parse(decoded);
            var processId = root.RootElement.GetProperty("processId").GetString() ?? Guid.NewGuid().ToString();
            _logger.LogInformation($"StartQueueProcessor: processing start for {processId}");

            // Fetch Buienradar feed
            var feedUrl = Environment.GetEnvironmentVariable("BUVIENRADAR_FEED")
                          ?? "https://data.buienradar.nl/2.0/feed/json";
            _logger.LogInformation($"Fetching feed from {feedUrl}");

            using var resp = await _http.GetAsync(feedUrl);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            // Navigate to stationmeasurements: actual.stationmeasurements (safe checks)
            JsonElement stationsElem;
            if (doc.RootElement.TryGetProperty("actual", out var actual) &&
                actual.TryGetProperty("stationmeasurements", out stationsElem) &&
                stationsElem.ValueKind == JsonValueKind.Array)
            {
                // ok
            }
            else if (doc.RootElement.TryGetProperty("stationmeasurements", out stationsElem) &&
                     stationsElem.ValueKind == JsonValueKind.Array)
            {
                // alternate path
            }
            else
            {
                _logger.LogWarning("Buienradar feed did not contain stationmeasurements array");
                return;
            }

            // Prepare queue client for image-queue
            var storageConn = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var imageQueueName = Environment.GetEnvironmentVariable("IMAGE_QUEUE_NAME") ?? "image-queue";
            var imageQueue = new QueueClient(storageConn, imageQueueName);
            await imageQueue.CreateIfNotExistsAsync();

            // Fan out up to 50 stations
            int enqueued = 0;
            foreach (var station in stationsElem.EnumerateArray().Take(50))
            {
                // create job payload with processId and station JSON
                var stationJson = station.GetRawText();
                var jobObj = new { processId, station = JsonDocument.Parse(stationJson).RootElement };
                // serialize job (we'll serialize station as raw JSON by using JsonDocument again)
                // build string manually to avoid double-escaping station
                var jobPayload = $"{{\"processId\":\"{processId}\",\"station\":{stationJson}}}";
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jobPayload));
                await imageQueue.SendMessageAsync(base64);
                enqueued++;
            }
            
            _logger.LogInformation($"Enqueued {enqueued} image jobs for process {processId} into queue '{imageQueueName}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"StartQueueProcessor failed: {ex}");
        }
    }
}