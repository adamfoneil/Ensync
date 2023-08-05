using Ensync.Core.Abstract;

namespace Ensync.Core;

public partial class SqlServerScriptBuilder
{
    private string IndexDefinition(DbObject @object)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<(StatementPlacement, string)> CreateIndex(DbObject? parent, DbObject child)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<(StatementPlacement, string)> AlterIndex(DbObject? object1, DbObject object2)
    {
        throw new NotImplementedException();
    }

    private IEnumerable<(StatementPlacement, string)> DropIndex(DbObject? object1, DbObject object2)
    {
        throw new NotImplementedException();
    }
}
