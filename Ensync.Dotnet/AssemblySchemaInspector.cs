using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;
using Ensync.Dotnet.Extensions;
using Microsoft.Extensions.DependencyModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Index = Ensync.Core.DbObjects.Index;

namespace Ensync.Dotnet;

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
		if (!File.Exists(fileName)) throw new FileNotFoundException(fileName);

		var depsFile = Path.Combine(
			Path.GetDirectoryName(fileName) ?? throw new Exception($"Couldn't get directory name from {fileName}"),
			Path.GetFileNameWithoutExtension(fileName) + ".deps.json");

		if (File.Exists(depsFile))
		{
			_dependencyContext ??= LoadDependencyContext(depsFile) ?? throw new Exception("Couldn't load dependency context");
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
		}

		_assembly = Assembly.LoadFile(fileName);
		TypeFilter = (type) => !type.IsAbstract && type.IsPublic;
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
		TypeFilter = (type) => !type.IsAbstract && type.IsPublic;
	}

	public virtual Func<Type, bool> TypeFilter { get; set; }

	public IEnumerable<(Type, string Message)> Errors { get; private set; } = Enumerable.Empty<(Type, string)>();

	protected override async Task<(IEnumerable<Table> Tables, IEnumerable<ForeignKey> ForeignKeys)> GetDbObjectsAsync()
	{
		await Task.CompletedTask;

		var types = _assembly.GetExportedTypes().Where(TypeFilter);

		List<(Type Type, Table Table, string ConstraintName)> tables = new();
		List<ForeignKey> foreignKeys = new();
		List<(Type, string)> errors = new();

		AddTables(types, tables, errors);
        AddForeignKeys(tables, foreignKeys, errors);

		Errors = errors;
		return (tables.Select(tuple => tuple.Table), foreignKeys);
	}

	private static void AddForeignKeys(List<(Type Type, Table Table, string ConstraintName)> tables, List<ForeignKey> foreignKeys, List<(Type, string)> errors)
	{
		var tableDictionary = tables.ToDictionary(tuple => tuple.Type.Name);

		var results = tables.SelectMany(tuple =>
			MappedProperties(tuple.Type)
			.Where(pi => pi.HasAttribute<ForeignKeyAttribute>(out _)), (tuple, pi) =>
			{
				var referencedTableName = pi.GetCustomAttribute<ForeignKeyAttribute>()!.Name;
				if (tableDictionary.TryGetValue(referencedTableName, out var referencedTable))
				{
					return new ForeignKey()
					{
						Name = $"FK_{tuple.ConstraintName}_{pi.Name}",
						Parent = tuple.Table,
						ReferencedTable = referencedTable.Table,
						Columns = new[] { new ForeignKey.Column() { ReferencingName = pi.Name, ReferencedName = referencedTable.Table.IdentityColumn } },
						//CascadeDelete = todo
						// CascadeUpdate = todo
					};
				}

				if (StringHelper.TryParseColumnReference(referencedTableName, out var result))
				{
					return new ForeignKey()
					{
						Name = $"FK_{tuple.ConstraintName}_{pi.Name}",
						Parent = tuple.Table,
						ReferencedTable = new Table() { Name = result.TableName },
						Columns = new[] { new ForeignKey.Column() { ReferencingName = pi.Name, ReferencedName = result.ColumnName } }
					};
				}

				throw new Exception($"Couldn't parse foreign key info from: {referencedTableName}");
			});

		foreignKeys.AddRange(results);
	}

	private void AddTables(IEnumerable<Type> types, List<(Type Type, Table Table, string ConstraintName)> tables, List<(Type, string)> errors)
	{
		foreach (var type in types)
		{
			try
			{
				var table = BuildTable(type);
				tables.Add((type, table.Table, table.ConstraintName));
			}
			catch (Exception exc)
			{
				errors.Add((type, exc.Message));
			}
		}
	}

	private (Table Table, string ConstraintName) BuildTable(Type type)
	{
		var nameParts = GetTableNameParts(type, "dbo");
		var mappedProperties = MappedProperties(type);
		var identityProperty = mappedProperties.SingleOrDefault(pi => pi.Name.Equals("Id")) ?? throw new Exception($"Entity type {type.Name} missing expected Id property");

		return (new Table()
		{
			Name = $"{nameParts.Schema}.{nameParts.Name}",
			IdentityColumn = identityProperty.Name,
			Columns = BuildColumns(mappedProperties, identityProperty, type).ToArray(),
			Indexes = BuildIndexes(nameParts.BaseConstraintName, mappedProperties, identityProperty).ToArray(),
			CheckConstraints = BuildCheckConstraints(type, nameParts.BaseConstraintName).ToArray()
		}, nameParts.BaseConstraintName);
	}

	private (string Schema, string Name, string BaseConstraintName) GetTableNameParts(Type type, string defaultSchema)
	{
		string name = (type.HasAttribute(out TableAttribute tableAttr)) ? tableAttr.Name : type.Name;

		string schema =
			(tableAttr != null && !string.IsNullOrEmpty(tableAttr.Schema)) ? tableAttr.Schema :
			defaultSchema;

		var baseConstraintName = schema.Equals(defaultSchema) ? name : schema + name;

		return (schema, name, baseConstraintName);
	}

	private static IEnumerable<PropertyInfo> MappedProperties(Type type) =>
		type.GetProperties().Where(pi =>
			(SupportedTypes.ContainsKey(pi.PropertyType) || pi.PropertyType.IsNullableEnum() || pi.PropertyType.IsEnum) &&
			pi.CanWrite &&
			!pi.HasAttribute<NotMappedAttribute>(out _));

	private IEnumerable<Column> BuildColumns(IEnumerable<PropertyInfo> properties, PropertyInfo identityProperty, Type declaringType)
	{
		return properties.Select((pi, index) => new Column()
		{
			Name = GetColumnName(pi),
			IsNullable = pi.PropertyType.IsNullable() && !pi.HasAttribute<RequiredAttribute>(out _) && !pi.HasAttribute<KeyAttribute>(out _),
			DataType = GetDataType(pi),
			Position = GetPosition(pi, index)
		});

		string GetColumnName(PropertyInfo pi)
		{
			var result = pi.Name;

			if (pi.HasAttribute<ColumnAttribute>(out var columnAttribute) && !string.IsNullOrEmpty(columnAttribute.Name))
			{
				return columnAttribute.Name;
			}

			return result;
		}

		string GetDataType(PropertyInfo pi)
		{
			var result =
				pi.HasAttribute<ColumnAttribute>(out var columnAttribute) && !string.IsNullOrWhiteSpace(columnAttribute.TypeName) ? columnAttribute.TypeName :
				SupportedTypes.TryGetValue(pi.PropertyType, out var dataType) ? dataType :
				pi.PropertyType.IsEnum ? "int" :
				pi.PropertyType.IsNullableEnum() ? "int" :
				throw new NotSupportedException($"Couldn't determine data type for {pi.DeclaringType!.Name}.{pi.Name}");

			if (pi.PropertyType.Equals(typeof(string)))
			{
				result += (pi.HasAttribute<MaxLengthAttribute>(out var maxLen)) ? $"({maxLen.Length})" : "(max)";
			}

			return result;
		}

		int GetPosition(PropertyInfo pi, int index)
		{
			if (pi.Name.Equals(identityProperty.Name)) return 0;

			if (!pi.DeclaringType?.Equals(declaringType) ?? false)
			{
				// properties from base types get moved to the end
				index += 100;
			}

			return index + 1;
		}
	}

	private static IEnumerable<Index> BuildIndexes(string constraintName, IEnumerable<PropertyInfo> mappedProperties, PropertyInfo? identityProperty)
	{
		IndexType alternateKeyType = IndexType.PrimaryKey;

		if (identityProperty is not null)
		{
			yield return new Index()
			{
				Name = $"PK_{constraintName}",
				IndexType = IndexType.PrimaryKey,
				Columns = new[] { new Index.Column() { Name = identityProperty.Name, Order = 1 }, }
			};

			alternateKeyType = IndexType.UniqueConstraint;
		}

		var keyColumns = mappedProperties.Where(pi => pi.HasAttribute<KeyAttribute>(out _));
		if (keyColumns.Any())
		{
			var prefix = (alternateKeyType == IndexType.PrimaryKey) ? "PK" : "U";
			yield return new Index()
			{
				Name = $"{prefix}_{constraintName}_{string.Join("_", keyColumns.Select(col => col.Name))}",
				IndexType = alternateKeyType,
				Columns = keyColumns.Select((col, index) => new Index.Column() { Name = col.Name, Order = index })
			};
		}

		var fkColumns = mappedProperties.Where(pi => pi.HasAttribute<ForeignKeyAttribute>(out _));
		foreach (var col in fkColumns)
		{
			yield return new Index()
			{
				Name = $"IX_{constraintName}_{col.Name}",
				IndexType = IndexType.NonUnique,
				Columns = new[] { new Index.Column() { Name = col.Name, Order = 1 } }
			};
		}
	}

	private static IEnumerable<CheckConstraint> BuildCheckConstraints(Type type, string constraintName) => Enumerable.Empty<CheckConstraint>();

	private static Dictionary<Type, string> SupportedTypes
	{
		get
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
            static IEnumerable<Type> GetBothTypes(Type type)
			{
				yield return type;
				yield return typeof(Nullable<>).MakeGenericType(type);
			}

			var results = nullableBaseTypes.Select(kp => new
			{
				Types = GetBothTypes(kp.Key),
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
}