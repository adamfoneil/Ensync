using Ensync.Core.Abstract;
using Ensync.Core.Extensions;

namespace Ensync.Core.Models;

public class Schema
{
    public IEnumerable<Table> Tables { get; set; } = Enumerable.Empty<Table>();

    public IEnumerable<(Table Parent, ForeignKey ForeignKey)> ForeignKeys => Tables.SelectMany(tbl => tbl.ForeignKeys, (tbl, fk) => (tbl, fk));

    public async Task<IEnumerable<ScriptAction>> CompareAsync(Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {       
        ArgumentNullException.ThrowIfNull(nameof(targetSchema));
        ArgumentNullException.ThrowIfNull(nameof(scriptBuilder));

        await scriptBuilder.InspectTargetDatabaseAsync();

        SetParents(this);
        SetParents(targetSchema);

        List<ScriptAction> results = new();

        CreateTables(results, Tables, targetSchema, scriptBuilder);

        AddColumns(results, Tables, targetSchema, scriptBuilder);
        // AddIndexes
        // AddChecks
        AddForeignKeys(results, Tables, targetSchema, scriptBuilder);

        // AlterColumns
        // AlterIndexes
        // AlterChecks
        // AlterForeignKeys

        // DropTables
        // DropColumns
        // DropIndexes
        // DropForeignKeys

        return results;
    }

    public async Task<IEnumerable<ScriptAction>> CreateAsync(SqlScriptBuilder scriptBuilder)
    {
        var target = new Schema();
        return await CompareAsync(target, scriptBuilder);        
    }

    public async Task<string> CreateScriptAsync(SqlScriptBuilder scriptBuilder, string separator)
    {
        var script = await CreateAsync(scriptBuilder);
        return script.ToSqlScript(separator);
    }

    /// <summary>
    /// certain script syntax requires access to the object parent 
    /// (e.g. indexes have an "on" clause that references the parent table explicitly).
    /// object Parent properties aren't set necessarily in the original graph, so they're set here
    /// </summary>    
    private static void SetParents(Schema schema)
    {
        foreach (var table in schema.Tables)
        {
            foreach (var col in table.Columns) col.Parent ??= table;
            foreach (var index in table.Indexes) index.Parent ??= table;
            foreach (var fk in table.ForeignKeys) fk.Parent ??= table;
            foreach (var chk in table.CheckConstraints) chk.Parent ??= table;
        }
    }

    private void AddForeignKeys(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var fks = sourceTables.SelectMany(t => t.ForeignKeys).ToArray();
        var fks1 = fks.Where(ReferencedTableCreatedOrExists);
        var fks2 = fks1.Except(TargetForeignKeys());

        results.AddRange(sourceTables.SelectMany(tbl => tbl.ForeignKeys
            .Where(ReferencedTableCreatedOrExists)
            .Except(TargetForeignKeys())
            .Select(fk => new ScriptAction(ScriptActionType.Create, fk)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, tbl, fk)
        })));

        bool ReferencedTableCreatedOrExists(ForeignKey key)
        {
            if (results.Any(sa => sa.Object.Equals(key.ReferencedTable))) return true;
            if (scriptBuilder.Metadata.Tables.Contains(key.ReferencedTable.Name)) return true;

            return false;
        }

        IEnumerable<ForeignKey> TargetForeignKeys() => targetSchema.Tables.SelectMany(tbl => tbl.ForeignKeys);
    }    

    private void CreateTables(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        results.AddRange(sourceTables.Except(targetSchema.Tables).Select(tbl => new ScriptAction(ScriptActionType.Create, tbl)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, null, tbl)
        }));
    }

    private void AddColumns(List<ScriptAction> results, IEnumerable<Table> sourceTables, Schema targetSchema, SqlScriptBuilder scriptBuilder)
    {
        var commonTables = sourceTables.Join(targetSchema.Tables, source => source, target => target, (source, target) => new
        {
            Source = source,
            Target = target
        });

        results.AddRange(commonTables.SelectMany(tablePair => tablePair.Source.Columns.Except(tablePair.Target.Columns).Select(col => new ScriptAction(ScriptActionType.Create, col)
        {
            Statements = scriptBuilder.GetScript(ScriptActionType.Create, targetSchema, tablePair.Source, col)
        })));
    }
}
