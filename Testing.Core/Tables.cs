using Ensync.Core;
using Ensync.Core.Models;
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
        Assert.IsTrue(statements.Select(st => st.Item2).SequenceEqual(new[]
        {
            "ALTER TABLE [dbo].[Child] DROP CONSTRAINT [FK_Child_Parent]",
            "DROP TABLE [dbo].[Parent]"
        }));  
    }
}
