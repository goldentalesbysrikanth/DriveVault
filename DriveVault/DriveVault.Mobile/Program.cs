using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;

static async Task Main()
{
    Console.WriteLine("DriveVault Mobile Client Prototype");
    if (args.Length == 0)
    {
        Console.WriteLine("Usage: DriveVault.Mobile <backend-url>");
        return;
    }
    var backend = args[0].TrimEnd('/');
    using var client = new HttpClient { BaseAddress = new Uri(backend) };
    try
    {
        var drives = await client.GetFromJsonAsync<List<Drive>>("/api/drives");
        Console.WriteLine($"Found {drives?.Count ?? 0} drives");
        if (drives != null)
        {
            foreach (var d in drives)
            {
                Console.WriteLine($"{d.Label} ({d.MountPath}) - {d.TotalBytes} bytes");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error contacting backend: {ex.Message}");
    }
}

public record Drive(
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("Label")] string Label,
    [property: JsonPropertyName("MountPath")] string MountPath,
    [property: JsonPropertyName("TotalBytes")] long TotalBytes,
    [property: JsonPropertyName("UsedBytes")] long UsedBytes,
    [property: JsonPropertyName("DriveType")] string DriveType,
    [property: JsonPropertyName("SerialNumber")] string SerialNumber,
    [property: JsonPropertyName("IsConnected")] bool IsConnected,
    [property: JsonPropertyName("HealthStatus")] string HealthStatus,
    [property: JsonPropertyName("HealthScore")] int HealthScore,
    [property: JsonPropertyName("IsFullyIndexed")] bool IsFullyIndexed,
    [property: JsonPropertyName("LastSeen")] DateTime LastSeen
);
