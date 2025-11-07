using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Web;

namespace WeatherImageGenerator;

public class GetResults
{
    private readonly ILogger<GetResults> _logger;

    public GetResults(ILogger<GetResults> logger)
    {
        _logger = logger;
    }

    [Function("GetResults")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var processId = query["processId"];

        var response = req.CreateResponse();

        if (string.IsNullOrEmpty(processId))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Missing processId query parameter.");
            return response;
        }

        var blobService = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        var container = blobService.GetBlobContainerClient("generated-images");

        var results = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(prefix: $"{processId}/"))
        {
            // public URL
            // var uri = $"{container.Uri}/{blob.Name}";
            // results.Add(uri);

            // SAS URL
            var blobClient = container.GetBlobClient(blob.Name);
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = container.Name,
                BlobName = blob.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            results.Add(sasUri.ToString());
        }

        response.StatusCode = HttpStatusCode.OK;
        await response.WriteStringAsync(JsonSerializer.Serialize(new { processId, images = results }));

        _logger.LogInformation($"Returned {results.Count} images for process {processId}");
        return response;
    }
}