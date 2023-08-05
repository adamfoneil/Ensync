using Ensync.Core.Abstract;

namespace Ensync.Core;

public partial class SqlServerScriptBuilder
{
    private string ForeignKeyDefinition(DbObject @object)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<(StatementPlacement, string)> CreateForeignKey(DbObject? object1, DbObject object2)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<(StatementPlacement, string)> DropForeignKey(DbObject? parent, DbObject child)
    {
        yield return (StatementPlacement.Immediate, $"ALTER TABLE {FormatName(parent!)} DROP CONSTRAINT {FormatName(child)}");
    }
}
