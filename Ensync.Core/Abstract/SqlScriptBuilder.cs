namespace Ensync.Core.Abstract;

public class DatabaseMetadata
{
	public HashSet<string> Schemas { get; init; } = new();
	public HashSet<string> Tables { get; init; } = new();
	public Dictionary<string, long> RowCounts { get; init; } = new();
	public long GetRowCount(string tableName) => RowCounts.TryGetValue(tableName, out var count) ? count : 0;

	internal string? GetDropWarning(string tableName)
	{
		var rows = GetRowCount(tableName);
		return (rows > 0) ? $"Caution! {rows:n0} rows will be deleted" : default;
	}
}

public abstract class SqlScriptBuilder
{
	public abstract Dictionary<DbObjectType, SqlStatements> Syntax { get; }

	protected abstract string FormatName(DbObject dbObject);
	protected abstract string FormatName(string name);
	protected abstract Task<DatabaseMetadata> GetMetadataAsync();

	protected abstract string BlockCommentStart { get; }
	protected abstract string BlockCommentEnd { get; }
	protected abstract string LineCommentStart { get; }

	public string ToBlockComment(string statement) => $"{BlockCommentStart}{statement}{BlockCommentEnd}";
	public string ToLineComment(string comment) => $"{LineCommentStart}{comment}";

	public DatabaseMetadata Metadata { get; private set; } = new();

	public async Task InspectTargetDatabaseAsync()
	{
		Metadata = await GetMetadataAsync();
	}

	public IEnumerable<(string, DbObject?)> GetScript(ScriptActionType actionType, Schema schema, DbObject? parent, DbObject child) => actionType switch
	{
		ScriptActionType.Create =>
			Syntax[child.Type].Create.Invoke(parent, child),

		ScriptActionType.Alter =>
			DropDependencies(Syntax, schema, child)
			.Concat(Syntax[child.Type].Alter.Invoke(parent, child))
			.Concat(CreateDependencies(Syntax, schema, child)),

		ScriptActionType.Drop =>
			DropDependencies(Syntax, schema, child)
			.Concat(Syntax[child.Type].Drop.Invoke(parent, child)),

		_ => throw new NotSupportedException()
	};

	private static IEnumerable<(string, DbObject?)> DropDependencies(Dictionary<DbObjectType, SqlStatements> syntax, Schema schema, DbObject child) =>
		child.GetDependencies(schema).SelectMany(obj => syntax[obj.Child.Type].Drop(obj.Parent, obj.Child));

	private static IEnumerable<(string, DbObject?)> CreateDependencies(Dictionary<DbObjectType, SqlStatements> syntax, Schema schema, DbObject child) =>
		child.GetDependencies(schema).SelectMany(obj => syntax[obj.Child.Type].Create(obj.Parent, obj.Child));

	public class SqlStatements
	{
		public Func<DbObject, string>? Definition { get; init; }
		public required Func<DbObject?, DbObject, IEnumerable<(string, DbObject?)>> Create { get; init; }
		public required Func<DbObject?, DbObject, IEnumerable<(string, DbObject?)>> Alter { get; init; }
		public required Func<DbObject?, DbObject, IEnumerable<(string, DbObject?)>> Drop { get; init; }
	}
}
