using AO.ConnectionStrings;
using Ensync.Core.Extensions;
using Ensync.Dotnet7;
using Ensync.SqlServer;
using Microsoft.Data.SqlClient;

namespace Ensync.CLI;

internal class Program
{
	static async Task Main(string[] args)
	{
		var context = new CommandContext(args);
		
		var fullPath = PathHelper.Resolve(context.BasePath, context.Configuration.AssemblyPath);
		Console.WriteLine(fullPath);
		
		var assemblyInspector = new AssemblySchemaInspector(fullPath);
		var assemblySchema = await assemblyInspector.GetSchemaAsync();

		var targetName = context.DbTarget;
		var target = context.Targets[targetName];

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
}