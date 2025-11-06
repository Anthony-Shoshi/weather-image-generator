using System;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class Start
{
    private readonly ILogger _logger;

    public Start(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Start>();
    }

    [Function("Start")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get")] HttpRequestData req)
    {
        var processId = Guid.NewGuid().ToString();
        var messageBody = JsonSerializer.Serialize(new { processId });

        // Get connection string and queue name from environment
        var storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        var queueName = Environment.GetEnvironmentVariable("START_QUEUE_NAME") ?? "start-queue";

        var queueClient = new QueueClient(storageConnection, queueName);
        await queueClient.CreateIfNotExistsAsync();
        await queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(messageBody)));

        _logger.LogInformation($"Queued new process {processId}");

        var response = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
        await response.WriteStringAsync(JsonSerializer.Serialize(new { processId, status = "queued" }));
        return response;
    }
}
