using Ensync.Core;
using Ensync.Core.Extensions;
using Ensync.Core.Models;
using Ensync.SqlServer;
using SqlServer.LocalDb;

namespace Testing.Core;

[TestClass]
public class Tables
{
    public const string DbName = "EnsyncDemo";

    [ClassInitialize]
    public static void Startup(TestContext testContext)
    {
        using var cn = LocalDb.GetConnection(DbName);
    }

    [TestMethod]
    public void DropTable()
    {
        var parent = new Table()
        {
            Name = "dbo.Parent",
            Columns = new[]
            {
                new Column() { Name = "Id"}
            }.ToHashSet()
        };

        var child = new Table()
        {
            Name = "dbo.Child",
            Columns = new[]
            {
                new Column() { Name = "ParentId" }
            }.ToHashSet(),
            ForeignKeys = new[]
            {
                new ForeignKey()
                {
                    Name = "FK_Child_Parent",
                    ReferencedTable = parent,
                    Columns = new ForeignKey.Column[]
                    {
                        new() { ReferencedName = "Id", ReferencingName = "ParentId" }
                    }
                }
            }.ToHashSet()
        };

        var schema = new Schema()
        {
            Tables = new[] { parent, child }.ToHashSet()
        };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var statements = scriptBuilder.GetScript(ScriptActionType.Drop, schema, null, parent).ToArray();
        Assert.IsTrue(statements.SequenceEqual(new[]
        {
            "ALTER TABLE [dbo].[Child] DROP CONSTRAINT [FK_Child_Parent]",
            "DROP TABLE [dbo].[Parent]"
        })); 
    }

    [TestMethod]    
    public async Task AddColumn()
    {
        var columns = new Column[]
        {
            new() { Name = "Column1", DataType = "nvarchar(50)" },
            new() { Name = "Column2", DataType = "nvarchar(50)" }
        };

        var sourceTable = new Table() { Name = "dbo.Whatever", Columns = columns.Concat(new[] { new Column() { Name = "Column3", DataType = "bit" } }) };
        var targetTable = new Table() { Name = "dbo.Whatever", Columns = columns };

        var sourceSchema = new Schema() {  Tables = new[] { sourceTable } };
        var targetSchema = new Schema() {  Tables = new[] { targetTable } };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceSchema.CompareAsync(targetSchema, scriptBuilder);
        var statements = script.ToSqlStatements();
        Assert.IsTrue(statements.SequenceEqual(new[] { "ALTER TABLE [dbo].[Whatever] ADD [Column3] bit NOT NULL" }));
    }

    [TestMethod]
    public async Task DropColumn()
    {
        var columns = new Column[]
        {
            new() { Name = "Column1", DataType = "nvarchar(50)" },
            new() { Name = "Column2", DataType = "nvarchar(50)" }
        };

        var sourceTable = new Table() { Name = "dbo.Whatever", Columns = columns };
        var targetTable = new Table() { Name = "dbo.Whatever", Columns = columns.Concat(new[] { new Column() { Name = "Column3", DataType = "bit" } }) };

        var sourceSchema = new Schema() { Tables = new[] { sourceTable } };
        var targetSchema = new Schema() { Tables = new[] { targetTable } };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceSchema.CompareAsync(targetSchema, scriptBuilder);
        var statements = script.ToSqlStatements();
        Assert.IsTrue(statements.SequenceEqual(new[] { "ALTER TABLE [dbo].[Whatever] DROP COLUMN [Column3]" }));
    }
}
