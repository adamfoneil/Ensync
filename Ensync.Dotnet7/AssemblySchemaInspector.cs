using Ensync.Core.Abstract;
using System.Reflection;

namespace Ensync.Dotnet7;

public class AssemblySchemaInspector : SchemaInspector
{
    private readonly Assembly _assembly;

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