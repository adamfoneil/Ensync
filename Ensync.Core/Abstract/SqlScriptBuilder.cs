namespace Ensync.Core.Abstract;

public class DatabaseMetadata
{
    public HashSet<string> Schemas { get; init; } = [];
    public HashSet<string> TableNames { get; init; } = [];
    public HashSet<string> ForeignKeyNames { get; init; } = [];
    public HashSet<string> IndexNames { get; init; } = [];
    public Dictionary<string, long> RowCounts { get; init; } = [];
    public long GetRowCount(string tableName) => RowCounts.TryGetValue(tableName, out var count) ? count : 0;

    internal string? GetDropWarning(string tableName)
    {
        var rows = GetRowCount(tableName);
        return (rows > 0) ? $"Caution! {rows:n0} rows will be deleted" : default;
    }

    public bool IsInitialized() => Schemas.Any() || TableNames.Any() || ForeignKeyNames.Any() || IndexNames.Any();
}

public enum StatementType
{
    Command,
    Comment
}

public abstract class SqlScriptBuilder
{
    public abstract Dictionary<DbObjectType, SqlStatements> Syntax { get; }

    protected abstract string FormatName(DbObject dbObject);
    protected abstract string FormatName(string name);
    protected abstract Task<DatabaseMetadata> GetMetadataAsync();

    protected abstract string BlockCommentStart { get; }
    protected abstract string BlockCommentEnd { get; }
    public abstract string LineCommentStart { get; }

    public string ToBlockComment(string statement) => $"{BlockCommentStart}{statement}{BlockCommentEnd}";
    public string ToLineComment(string comment) => $"{LineCommentStart}{comment}";

    public DatabaseMetadata Metadata { get; private set; } = new();

    public async Task InspectTargetDatabaseAsync()
    {
        if (Metadata.IsInitialized()) return;
        Metadata = await GetMetadataAsync();
    }

    public void SetMetadata(DatabaseMetadata databaseMetadata) => Metadata = databaseMetadata;

    public IEnumerable<(string, DbObject?)> GetScript(ScriptActionType actionType, Schema schema, DbObject? parent, DbObject child, List<ScriptAction> script, bool debug = false) => actionType switch
    {
        ScriptActionType.Create =>
            Syntax[child.Type].Create.Invoke(parent, child),

        ScriptActionType.Alter =>
            DropDependencies(Syntax, schema, child, script, debug)
            .Concat(Comment(debug, $"alter {child}"))
            .Concat(Syntax[child.Type].Alter.Invoke(parent, child))
            .Concat(CreateDependencies(Syntax, schema, child, debug, script)),

        ScriptActionType.Drop =>
            DropDependencies(Syntax, schema, child, script, debug)
            .Concat(Comment(debug, $"drop {child}"))
            .Concat(Syntax[child.Type].Drop.Invoke(parent, child)),

        _ => throw new NotSupportedException()
    };

    private IEnumerable<(string, DbObject?)> Comment(bool debug, string text)
    {
        if (debug) yield return ($"{LineCommentStart}{text}", null);
    }

    private IEnumerable<(string, DbObject?)> DropDependencies(Dictionary<DbObjectType, SqlStatements> syntax, Schema schema, DbObject child, List<ScriptAction> script, bool debug)
    {
        var results = child.GetDependencies(schema, script)
            .SelectMany(obj => syntax[obj.Child.Type].Drop(obj.Parent, obj.Child))
            .Where(obj => TargetObjectExists(obj.Item2!))
            .ToList();

        var count = results.Count;
        if (debug) results.Insert(0, ($"{LineCommentStart}drop dependencies of {child} ({count})", null));
        return results;
    }


    private IEnumerable<(string, DbObject?)> CreateDependencies(Dictionary<DbObjectType, SqlStatements> syntax, Schema schema, DbObject child, bool debug, List<ScriptAction> script)
    {
        var results = child.GetDependencies(schema, script)
            .SelectMany(obj => syntax[obj.Child.Type].Create(obj.Parent, obj.Child))
            .ToList();

        var count = results.Count;
        if (debug) results.Insert(0, ($"{LineCommentStart} create dependencies of {child} ({count})", null));
        return results;
    }

    internal bool TargetObjectExists(DbObject dbObject) => dbObject.Type switch
    {
        DbObjectType.Table => Metadata.TableNames.Contains(dbObject.Name, StringComparer.InvariantCultureIgnoreCase),
        DbObjectType.ForeignKey => Metadata.ForeignKeyNames.Contains(dbObject.Name, StringComparer.InvariantCultureIgnoreCase),
        DbObjectType.Index => Metadata.IndexNames.Contains(dbObject.Name, StringComparer.InvariantCultureIgnoreCase),
        _ => throw new NotImplementedException()
    };


    public class SqlStatements
    {
        public Func<DbObject, string>? Definition { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<(string, DbObject?)>> Create { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<(string, DbObject?)>> Alter { get; init; }
        public required Func<DbObject?, DbObject, IEnumerable<(string, DbObject?)>> Drop { get; init; }
    }
}
