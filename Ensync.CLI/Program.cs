using AO.ConnectionStrings;
using Ensync.Core;
using Ensync.Core.Extensions;
using Ensync.Dotnet7;
using Ensync.SqlServer;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Ensync.CLI;

internal class Program
{
	static async Task Main(string[] args)
	{
		var config = FindConfig(args);
		var targets = config.Data.DatabaseTargets.ToDictionary(item => item.Name);

		var fullPath = PathHelper.Resolve(config.BasePath, config.Data.AssemblyPath);
		Console.WriteLine(fullPath);
		
		var assemblyInspector = new AssemblySchemaInspector(fullPath);
		var assemblySchema = await assemblyInspector.GetSchemaAsync();

		var targetName = args.Length == 2 ? args[1] : config.Data.DatabaseTargets[0].Name;
		var target = targets[targetName];

		EnsureValidDbTarget(target.ConnectionString);

		var dbInspector = new SqlServerSchemaInspector(target.ConnectionString);
		var dbSchema = await dbInspector.GetSchemaAsync();

		var scriptBuilder = new SqlServerScriptBuilder(target.ConnectionString);
		var script = await assemblySchema.CompareAsync(dbSchema, scriptBuilder);

		var statements = script.ToSqlStatements(scriptBuilder, true).ToArray();
	}

	private static void EnsureValidDbTarget(string connectionString)
	{
		try
		{
			using var cn = new SqlConnection(connectionString);
			cn.Open();
		}
		catch 
		{
			if (TryCreateDbIfNotExists(connectionString)) return;
			throw;
		}
	}

	private static bool TryCreateDbIfNotExists(string originalConnectionString)
	{
		var masterConnection = NewDatabase(originalConnectionString, "master");
		var dbName = ConnectionString.Database(originalConnectionString);

		try
		{
			using (var cn = new SqlConnection(masterConnection))
			{
				cn.Open();
				if (!SqlServerUtil.DatabaseExists(cn, dbName))
				{
					SqlServerUtil.Execute(cn, $"CREATE DATABASE [{dbName}]");
				}
				return true;
			}
		}
		catch
		{
			return false;
		}

		string NewDatabase(string originalConnectionString, string databaseName)
		{
			var parts = ConnectionString.ToDictionary(originalConnectionString);
			var tokens = new[] { "Initial Catalog", "Database" };
			var dbToken = tokens.FirstOrDefault(parts.ContainsKey) ?? throw new ArgumentException($"Expected token {string.Join(", ", tokens)} not found in connection string");
			parts[dbToken] = databaseName;
			return string.Join(";", parts.Select(kp => $"{kp.Key}={kp.Value}"));
		}
	}

	private static (Configuration Data, string BasePath) FindConfig(string[] args)
	{
		var startPath = args.Length == 0 ? "." : args[0];
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