using Ensync.Core.Abstract;
using Ensync.Core.Models;

namespace Ensync.Core;

public partial class SqlServerScriptBuilder
{
    private string ForeignKeyDefinition(DbObject @object)
    {
        var fk = @object as ForeignKey ?? throw new Exception("Unexpected objec type");
        string referencingColumns = string.Join(", ", fk.Columns.Select(col => FormatName(col.ReferencingName)));
        string referencedColumns = string.Join(", ", fk.Columns.Select(col => FormatName(col.ReferencedName)));
        var result = $"{FormatName(fk)} FOREIGN KEY ({referencingColumns}) REFERENCES {FormatName(fk.ReferencedTable)} ({referencedColumns})";
        if (fk.CascadeDelete) result += " ON DELETE CASCADE";
        if (fk.CascadeUpdate) result += " ON UPDATE CASCADE";
        return result;
    }

    private IEnumerable<(StatementPlacement, string)> CreateForeignKey(DbObject? parent, DbObject child)
    {
        yield return (StatementPlacement.Deferred, $"ALTER TABLE {FormatName(parent!)} ADD CONSTRAINT {ForeignKeyDefinition(child)}");
    }

    private IEnumerable<(StatementPlacement, string)> DropForeignKey(DbObject? parent, DbObject child)
    {
        yield return (StatementPlacement.Immediate, $"ALTER TABLE {FormatName(parent!)} DROP CONSTRAINT {FormatName(child)}");
    }
}
