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
            Alter = (obj) => throw new NotSupportedException(),
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

    private IEnumerable<string> AlterColumn(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        yield return $"ALTER TABLE <table> ALTER COLUMN {ColumnDefinition(column)}";
    }

    private string ColumnDefinition(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        return $"[{column.Name}] {column.DataType} {(column.IsNullable ? "NULL" : "NOT NULL")}";
    }

    private IEnumerable<string> DropColumn(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        yield return $"ALTER TABLE <table> DROP COLUMN [{column.Name}]";
    }

    private IEnumerable<string> AddColumn(DbObject @object)
    {
        var column = @object as Column ?? throw new Exception("Unexpected object type");
        yield return $"ALTER TABLE <table> ADD {ColumnDefinition(column)}";
    }

    private IEnumerable<string> DropTable(DbObject @object)
    {
        yield return $"DROP TABLE {QualifiedName(@object)}";
    }

    private IEnumerable<string> AlterTable(DbObject @object)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<string> CreateTable(DbObject @object)
    {
        var table = @object as Table ?? throw new Exception("Unexpected object type");

        //if (!SchemaExists) create schema

        yield return 
            $@"CREATE TABLE {QualifiedName(@object)} (
                {string.Join("\r\n,", table.Columns.Select(Syntax[DbObjectType.Column].Definition!))}
            )";
    }
    
    private string QualifiedName(DbObject obj)
    {
        throw new NotImplementedException();
    }
}
