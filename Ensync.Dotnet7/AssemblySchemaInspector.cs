using Ensync.Core;
using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;
using Microsoft.Extensions.DependencyModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Index = Ensync.Core.DbObjects.Index;

namespace Ensync.Dotnet7;

public class AssemblySchemaInspector : SchemaInspector
{
	private readonly Assembly _assembly;

	/// <summary>
	/// everything related to DependencyContext had a lot of ChatGPT help
	/// https://chat.openai.com/share/f80a4013-044b-469b-aafc-151a74b44bac
	/// </summary>
	private static DependencyContext? _dependencyContext;

	public AssemblySchemaInspector(string fileName) 
	{
		var depsFile = Path.Combine(
			Path.GetDirectoryName(fileName) ?? throw new Exception($"Couldn't get directory name from {fileName}"), 
			Path.GetFileNameWithoutExtension(fileName) + ".deps.json");

		if (!File.Exists(depsFile)) throw new FileNotFoundException($"Couldn't find dependency info file {depsFile}");

		_dependencyContext ??= LoadDependencyContext(depsFile) ?? throw new Exception("Couldn't load dependency context");

		AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

		_assembly = Assembly.LoadFile(fileName);
		TypeFilter = (type) => true;
	}

	private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
	{
		// Try to get the runtime assembly information from the DependencyContext
		var library = _dependencyContext!.RuntimeLibraries.FirstOrDefault(
			lib => string.Equals(lib.Name, args.Name.Split(',')[0], StringComparison.OrdinalIgnoreCase));

		if (library != null)
		{
			var local = GetLocalDll(library.Name);
			if (local.Success)
			{
				return Assembly.LoadFile(local.Path);
			}

			var package = GetNugetPackageDll(library);
			if (package.Success)
			{
				return Assembly.LoadFile(package.Path);
			}
		}

		return null;
	}

	private static (bool Success, string Path) GetNugetPackageDll(RuntimeLibrary library)
	{
		var packagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
		   ".nuget", "packages", library.Name.ToLower(), library.Version);

		var assemblyPath = Path.Combine(packagePath, library.RuntimeAssemblyGroups[0].RuntimeFiles[0].Path);

		return (File.Exists(assemblyPath), assemblyPath);
	}

	private static (bool Success, string Path) GetLocalDll(string name)
	{
		var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{name}.dll");
		return (File.Exists(path), path);
	}

	private static DependencyContext LoadDependencyContext(string depsJsonPath)
	{
		using var stream = new FileStream(depsJsonPath, FileMode.Open, FileAccess.Read);
		using var reader = new StreamReader(stream);
		return new DependencyContextJsonReader().Read(reader.BaseStream);
	}

	public AssemblySchemaInspector(Assembly assembly)
	{
		_assembly = assembly;
		TypeFilter = (type) => true;
	}

	public virtual Func<Type, bool> TypeFilter { get; set; }

	public IEnumerable<(Type, string Message)> Errors { get; private set; } = Enumerable.Empty<(Type, string)>();

	protected override async Task<IEnumerable<DbObject>> GetDbObjectsAsync()
	{
		await Task.CompletedTask;

		var types = _assembly.GetExportedTypes().Where(TypeFilter);

		List<DbObject> dbObjects = new();
		List<(Type, string)> errors = new();
		var typeDictionary = types.ToDictionary(t => t.Name);

		foreach (var type in types)
		{
			try
			{
				dbObjects.Add(BuildTable(type, typeDictionary));
			}
			catch (Exception exc)
			{
				errors.Add((type, exc.Message));
			}
		}

		Errors = errors;
		return dbObjects;
	}

	private Table BuildTable(Type type, Dictionary<string, Type> typeDictionary) => new Table()
	{
		Name = GetTableName(type, "dbo"),
		Columns = BuildColumns(type),
		Indexes = BuildIndexes(type),
		CheckConstraints = BuildCheckConstraints(type),
		ForeignKeys = BuildForeignKeys(type, typeDictionary)
	};

	private IEnumerable<ForeignKey> BuildForeignKeys(Type type, Dictionary<string, Type> typeDictionary)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<CheckConstraint> BuildCheckConstraints(Type type)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<Index> BuildIndexes(Type type)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<Column> BuildColumns(Type type)
	{
		throw new NotImplementedException();
	}

	private string GetTableName(Type type, string defaultSchema)
	{
		throw new NotImplementedException();
	}

	private static Dictionary<Type, string> GetSupportedTypes()
	{
		var nullableBaseTypes = new Dictionary<Type, string>()
			{
				{ typeof(int), "int" },
				{ typeof(long), "bigint" },
				{ typeof(short), "smallint" },
				{ typeof(byte), "tinyint" },
				{ typeof(DateTime), "datetime" },
				{ typeof(decimal), "decimal" },
				{ typeof(bool), "bit" },
				{ typeof(TimeSpan), "time" },
				{ typeof(Guid), "uniqueidentifier" }
			};

		// help from https://stackoverflow.com/a/23402195/2023653
		IEnumerable<Type> getBothTypes(Type type)
		{
			yield return type;
			yield return typeof(Nullable<>).MakeGenericType(type);
		}

		var results = nullableBaseTypes.Select(kp => new
		{
			Types = getBothTypes(kp.Key),
			SqlType = kp.Value
		}).SelectMany(item => item.Types.Select(t => new
		{
			Type = t,
			item.SqlType
		}));

		var result = results.ToDictionary(item => item.Type, item => item.SqlType);

		// string is special in that it's already nullable
		result.Add(typeof(string), "nvarchar");
		result.Add(typeof(byte[]), "varbinary");

		return result;
	}
}