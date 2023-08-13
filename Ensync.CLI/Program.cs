using AO.ConnectionStrings;
using CommandLine;
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
		await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async o =>
		{
			if (o.Merge) o.ActionName = "Merge";

			var config = FindConfig(o.ConfigPath);
			var targets = config.Data.DatabaseTargets.ToDictionary(item => item.Name);

			var source = await GetSourceSchemaAsync(o, config.BasePath, config.Data, targets);
			var target = await GetDbSchemaAsync(o, config.Data, targets);
			Console.WriteLine($"Merging from {source.Description} to {target.Description}");
			Console.WriteLine();

			var scriptBuilder = new SqlServerScriptBuilder(target.Target.ConnectionString);
			var script = await source.Schema.CompareAsync(target.Schema, scriptBuilder);

			var statements = script.ToSqlStatements(scriptBuilder, true).ToArray();

			switch (o.Action)
			{
				case Action.Preview:
					PreviewChanges(statements);
					break;

				case Action.Merge:
					MergeChanges(target.Target, statements);
					break;

				case Action.LaunchSqlFile:
					break;

				case Action.Ignore:
					break;
			}

		});
	}

	private static async Task<(Configuration.Target Target, string Description, Schema Schema)> GetDbSchemaAsync(Options options, Configuration config, Dictionary<string, Configuration.Target> targets)
	{
		var targetName = options.DbTarget ?? config.DatabaseTargets[0].Name;
		var target = targets[targetName];

		EnsureValidDbTarget(target.ConnectionString);

		var dbInspector = new SqlServerSchemaInspector(target.ConnectionString);
		var description = $"database {target.Name}";
		return (target, description, await dbInspector.GetSchemaAsync());
	}

	private static async Task<(string Description, Schema Schema)> GetSourceSchemaAsync(Options options, string basePath, Configuration config, Dictionary<string, Configuration.Target> targets)
	{
		if (options.UseAssemblySource)
		{
			var assemblyFile = PathHelper.Resolve(basePath, config.AssemblyPath);
			var assemblyInspector = new AssemblySchemaInspector(assemblyFile);
			return ($"assembly {Path.GetFileName(assemblyFile)}", await assemblyInspector.GetSchemaAsync());
		}

		var dbSchema = await GetDbSchemaAsync(options, config, targets);
		return (dbSchema.Description, dbSchema.Schema);
	}

	private static void PreviewChanges(string[] statements)
	{
		var color = Console.ForegroundColor;
		try
		{
			if (statements.Any())
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("Previewing changes:");
				foreach (var cmd in statements)
				{
					Console.WriteLine(cmd);
					Console.WriteLine();
				}
			}
			else
			{
				Console.WriteLine("No changes found");
			}
		}
		finally
		{
			Console.ForegroundColor = color;
		}

	}

	private static void MergeChanges(Configuration.Target target, string[] statements)
	{
		if (!statements.Any())
		{
			Console.WriteLine("No changes to merge");
			return;
		}

		var color = Console.ForegroundColor;

		if (target.IsProduction)
		{
			Console.ForegroundColor = ConsoleColor.Magenta;
			Console.WriteLine("Confirm merge to production database by typing the database name:");
			var result = Console.ReadLine();
			if (!result?.Equals(ConnectionString.Database(target.ConnectionString)) ?? true)
			{
				Console.WriteLine("Operation halted");
				Console.ForegroundColor = color;
				return;
			}
		}

		using var cn = new SqlConnection(target.ConnectionString);
		cn.Open();
		using var txn = cn.BeginTransaction();

		try
		{
			foreach (var sql in statements)
			{
				using var cmd = new SqlCommand(sql, cn, txn);
				cmd.ExecuteNonQuery();
			}
			txn.Commit();
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Changes merged successfully");
		}
		catch (Exception exc)
		{
			txn.Rollback();
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(exc.ToString());
		}
		finally
		{
			Console.ForegroundColor = color;
		}
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