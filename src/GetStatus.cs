using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Web;
using WeatherImageGenerator.Helpers;

namespace WeatherImageGenerator;

public class GetStatus
{
    private readonly ILogger<GetStatus> _logger;

    public GetStatus(ILogger<GetStatus> logger)
    {
        _logger = logger;
    }

    [Function("GetStatus")]
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

        var entity = await StatusHelper.GetStatusAsync(processId);
        if (entity == null)
        {
            response.StatusCode = HttpStatusCode.NotFound;
            await response.WriteStringAsync($"No status found for {processId}");
            return response;
        }

        var status = new
        {
            processId,
            status = entity.GetString("Status"),
            completed = entity.GetInt32("Completed"),
            total = entity.GetInt32("Total"),
            lastUpdated = entity.GetDateTime("LastUpdated")
        };

        response.StatusCode = HttpStatusCode.OK;
        await response.WriteStringAsync(JsonSerializer.Serialize(status));
        return response;
    }
}