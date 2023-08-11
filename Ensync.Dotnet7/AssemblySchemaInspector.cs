using Ensync.Core.Abstract;
using Microsoft.Extensions.DependencyModel;
using System.Buffers.Text;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

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

        if (!File.Exists(depsFile)) throw new FileNotFoundException($"Couldn't find file {depsFile}");

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
			// Construct the path to the assembly, this is just a basic example
			// and assumes the DLL is in the same folder as the application.
			// You may need to adjust this logic to fit your application's structure.
			var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{library.Name}.dll");

			if (File.Exists(assemblyPath))
			{
				return Assembly.LoadFile(assemblyPath);
			}
		}

		return null;
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