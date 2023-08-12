using Ensync.CLI.Abstract;
using Ensync.Core;
using System.Text.Json;

namespace Ensync.CLI;

internal class CommandContext : CommandContextBase
{
    public CommandContext(string[] args) : base(args)
    {
    }

    public string ConfigPath { get; private set; } = default!;
    public string DbTarget { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public Configuration Configuration { get; private set; } = default!;
	public string BasePath { get; private set; } = default!;
	public Dictionary<string, Configuration.Target> Targets { get; private set; } = new();

	protected override void Initialize()
	{
		ConfigPath = ParseArgument(0, ".");		
		var config = FindConfig(ConfigPath);
		Configuration = config.Data;
		BasePath = config.BasePath;
		Targets = Configuration.DatabaseTargets.ToDictionary(item => item.Name);
		DbTarget = ParseArgument(1, Targets.FirstOrDefault().Key ?? throw new Exception("Expected at least one database target"));
		Action = ParseArgument(2, "script");
	}

	private static (Configuration Data, string BasePath) FindConfig(string configPath)
	{
		var startPath = configPath;

		var path = Path.GetFullPath(startPath);

		const string ensyncConfig = "ensync.config.json";

		var configFile = Path.Combine(path, ensyncConfig);
		do
		{
			if (File.Exists(configFile))
			{
				var json = File.ReadAllText(configFile);
				return (JsonSerializer.Deserialize<Configuration>(json) ?? throw new Exception("Couldn't read json"), path);
			}

			path = Directory.GetParent(path)?.FullName ?? throw new Exception($"Couldn't get directory parent of {path}");
			configFile = Path.Combine(path, ensyncConfig);
		} while (true);
	}
}
