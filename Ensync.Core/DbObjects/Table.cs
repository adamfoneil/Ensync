using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

public class Table : DbObject
{
	public override DbObjectType Type => DbObjectType.Table;

	public IEnumerable<Column> Columns { get; set; } = Enumerable.Empty<Column>();
	public IEnumerable<Index> Indexes { get; set; } = Enumerable.Empty<Index>();
	public IEnumerable<CheckConstraint> CheckConstraints { get; set; } = Enumerable.Empty<CheckConstraint>();

	public string ClusteredIndex { get; init; } = default!;
	public string IdentityColumn { get; init; } = default!;

	public async Task<IEnumerable<ScriptAction>> CompareAsync(Table targetTable, SqlScriptBuilder scriptBuilder)
	{
		var sourceSchema = new Schema() { Tables = new[] { this } };
		var targetSchema = new Schema() { Tables = new[] { targetTable } };
		return await sourceSchema.CompareAsync(targetSchema, scriptBuilder);
	}

	public override IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema, List<ScriptAction> script)
	{
		var results = schema.ForeignKeys
			.Where(fk => fk.ReferencedTable.Equals(this) && !AlreadyDropping(fk.Parent))
			.Select(fk => ((DbObject?)fk.Parent, (DbObject)fk))
			.ToList();

		return results;

		bool AlreadyDropping(DbObject? @object) =>
			script.Any(sa => sa.Action == ScriptActionType.Drop && sa.Object.Equals(@object));
	}

}
