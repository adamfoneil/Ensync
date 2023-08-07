using Ensync.Core.Abstract;
using Ensync.Core.Models;

namespace Ensync.SqlServer;

public partial class SqlServerScriptBuilder
{
    private IEnumerable<(StatementPlacement, string)> AlterColumn(DbObject? parent, DbObject child)
    {        
        yield return (StatementPlacement.Immediate, $"ALTER TABLE {FormatName(parent!)} ALTER COLUMN {ColumnDefinition(child)}");
    }

    private string ColumnDefinition(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        var table = @object.Parent as Table ?? throw new Exception("Unexpected parent object type");
        
        var dataType = column.DataType;
        if (column.Name.Equals(table.IdentityColumn)) dataType += " identity(1,1)";
        return $"[{column.Name}] {dataType} {(column.IsNullable ? "NULL" : "NOT NULL")}";
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
        if (!Metadata.Schemas.Contains(schema))
        {
            yield return (StatementPlacement.Immediate, $"CREATE SCHEMA {FormatName(schema)}");
        }

        yield return
            (StatementPlacement.Immediate, 
$@"CREATE TABLE {FormatName(table)} (
{string.Join(",\r\n", table.Columns.Select(col => "\t" + Syntax[DbObjectType.Column].Definition!.Invoke(col)))}
)");

        foreach (var index in table.Indexes)
        {
            foreach (var statement in CreateIndex(table, index)) yield return statement;
        }

        foreach (var check in table.CheckConstraints)
        {
            // alter table add constraint
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
