using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;

using Index = Ensync.Core.DbObjects.Index;

namespace Ensync.SqlServer;

public partial class SqlServerScriptBuilder
{
	private string IndexDefinition(DbObject @object)
	{
		var index = @object as Index ?? throw new Exception("Unexpected object type");
		var columnList = string.Join(", ", index.Columns.OrderBy(col => col.Order).Select(col => $"{FormatName(col.Name)} {((col.Direction == SortDirection.Ascending) ? "ASC" : "DESC")}"));
		return $"({columnList})";
	}

	private IEnumerable<(string, DbObject?)> CreateIndex(DbObject? parent, DbObject child)
	{
		var index = child as Index ?? throw new Exception("Unexpected object type");
		var statement = index.IndexType switch
		{
			IndexType.UniqueIndex => $"CREATE UNIQUE INDEX {FormatName(index)} ON {FormatName(parent!)} {IndexDefinition(index)}",
			IndexType.UniqueConstraint => $"ALTER TABLE {FormatName(parent!)} ADD CONSTRAINT {FormatName(index)} UNIQUE {IndexDefinition(index)}",
			IndexType.PrimaryKey => $"ALTER TABLE {FormatName(parent!)} ADD CONSTRAINT {FormatName(index)} PRIMARY KEY {IndexDefinition(index)}",
			IndexType.NonUnique => $"CREATE INDEX {FormatName(index)} ON {FormatName(parent!)} {IndexDefinition(index)}",
			_ => throw new NotSupportedException($"Unrecognized index type {index.IndexType}")
		};

		yield return (statement, index);
	}

	private IEnumerable<(string, DbObject?)> AlterIndex(DbObject? parent, DbObject child)
	{
		foreach (var cmd in DropIndex(parent, child))
		{
			yield return cmd;
		}

		foreach (var cmd in CreateIndex(parent, child))
		{
			yield return cmd;
		}
	}

	private IEnumerable<(string, DbObject?)> DropIndex(DbObject? parent, DbObject child)
	{
		var index = child as Index ?? throw new Exception("Unexpected object type");

		var statement = index.IndexType switch
		{
			IndexType.UniqueIndex or IndexType.NonUnique => $"DROP INDEX {FormatName(index)} ON {FormatName(parent!)}",
			IndexType.UniqueConstraint or IndexType.PrimaryKey => $"ALTER TABLE {FormatName(parent!)} DROP CONSTRAINT {FormatName(child)}",
			_ => throw new NotSupportedException($"Unrecognized index type {index.IndexType}")
		};

		yield return (statement, index);
	}
}
