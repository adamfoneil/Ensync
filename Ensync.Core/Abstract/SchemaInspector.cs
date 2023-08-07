namespace Ensync.Core.Abstract;

public abstract class SchemaInspector
{
    protected abstract Task<IEnumerable<DbObject>> GetDbObjectsAsync();

    public async Task<Dictionary<int, DbObject>> GetSchemaAsync()
    { 
        var objects = await GetDbObjectsAsync();
        return objects.ToDictionary(obj => obj.ObjectId, obj => obj);
    }
}
