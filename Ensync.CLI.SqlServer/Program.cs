﻿using AO.ConnectionStrings;
using CommandLine;
using Ensync.Core;
using Ensync.Core.Abstract;
using Ensync.Core.Extensions;
using Ensync.Core.Models;
using Ensync.Dotnet;
using Ensync.SqlServer;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;

namespace Ensync.CLI;

internal class Program
{
	const string ConfigFilename = "ensync.config.json";
	const string IgnoreFilename = "ensync.ignore.json";

	const string DefaultConnectionString = "<add your connection string here>";

	static async Task Main(string[] args)
	{
		await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async o =>
		{
			WriteColorLine($"Ensync version {GetVersion()}", ConsoleColor.Cyan);

			try
			{
				var config = FindConfig(o.ConfigPath);

				if (o.Init)
				{
					CreateEmptyConfig(config.BasePath);
					return;
				}

				if (o.Merge) o.ActionName = "Merge";
				if (o.Script) o.ActionName = "Script";

				var targets = config.Data.DatabaseTargets.ToDictionary(item => item.Name);

				var source = await GetSourceSchemaAsync(o, config.BasePath, config.Data, targets);
				var target = await GetDbSchemaAsync(o, config.Data, targets);
				Console.WriteLine($"Merging from {source.Description} to {target.Description}");
				Console.WriteLine();

				var scriptBuilder = new SqlServerScriptBuilder(target.Target.ConnectionString);
				var script = await source.Schema.CompareAsync(target.Schema, scriptBuilder, o.Debug);

				if (!string.IsNullOrWhiteSpace(o.Ignore))
				{
					o.ActionName = "Preview";
					AppendIgnoreObjects(config.Ignore, o.Ignore, script);
					SaveIgnoreSettings(config.BasePath, config.Ignore);
				}

				var executeScript = script.Except(config.Ignore.ToScriptActions()).Where(sa => Filtered(o.Filter, sa)).ToArray();
				var destructive = executeScript.Where(a => a.IsDestructive);
				foreach (var action in destructive)
				{
					// todo: prompt in some way
				}

				var sqlStatements = executeScript.ToSqlStatements(scriptBuilder, true).ToArray();
				var compactStatements = executeScript.ToCompactStatements();

				switch (o.Action)
				{
					case Action.Preview:
						PreviewChanges(o.Compact ? compactStatements : sqlStatements);
						if (sqlStatements.Any())
						{
							WriteColorLine("Use --merge to apply changes to database. Use --debug to show script comments", ConsoleColor.Cyan);
						}
						break;

					case Action.Merge:
						MergeChanges(target.Target, sqlStatements);
						break;

					case Action.Script:
						CreateSqlScript(config.BasePath, sqlStatements);
						break;

					case Action.CaptureTestCase:
						SetFKParents(source.Schema);
						SetFKParents(target.Schema);
						WriteZipFile(config.BasePath, "TestCase.zip",
						[
							("source.json", source.Schema),
							("target.json", target.Schema),
							("metadata.json", scriptBuilder.Metadata),
							("statements.json", sqlStatements)
						], GetOptions());
						WriteColorLine("Created zip file test case", ConsoleColor.Green);
						break;

					case Action.Debug:
						SaveSchemaMarkdown(config.BasePath, "source.md", source.Schema);
						SaveSchemaMarkdown(config.BasePath, "target.md", target.Schema);
						CreateSqlScript(config.BasePath, sqlStatements);
						WriteColorLine("Created source.md, target.md, and script.sql", ConsoleColor.Green);
						break;
				}
			}
			catch (Exception exc)
			{
				WriteColorLine(exc.Message, ConsoleColor.Red);
			}			
		});

		static void SetFKParents(Schema schema)
		{
			foreach (var fk in schema.ForeignKeys)
			{
				fk.ParentName ??= fk.Parent?.Name;
			}
		}
	}

	/// <summary>
	/// public so I could write a test theoretically
	/// </summary>
	public static bool Filtered(string? filter, ScriptAction action)
	{
		if (string.IsNullOrWhiteSpace(filter)) return true;

		DbObjectType? filterType;
		string? objectTypeStr = null;
		string? objectNameStr;

		try
		{
			int colonPosition = filter.IndexOf(':');
			objectNameStr = (colonPosition > -1) ? filter[(colonPosition + 1)..] : filter;
			objectTypeStr = (colonPosition > -1) ? filter[..colonPosition] : null;
			filterType = objectTypeStr is not null ? Enum.Parse<DbObjectType>(objectTypeStr) : null;
		}
		catch 
		{
			throw new Exception($"Unrecognized object type filter: {objectTypeStr}");
		}
			
		return 
			action.Object.Name.Contains(objectNameStr, StringComparison.InvariantCultureIgnoreCase) && 
			(action.Object.Type == filterType || filterType is null);
	}

	private static void AppendIgnoreObjects(Ignore ignore, string expression, IEnumerable<ScriptAction> script)
	{
		var ignoreObjects = script.Where(IsIgnored).SelectMany(action => new ScriptActionKey[]
		{
			new(ScriptActionType.Create, action.Object.Name, action.Object.Type),
			new(ScriptActionType.Alter, action.Object.Name, action.Object.Type),
			new(ScriptActionType.Drop, action.Object.Name, action.Object.Type)
		}).ToArray();

		var list = ignore.Actions.ToList();
		list.AddRange(ignoreObjects);
		ignore.Actions = [.. list];

		bool IsIgnored(ScriptAction action)
		{
			var (objectType, startsWith) = ParseExpression();
			return (action.Object.Type == objectType && action.Object.Name.StartsWith(startsWith));
		}

		(DbObjectType Type, string StartsWith) ParseExpression()
		{
			var colon = expression.IndexOf(':');
			if (colon == -1) throw new Exception("Ignore expression should have an object type followed by a colon before the object name to ignore.");
			var objType = Enum.Parse<DbObjectType>(expression[..colon], true);
			var startsWith = expression[(colon + 1)..];
			return (objType, startsWith);
		}
	}

	private static void SaveSchemaMarkdown(string basePath, string fileName, Schema schema)
	{
		var outputFile = Path.Combine(basePath, fileName);
		if (File.Exists(outputFile)) File.Delete(outputFile);

		using var output = File.CreateText(outputFile);

		output.WriteLine("# Tables:");
		foreach (var tbl in schema.Tables.OrderBy(tbl => tbl.Name))
		{
			output.WriteLine($"## {tbl.Name}");
			output.WriteLine("###  Columns:");
			foreach (var col in tbl.Columns.OrderBy(col => col.Name))
			{
				output.WriteLine($"- {col.Name} {col.DataType} {(col.IsNullable ? "NULL" : "NOT NULL")}");
			}

			output.WriteLine("### Indexes:");
			foreach (var ndx in tbl.Indexes.OrderBy(ndx => ndx.Name))
			{
				output.WriteLine($"- {ndx.Name}: {string.Join(", ", ndx.Columns.OrderBy(col => col.Name).Select(col => col.Name))}");
			}

			output.WriteLine();
		}

		output.WriteLine();
		output.WriteLine("# Foreign Keys:");
		foreach (var fk in schema.ForeignKeys)
		{
			output.WriteLine($"- {fk.Name}: {string.Join(", ", fk.Columns
				.OrderBy(col => col.ReferencingName)
				.Select(col => $"{fk.Parent!.Name}.{col.ReferencingName} = {fk.ReferencedTable.Name}.{col.ReferencedName}"))}");
		}

		output.Close();
	}

	private static JsonSerializerOptions GetOptions() =>
		new()
		{
			WriteIndented = true
		};

	private static void WriteZipFile(string path, string zipFilename, (string, object)[] contents, JsonSerializerOptions options)
	{
		var zipPath = Path.Combine(path, zipFilename);
		if (File.Exists(zipPath)) File.Delete(zipPath);

		using var stream = File.Create(zipPath);
		using var zipFile = new ZipArchive(stream, ZipArchiveMode.Create); // not working

		foreach (var item in contents)
		{
			var entry = zipFile.CreateEntry(item.Item1);
			using var entryStream = entry.Open();
			JsonSerializer.Serialize(entryStream, item.Item2, options: options);
		}
	}

	private static string GetVersion() => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "<unkown version>";

	private static void CreateSqlScript(string basePath, string[] statements)
	{
		var outputFile = Path.Combine(basePath, "script.sql");
		if (File.Exists(outputFile)) File.Delete(outputFile);

		using var output = File.CreateText(outputFile);
		foreach (var statement in statements)
		{
			output.WriteLine(statement);
			output.WriteLine();
		}
		output.Close();
	}

	private static Configuration DefaultConfiguration(string basePath) => new()
	{
		AssemblyPath = FindAssemblyPath(basePath),
		DatabaseTargets = [FindDefaultDatabaseTarget(basePath)]
	};

	private static void CreateEmptyConfig(string basePath)
	{
		Console.WriteLine("Creating empty configuration...");
		var outputFile = Path.Combine(basePath, ConfigFilename);

		if (!File.Exists(outputFile))
		{
			var config = DefaultConfiguration(basePath);

			var json = JsonSerializer.Serialize(config, new JsonSerializerOptions()
			{
				WriteIndented = true
			});

			File.WriteAllText(outputFile, json);
		}

		if (!File.Exists(GetIgnoreFilename(basePath)))
		{
			var ignore = new Ignore()
			{
				Actions = [new(ScriptActionType.Create, "dbo.Sample", DbObjectType.Table)]
			};

			SaveIgnoreSettings(basePath, ignore);
		}
	}

	private static string GetIgnoreFilename(string basePath) => Path.Combine(basePath, IgnoreFilename);

	private static void SaveIgnoreSettings(string basePath, Ignore ignore)
	{
		var outputFile = GetIgnoreFilename(basePath);

		var json = JsonSerializer.Serialize(ignore, new JsonSerializerOptions()
		{
			WriteIndented = true
		});

		File.WriteAllText(outputFile, json);
	}

	private static Configuration.Target FindDefaultDatabaseTarget(string basePath)
	{
		const string ConnectionType = "SqlServer";

		var folders = new[]
		{
			basePath, // assumed to be project
			Directory.GetParent(basePath)?.FullName // assumed to be solution
		};

		foreach (var folder in folders.Where(val => !string.IsNullOrWhiteSpace(val)))
		{
			var appSettings = FileUtil.FindWhere(folder!, "appsettings.json").FirstOrDefault();
			if (appSettings is null) continue;

			var json = File.ReadAllText(appSettings);
			var connectionInfo = JsonHelper.FindFirstConnectionString(json);
			if (connectionInfo.Success)
			{
				return new Configuration.Target()
				{
					Name = connectionInfo.Name!,
					ConnectionString = connectionInfo.ConnectionString!,
					Type = ConnectionType
				};
			}
		}

		return Empty();

		static Configuration.Target Empty() => new()
		{
			Type = ConnectionType,
			ConnectionString = DefaultConnectionString,
			Name = "default"
		};
	}

	private static string FindAssemblyPath(string basePath)
	{
		const string NotFound = "<no project found>";
		var csproj = FileUtil.FindWhere(basePath, "*.csproj").FirstOrDefault() ?? NotFound;
		if (csproj.Equals(NotFound)) return NotFound;

		var csprojDoc = XDocument.Load(csproj);
		var assemblyFileName = csprojDoc.Descendants("AssemblyName").FirstOrDefault()?.Value ?? Path.GetFileNameWithoutExtension(csproj) + ".dll";
		var targetFramework = csprojDoc.Descendants("TargetFramework").FirstOrDefault()?.Value ?? "net8.0";
		return $".\\bin\\Debug\\{targetFramework}\\{assemblyFileName}";
	}

	private static async Task<(Configuration.Target Target, string Description, Schema Schema)> GetDbSchemaAsync(Options options, Configuration config, Dictionary<string, Configuration.Target> targets)
	{
		if (!config.DatabaseTargets.Any()) throw new Exception("Please create at least one database target.");

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
			if (AssemblyOutdated(assemblyFile, basePath))
			{
				if (!BuildProject(basePath)) throw new Exception("Build failed");
			}

			var assemblyInspector = new AssemblySchemaInspector(assemblyFile);
			return ($"assembly {Path.GetFileName(assemblyFile)}", await assemblyInspector.GetSchemaAsync());
		}

		var dbSchema = await GetDbSchemaAsync(options, config, targets);
		return (dbSchema.Description, dbSchema.Schema);
	}

	private static bool BuildProject(string basePath)
	{
		Console.WriteLine("Building project...");

		var psi = new ProcessStartInfo()
		{
			FileName = "dotnet",
			Arguments = $"build \"{basePath}\" --configuration Debug",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		var process = Process.Start(psi) ?? throw new Exception($"Couldn't start {psi.FileName}");
		_ = process.StandardOutput.ReadToEnd();
		var errors = process.StandardError.ReadToEnd();
		process.WaitForExit();

		if (!string.IsNullOrWhiteSpace(errors))
		{
			WriteColorLine(errors, ConsoleColor.Red);
			return false;
		}

		return true;
	}

	private static bool AssemblyOutdated(string assemblyFile, string basePath)
	{
		var buildDate = new FileInfo(assemblyFile).LastWriteTimeUtc;
		var sourceDate = Directory.GetFiles(basePath, "*.cs", SearchOption.AllDirectories).Select(fi => new FileInfo(fi)).Max(fi => fi.LastWriteTimeUtc);
		return sourceDate > buildDate;
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
		if (connectionString.Equals(DefaultConnectionString)) throw new NotImplementedException($"It looks like your connection string has not been setup yet in {ConfigFilename}");

		try
		{
			Console.WriteLine("Checking database...");
			using var cn = new SqlConnection(connectionString);
			cn.Open();
		}
		catch
		{
			Console.Write($"Creating database {ConnectionString.Database(connectionString)}...");
			if (TryCreateDbIfNotExists(connectionString))
			{
				Thread.Sleep(1000);
				return;
			}
			throw;
		}
	}

	private static bool TryCreateDbIfNotExists(string originalConnectionString)
	{
		var masterConnection = NewDatabase(originalConnectionString, "master");
		var dbName = ConnectionString.Database(originalConnectionString);

		try
		{
			using var cn = new SqlConnection(masterConnection);
			cn.Open();
			if (!SqlServerUtil.DatabaseExists(cn, dbName))
			{
				SqlServerUtil.Execute(cn, $"CREATE DATABASE [{dbName}]");
				int count = 0;
				do
				{
					Thread.Sleep(500);
					try
					{
						using var testCn = new SqlConnection(originalConnectionString);
						testCn.Open();
						testCn.Close();
						return true;
					}
					catch
					{
						if (count > 10) throw;
					}
					count++;
				} while (true);
			}
			cn.Close();
			return true;
		}
		catch
		{
			return false;
		}

		static string NewDatabase(string originalConnectionString, string databaseName)
		{
			var parts = ConnectionString.ToDictionary(originalConnectionString);
			var tokens = new[] { "Initial Catalog", "Database" };
			var dbToken = tokens.FirstOrDefault(parts.ContainsKey) ?? throw new ArgumentException($"Expected token {string.Join(", ", tokens)} not found in connection string");
			parts[dbToken] = databaseName;
			return string.Join(";", parts.Select(kp => $"{kp.Key}={kp.Value}"));
		}
	}

	private static (Configuration Data, Ignore Ignore, string BasePath) FindConfig(string configPath)
	{
		var path = Path.GetFullPath(configPath);

		if (!IsProjectPath(path)) throw new Exception($"{path} is missing a .csproj file. Are you in a solution directory?");

		var configFile = Path.Combine(path, ConfigFilename);
		var ignoreFile = Path.Combine(path, IgnoreFilename);

		Configuration? config = default;
		Ignore ignore = new();

		if (File.Exists(configFile))
		{
			var json = File.ReadAllText(configFile);
			config = JsonSerializer.Deserialize<Configuration>(json) ?? throw new Exception("Couldn't read config file json");
		}

		if (File.Exists(ignoreFile))
		{
			var json = File.ReadAllText(ignoreFile);
			ignore = JsonSerializer.Deserialize<Ignore>(json) ?? throw new Exception("Couldn't read ignore file json");
		}

		if (config is not null)
		{
			return (config, ignore, path);
		}
		
		CreateEmptyConfig(path);
		return FindConfig(path);
		
		static bool IsProjectPath(string path) => Directory.GetFiles(path, "*.csproj").Any();		
	}

	private static void WriteColorLine(string text, ConsoleColor color)
	{
		var currenColor = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.WriteLine(text);
		Console.ForegroundColor = currenColor;
	}
}