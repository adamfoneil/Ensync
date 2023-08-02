using Ensync.Core.Abstract;

namespace Ensync.Core;

public class SchemaComparer
{
    public IEnumerable<ScriptAction> GetDiffScript(IEnumerable<DbObject> sourceObjects,  IEnumerable<DbObject> targetObjects)
    {
        throw new NotImplementedException();
    }
}
