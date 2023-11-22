using Ensync.Core.Abstract;
using Ensync.Core.DbObjects;

namespace Ensync.SqlServer;

public partial class SqlServerScriptBuilder
{
	private IEnumerable<(string, DbObject?)> AlterColumn(DbObject? parent, DbObject child)
	{
		var column = child as Column ?? throw new Exception("Unexpected object type");

		if (column.IsCalculated)
		{
			yield return ($"ALTER TABLE {FormatName(parent!)} DROP COLUMN {FormatName(child!)}", column);
			yield return ($"ALTER TABLE {FormatName(parent!)} ADD {ColumnDefinition(child)}", column);
		}
		else
		{
			yield return ($"ALTER TABLE {FormatName(parent!)} ALTER COLUMN {ColumnDefinition(child)}", child);
		}
	}

	private string ColumnDefinition(DbObject @object)
	{
		var column = @object as Column ?? throw new Exception("Unexpected object type");
		var table = @object.Parent as Table ?? throw new Exception("Unexpected parent object type");

		if (column.IsCalculated)
		{
			return $"[{column.Name}] AS {column.Expression} PERSISTED";
		}

		var dataType = column.DataType;
		if (column.Name.Equals(table.IdentityColumn)) dataType += " identity(1,1)";
		return $"[{column.Name}] {dataType} {(column.IsNullable ? "NULL" : "NOT NULL")}";
	}

	private IEnumerable<(string, DbObject?)> DropColumn(DbObject? parent, DbObject @object)
	{
		var column = @object as Column ?? throw new Exception("Unexpected object type");
		yield return ($"ALTER TABLE {FormatName(parent!)} DROP COLUMN {FormatName(column)}", column);
	}

	private IEnumerable<(string, DbObject?)> AddColumn(DbObject? parent, DbObject child)
	{
		var column = child as Column ?? throw new Exception("Unexpected object type");
		yield return ($"ALTER TABLE {FormatName(parent!)} ADD {ColumnDefinition(column)}", column);
	}

	private IEnumerable<(string, DbObject?)> DropTable(DbObject? parent, DbObject child)
	{
		yield return ($"DROP TABLE {FormatName(child)}", child);
	}

	private IEnumerable<(string, DbObject?)> CreateTable(DbObject? parent, DbObject child)
	{
		var table = child as Table ?? throw new Exception("Unexpected object type");

		var schema = SchemaName(child.Name);
		if (!Metadata.Schemas.Contains(schema))
		{
			yield return ($"CREATE SCHEMA {FormatName(schema)}", default);
		}

		yield return
($@"CREATE TABLE {FormatName(table)} (
{string.Join(",\r\n", table.Columns.OrderBy(col => col.Position).Select(col => "   " + Syntax[DbObjectType.Column].Definition!.Invoke(col)))}
)", table);

		foreach (var index in table.Indexes)
		{
			foreach (var statement in CreateIndex(table, index)) yield return (statement.Item1, index);
		}

		foreach (var check in table.CheckConstraints)
		{
			// alter table add constraint
		}
	}

	private static string SchemaName(string name)
	{
		var parts = name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

		return
			(parts.Length == 1) ? "dbo" :
			(parts.Length > 1) ? parts[0] :
			throw new Exception("How did you get here?");
	}
}
