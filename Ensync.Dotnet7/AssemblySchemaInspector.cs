using Ensync.Core.Abstract;
using Microsoft.Extensions.DependencyModel;
using System.Reflection;

namespace Ensync.Dotnet7;

public class AssemblySchemaInspector : SchemaInspector
{
    private readonly Assembly _assembly;

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
			if (local.Result)
			{
				return Assembly.LoadFile(local.Path);
			}

			var package = GetNugetPackageDll(library);
			if (package.Result)
			{
				return Assembly.LoadFile(package.Path);
			}
		}

		return null;
	}

	private (bool Result, string Path) GetNugetPackageDll(RuntimeLibrary library)
	{
		var packagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
		   ".nuget", "packages", library.Name.ToLower(), library.Version);

		var assemblyPath = Path.Combine(packagePath, library.RuntimeAssemblyGroups.First().RuntimeFiles.First().Path);

		return (File.Exists(assemblyPath), assemblyPath);
	}

	private (bool Result, string Path) GetLocalDll(string name)
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

    protected override async Task<IEnumerable<DbObject>> GetDbObjectsAsync()
    {
        var types = _assembly.GetExportedTypes().Where(TypeFilter);

        throw new NotImplementedException();
    }
}