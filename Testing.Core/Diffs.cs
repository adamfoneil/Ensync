using Ensync.Core;
using Ensync.Core.Extensions;
using Ensync.Core.DbObjects;
using Ensync.SqlServer;
using SqlServer.LocalDb;

using Index = Ensync.Core.DbObjects.Index;

namespace Testing.Core;

[TestClass]
public class Diffs
{
    public const string DbName = "EnsyncDemo";

    [ClassInitialize]
    public static void Startup(TestContext testContext)
    {
        using var cn = LocalDb.GetConnection(DbName);
    }

    [TestMethod]
    public void DropReferencedTable()
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
        var statements = scriptBuilder.GetScript(ScriptActionType.Drop, schema, null, parent);
        Assert.IsTrue(statements.Select(item => item.Item1).SequenceEqual(new[]
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
        var statements = script.ToSqlStatements(scriptBuilder);
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

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
        var statements = script.ToSqlStatements(scriptBuilder);
        Assert.IsTrue(statements.SequenceEqual(new[] { "ALTER TABLE [dbo].[Whatever] DROP COLUMN [Column3]" }));
    }

    [TestMethod]
    public async Task DropEmptyTable()
    {
        var targetTable = new Table()
        {
            Name = "dbo.Whatever"
        };

        var sourceSchema = new Schema();
        var targetSchema = new Schema() {  Tables = new Table[] { targetTable } };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceSchema.CompareAsync(targetSchema, scriptBuilder);
        Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
        {
            "DROP TABLE [dbo].[Whatever]"
        }));
    }

    [TestMethod]
    public async Task DropColumnWithIndex()
    {
        var sourceColumns = new Column[]
        {
            new() { Name = "Column2", DataType = "nvarchar(50)" }            
        };

        var targetColumns = sourceColumns.Concat(new Column[]
        {
            new() { Name = "Column1", DataType = "nvarchar(50)" }
        });

        var index = new Index()
        {
            Name = "IX_Whatever_Column1",
            IndexType = IndexType.NonUnique,
            Columns = new Index.Column[]
            {
                new() { Name = "Column1" }
            }
        };

        var sourceTable = new Table() { Name = "dbo.Whatever", Columns = sourceColumns };
        var targetTable = new Table() { Name = "dbo.Whatever", Columns = targetColumns, Indexes = new Index[] { index } };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
        var statements = script.ToSqlStatements(scriptBuilder);
        Assert.IsTrue(statements.SequenceEqual(new[]
        {
            "DROP INDEX [IX_Whatever_Column1] ON [dbo].[Whatever]",
            "ALTER TABLE [dbo].[Whatever] DROP COLUMN [Column1]"
        }));
    }

    [TestMethod]
    public async Task AlterColumn()
    {
        Assert.Fail();
    }

    [TestMethod]
    public async Task AlterColumnWithIndex()
    {
        Assert.Fail();
        // drop dependencies
        // alter column(s)
        // rebuild dependencies
    }

    [TestMethod]
    public async Task AlterReferencedPKColumn()
    {
        Assert.Fail();
    }

    [TestMethod]
    public async Task AddIndex()
    {
        var sourceTable = new Table()
        {
            Name = "dbo.Whatever",
            Indexes = new Index[]
            {
                new() { Name = "IX_Hello", IndexType = IndexType.NonUnique, Columns = new Index.Column[] 
                { 
                    new() { Name = "Column1" },
                    new() { Name = "Column2" }
                }}
            }
        };

        var targetTable = new Table()
        {
            Name = "dbo.Whatever"
        };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
        Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
        {
            "CREATE INDEX [IX_Hello] ON [dbo].[Whatever] ([Column1] ASC, [Column2] ASC)"
        }));
    }

    [TestMethod]
    public async Task DropIndex()
    {
        var sourceTable = new Table()
        {
            Name = "dbo.Whatever"            
        };

        var targetTable = new Table()
        {
            Name = "dbo.Whatever",
            Indexes = new Index[]
            {
                new() { Name = "IX_Hello", IndexType = IndexType.NonUnique, Columns = new Index.Column[]
                {
                    new() { Name = "Column1" },
                    new() { Name = "Column2" }
                }}
            }
        };

        var scriptBuilder = new SqlServerScriptBuilder(LocalDb.GetConnectionString(DbName));
        var script = await sourceTable.CompareAsync(targetTable, scriptBuilder);
        Assert.IsTrue(script.ToSqlStatements(scriptBuilder).SequenceEqual(new[]
        {
            "DROP INDEX [IX_Hello] ON [dbo].[Whatever]"
        }));
    }
}
