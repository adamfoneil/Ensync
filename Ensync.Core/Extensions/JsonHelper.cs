using BlushingPenguin.JsonPath;
using System.Text.Json;

namespace Ensync.Core.Extensions;

public static class JsonHelper
{
    public static (bool Success, string? Name, string? ConnectionString) FindFirstConnectionString(string settingsJson)
    {
        var doc = JsonDocument.Parse(settingsJson);
        var connectionStringElement = doc.SelectTokens("ConnectionStrings")?.FirstOrDefault();

        if (connectionStringElement?.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in connectionStringElement.Value.EnumerateObject())
            {
                return (true, property.Name, property.Value.ToString());
            }
        }

        return (false, default, default);
    }
}
