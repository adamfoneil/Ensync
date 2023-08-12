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
			var config = FindConfig(o.ConfigPath);
			var assemblyFile = PathHelper.Resolve(config.BasePath, config.Data.AssemblyPath);
			Console.WriteLine(assemblyFile);

			var assemblyInspector = new AssemblySchemaInspector(assemblyFile);
			var assemblySchema = await assemblyInspector.GetSchemaAsync();

			var targets = config.Data.DatabaseTargets.ToDictionary(item => item.Name);
			var targetName = o.DbTarget ?? config.Data.DatabaseTargets[0].Name;
			var target = targets[targetName];

			EnsureValidDbTarget(target.ConnectionString);

			var dbInspector = new SqlServerSchemaInspector(target.ConnectionString);
			var dbSchema = await dbInspector.GetSchemaAsync();

			var scriptBuilder = new SqlServerScriptBuilder(target.ConnectionString);
			var script = await assemblySchema.CompareAsync(dbSchema, scriptBuilder);

			var statements = script.ToSqlStatements(scriptBuilder, true).ToArray();

			switch (o.Action)
			{
				case Action.ScriptOnly:
					foreach (var cmd in statements)
					{
						Console.WriteLine(cmd);
						Console.WriteLine();
					}
					break;

				case Action.Merge:
					MergeChanges(target.ConnectionString, statements);
					break;

				case Action.LaunchSqlFile:
					break;

				case Action.Ignore:
					break;
			}

		});
	}

	private static void MergeChanges(string connectionString, string[] statements)
	{
		using var cn = new SqlConnection(connectionString);
		cn.Open();
		using var txn = cn.BeginTransaction();

		var color = Console.ForegroundColor;
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