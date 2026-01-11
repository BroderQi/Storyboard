using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.IO;
using Microsoft.Extensions.Configuration;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// Reads/writes appsettings.json at runtime (keeps non-AIServices sections intact).
/// </summary>
public sealed class AppSettingsStore
{
    private readonly IConfiguration _configuration;

    public AppSettingsStore(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string SettingsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public AIServicesConfiguration LoadAIServices()
    {
        if (!File.Exists(SettingsFilePath))
            return new AIServicesConfiguration();

        var json = File.ReadAllText(SettingsFilePath);
        var node = JsonNode.Parse(json) as JsonObject;
        if (node == null)
            return new AIServicesConfiguration();

        var aiNode = node["AIServices"];
        if (aiNode == null)
            return new AIServicesConfiguration();

        return aiNode.Deserialize<AIServicesConfiguration>(JsonOptions) ?? new AIServicesConfiguration();
    }

    public void SaveAIServices(AIServicesConfiguration config)
    {
        JsonObject root;

        if (File.Exists(SettingsFilePath))
        {
            var json = File.ReadAllText(SettingsFilePath);
            root = (JsonNode.Parse(json) as JsonObject) ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root["AIServices"] = JsonSerializer.SerializeToNode(config, JsonOptions);

        File.WriteAllText(SettingsFilePath, root.ToJsonString(JsonOptions));

        if (_configuration is IConfigurationRoot cfgRoot)
        {
            cfgRoot.Reload();
        }
    }
}
