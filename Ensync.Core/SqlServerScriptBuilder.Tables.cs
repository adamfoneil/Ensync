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

        //if (!SchemaExists) create schema

        yield return
            (StatementPlacement.Immediate, $@"CREATE TABLE {FormatName(child)} (
                {string.Join("\r\n,", table.Columns.Select(Syntax[DbObjectType.Column].Definition!))}
            )");

        foreach (var index in table.Indexes)
        {

        }

        foreach (var check in table.CheckConstraints)
        {

        }

        foreach (var fk in table.ForeignKeys)
        {
            // if referenced table exists, return deferred statements
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
