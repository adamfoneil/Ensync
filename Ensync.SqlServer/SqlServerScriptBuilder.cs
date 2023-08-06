using Dapper;
using Ensync.Core.Abstract;
using Microsoft.Data.SqlClient;

namespace Ensync.SqlServer;


public partial class SqlServerScriptBuilder : SqlScriptBuilder
{
    private readonly string _connectionString;

    public SqlServerScriptBuilder(string connectionString)
    {
        _connectionString = connectionString;
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
            Alter = (parent, child) => throw new NotImplementedException(),
            Drop = DropIndex
        },
        [DbObjectType.ForeignKey] = new()
        {
            Definition = ForeignKeyDefinition,
            Create = CreateForeignKey,
            Alter = (parent, child) => throw new NotImplementedException(),
            Drop = DropForeignKey
        }
    };

    protected override string FormatName(DbObject dbObject) => FormatName(dbObject.Name);

    protected override string FormatName(string name)
    {
        var parts = name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(".", parts.Select(part => $"[{part.Trim()}]"));
    }

    private (string Schema, string Name) ParseTableName(string name)
    {
        var parts = name.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        return
            (parts.Length == 2) ? (parts[0], parts[1]) :
            (parts.Length == 1) ? ("dbo", parts[0]) :
            throw new Exception("Unexpected table name format");
    }

    protected override async Task<DatabaseMetadata> GetMetadataAsync()
    {
        using var cn = new SqlConnection(_connectionString);
        var schemas = (await cn.QueryAsync<string>("SELECT [name] FROM [sys].[schemas]")).Select(val => val.ToLower()).ToHashSet();
        var tables = (await cn.QueryAsync<string>("SELECT SCHEMA_NAME([schema_id]) + '.' + [name] FROM [sys].[tables]")).Select(val => val.ToLower()).ToHashSet();
        return new DatabaseMetadata() 
        { 
            Schemas = schemas, 
            Tables = tables 
        };
    }
}
