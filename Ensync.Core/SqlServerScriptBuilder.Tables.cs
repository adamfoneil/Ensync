using Ensync.Core.Abstract;
using Ensync.Core.Models;

namespace Ensync.Core;

public partial class SqlServerScriptBuilder
{
    private IEnumerable<(StatementPlacement, string)> AlterColumn(DbObject? parent, DbObject child)
    {
        var column = child as Column ?? throw new Exception("Unexpected object type");
        yield return (StatementPlacement.Immediate, $"ALTER TABLE {FormatName(parent!)} ALTER COLUMN {ColumnDefinition(column)}");
    }

    private string ColumnDefinition(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        return $"[{column.Name}] {column.DataType} {(column.IsNullable ? "NULL" : "NOT NULL")}";
    }

    private IEnumerable<(StatementPlacement, string)> DropColumn(DbObject? parent, DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        yield return (StatementPlacement.Immediate, $"ALTER TABLE {FormatName(parent!)} DROP COLUMN {FormatName(column)}");
    }

    private IEnumerable<(StatementPlacement, string)> AddColumn(DbObject? parent, DbObject child)
    {
        var column = child as Column ?? throw new Exception("Unexpected object type");
        yield return (StatementPlacement.Immediate, $"ALTER TABLE {FormatName(parent!)} ADD {ColumnDefinition(column)}");
    }

    private IEnumerable<(StatementPlacement, string)> DropTable(DbObject? parent, DbObject child)
    {
        yield return (StatementPlacement.Immediate, $"DROP TABLE {FormatName(child)}");
    }

    private IEnumerable<string> AlterTable(DbObject? parent, DbObject @object) => throw new NotImplementedException();

    private IEnumerable<(StatementPlacement, string)> CreateTable(DbObject? parent, DbObject child)
    {
        var table = child as Table ?? throw new Exception("Unexpected object type");

        var schema = SchemaName(child.Name);
        if (!_schemas.Contains(schema))
        {
            yield return (StatementPlacement.Immediate, $"CREATE SCHEMA {FormatName(schema)}");
        }

        yield return
            (StatementPlacement.Immediate, $@"CREATE TABLE {FormatName(child)} (
                {string.Join("\r\n,", table.Columns.Select(Syntax[DbObjectType.Column].Definition!))}
            )");

        foreach (var index in table.Indexes)
        {
            // create index or alter table add constraint
        }

        foreach (var check in table.CheckConstraints)
        {
            // alter table add constraint
        }

        foreach (var fk in table.ForeignKeys)
        {
            var tableName = ParseTableName(fk.ReferencedTable.Name);
            if (_tables.Contains((tableName.Schema, tableName.Name)))
            {
                string referencingColumns = string.Join(", ", fk.Columns.Select(col => FormatName(col.ReferencingName)));
                string referencedColumns = string.Join(", ", fk.Columns.Select(col => FormatName(col.ReferencedName)));
                var result = $"ALTER TABLE {FormatName(child)} ADD CONSTRAINT {FormatName(fk)} FOREIGN KEY ({referencingColumns}) REFERENCES {FormatName(fk.ReferencedTable)} ({referencedColumns})";
                if (fk.CascadeDelete) result += " ON DELETE CASCADE";
                if (fk.CascadeUpdate) result += " ON UPDATE CASCADE";
                yield return (StatementPlacement.Deferred, result);
            }
        }
    }

    private string SchemaName(string name)
    {
        var parts = name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

        return
            (parts.Length == 1) ? "dbo" :
            (parts.Length > 1) ? parts[0] :
            throw new Exception("How did you get here?");
    }
}
