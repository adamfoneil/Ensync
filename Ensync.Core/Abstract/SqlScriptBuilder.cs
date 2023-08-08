using Ensync.Core.Models;

namespace Ensync.Core.Abstract;

public class DatabaseMetadata
{
    public HashSet<string> Schemas { get; init; } = new();
    public HashSet<string> Tables { get; init; } = new();
    public Dictionary<string, long> RowCounts { get; init; } = new();
}

public abstract class SqlScriptBuilder
{
    public abstract Dictionary<DbObjectType, SqlStatements> Syntax { get; }

    protected abstract string FormatName(DbObject dbObject);
    protected abstract string FormatName(string name);
    protected abstract Task<DatabaseMetadata> GetMetadataAsync();

    public DatabaseMetadata Metadata { get; private set; } = new();

    public async Task InspectTargetDatabaseAsync()
    {
        Metadata = await GetMetadataAsync();
    }

    public IEnumerable<string> GetScript(ScriptActionType actionType, Schema schema, DbObject? parent, DbObject child) => actionType switch
    {
        ScriptActionType.Create => 
            Syntax[child.Type].Create.Invoke(parent, child),

        ScriptActionType.Alter => 
            DropDependencies(Syntax, schema, parent, child)
            .Concat(Syntax[child.Type].Alter.Invoke(parent, child))
            .Concat(CreateDependencies(Syntax, schema, parent, child)),

        ScriptActionType.Drop => 
            DropDependencies(Syntax, schema, parent, child)
            .Concat(Syntax[child.Type].Drop.Invoke(parent, child)),

        _ => throw new NotSupportedException()
    };

    private IEnumerable<string> DropDependencies(Dictionary<DbObjectType, SqlStatements> syntax, Schema schema, DbObject? parent, DbObject child) =>
        child.GetDependencies(schema).SelectMany(obj => syntax[obj.Child.Type].Drop(obj.Parent, obj.Child));

    private IEnumerable<string> CreateDependencies(Dictionary<DbObjectType, SqlStatements> syntax, Schema schema, DbObject? parent, DbObject child) =>
        child.GetDependencies(schema).SelectMany(obj => syntax[obj.Child.Type].Create(obj.Parent, obj.Child));

    public class SqlStatements
    {       
        public Func<DbObject, string>? Definition { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<string>> Create { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<string>> Alter { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<string>> Drop { get; init; }
    }
}
