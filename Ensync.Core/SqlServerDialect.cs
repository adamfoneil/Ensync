using Ensync.Core.Abstract;
using Ensync.Core.Models;

namespace Ensync.Core;

public class SqlServerDialect : SqlDialect
{
    public override Dictionary<DbObjectType, SqlStatements> Syntax => new()
    {
        [DbObjectType.Table] = new()
        {            
            Create = CreateTable,            
            Alter = (parent, child) => throw new NotSupportedException(),
            Drop = DropTable
        },
        [DbObjectType.Column] = new()
        {
            Definition = ColumnDefinition,
            Create = AddColumn, // ALTER TABLE <parent> ADD ....
            Alter = AlterColumn, // ALTER TABLE <parent> ALTER COLUMN 
            Drop = DropColumn // ALTER TABLE <parent> DROP
        }
    };

    private IEnumerable<string> AlterColumn(DbObject? parent, DbObject child)
    {
        var column = child as Column ?? throw new Exception("Unexpected object type");
        yield return $"ALTER TABLE {FormatName(parent!)} ALTER COLUMN {ColumnDefinition(column)}";
    }

    private string ColumnDefinition(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        return $"[{column.Name}] {column.DataType} {(column.IsNullable ? "NULL" : "NOT NULL")}";
    }

    private IEnumerable<string> DropColumn(DbObject? parent, DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        yield return $"ALTER TABLE {FormatName(parent!)} DROP COLUMN {FormatName(column)}";
    }

    private IEnumerable<string> AddColumn(DbObject? parent, DbObject child)
    {
        var column = child as Column ?? throw new Exception("Unexpected object type");
        yield return $"ALTER TABLE {FormatName(parent!)} ADD {ColumnDefinition(column)}";
    }

    private IEnumerable<string> DropTable(DbObject? parent, DbObject child)
    {
        yield return $"DROP TABLE {FormatName(child)}";
    }

    private IEnumerable<string> AlterTable(DbObject? parent, DbObject @object) => throw new NotImplementedException();

    private IEnumerable<string> CreateTable(DbObject? parent, DbObject child)
    {
        var table = child as Table ?? throw new Exception("Unexpected object type");

        //if (!SchemaExists) create schema        

        yield return 
            $@"CREATE TABLE {FormatName(child)} (
                {string.Join("\r\n,", table.Columns.Select(Syntax[DbObjectType.Column].Definition!))}
            )";

        foreach (var index in table.Indexes)
        {

        }

        foreach (var check in table.CheckConstraints)
        {

        }
    }
    
    protected override string FormatName(DbObject dbObject)
    {
        var parts = dbObject.Name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(part => $"[{part}]"));
    }
}
