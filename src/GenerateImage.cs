// using System;
// using Azure.Storage.Queues.Models;
// using Microsoft.Azure.Functions.Worker;
// using Microsoft.Extensions.Logging;
// using System.IO;
// using System.Text.Json;
// using System.Threading.Tasks;
// using Azure.Storage.Blobs;

// namespace WeatherImageGenerator;

// public class GenerateImage
// {
//     private readonly ILogger _logger;

//     public GenerateImage(ILoggerFactory loggerFactory)
//     {
//         _logger = loggerFactory.CreateLogger<GenerateImage>();
//     }

//     [Function(nameof(GenerateImage))]
//     public async Task Run([QueueTrigger("start-queue", Connection = "AzureWebJobsStorage")] string queueItem)
//     {
//         try
//         {
//             var message = JsonSerializer.Deserialize<MessageModel>(queueItem);
//             var processId = message?.processId ?? Guid.NewGuid().ToString();
//             _logger.LogInformation($"Generating image for process: {processId}");

//             // Simulate image generation
//             await Task.Delay(2000);

//             // Create simple text as "generated image"
//             var blobService = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
//             var containerClient = blobService.GetBlobContainerClient("generated-images");
//             await containerClient.CreateIfNotExistsAsync();

//             var blobClient = containerClient.GetBlobClient($"{processId}.txt");
//             await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"Image for process {processId}"));
//             await blobClient.UploadAsync(stream, overwrite: true);

//             _logger.LogInformation($"Image stored as blob: {processId}.txt");
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError($"Error processing queue message: {ex.Message}");
//         }
//     }

//     private class MessageModel
//     {
//         public string? processId { get; set; }
//     }
// }