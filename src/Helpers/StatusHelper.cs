using System;
using Azure;
using Azure.Data.Tables;
using System.Threading.Tasks;


namespace WeatherImageGenerator.Helpers;

public static class StatusHelper
{
    private static TableClient GetTableClient()
    {
        var conn = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        var service = new TableServiceClient(conn);
        var table = service.GetTableClient("ProcessStatus");
        table.CreateIfNotExists();
        return table;
    }

    public static async Task UpdateStatusAsync(string processId, string status, int completed = 0, int total = 0)
    {
        var table = GetTableClient();
        var entity = new TableEntity("Process", processId)
        {
            { "Status", status },
            { "Completed", completed },
            { "Total", total },
            { "LastUpdated", DateTime.UtcNow }
        };
        await table.UpsertEntityAsync(entity);
    }

    public static async Task<TableEntity?> GetStatusAsync(string processId)
    {
        var table = GetTableClient();
        try
        {
            return await table.GetEntityAsync<TableEntity>("Process", processId);
        }
        catch
        {
            return null;
        }
    }
}
