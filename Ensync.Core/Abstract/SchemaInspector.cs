using Ensync.Core.DbObjects;

namespace Ensync.Core.Abstract;

public abstract class SchemaInspector
{
	protected abstract Task<(IEnumerable<Table> Tables, IEnumerable<ForeignKey> ForeignKeys)> GetDbObjectsAsync();

	public async Task<Schema> GetSchemaAsync()
	{ 
		var objects = await GetDbObjectsAsync();

		return new()
		{
			Tables = objects.Tables,
			ForeignKeys = objects.ForeignKeys
		};
	}
}
