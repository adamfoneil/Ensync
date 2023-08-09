using Ensync.Core.DbObjects;

namespace Ensync.Core.Abstract;

public abstract class SchemaInspector
{
	protected abstract Task<IEnumerable<DbObject>> GetDbObjectsAsync();

	public async Task<Schema> GetSchemaAsync()
	{ 
		var objects = await GetDbObjectsAsync();

		return new()
		{
			Tables = objects.OfType<Table>()
		};
	}
}
