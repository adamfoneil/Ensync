using Ensync.Core.Abstract;

namespace Ensync.Core;

public partial class SqlServerScriptBuilder : SqlScriptBuilder
{
    public SqlServerScriptBuilder()
    {            
    }

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
        },
        [DbObjectType.Index] = new()
        {
            Definition = IndexDefinition,
            Create = CreateIndex,
            Alter = AlterIndex,
            Drop = DropIndex
        }
    };

    protected override string FormatName(DbObject dbObject)
    {
        var parts = dbObject.Name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(part => $"[{part.Trim()}]"));
    }

    private async Task<bool> SchemaExistsAsync(string schemaName)
    {
        throw new NotImplementedException();
    }

    private async Task<long> GetRowCountAsync(string tableName)
    {
        throw new NotImplementedException();
    }

    private async Task<bool> TableExistsAsyc(string tableName)
    {
        throw new NotImplementedException();
    }
}
