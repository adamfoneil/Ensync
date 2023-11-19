using Ensync.Core.DbObjects;

namespace Ensync.Core.Abstract;

public abstract class SchemaInspector
{
	protected abstract Task<(IEnumerable<Table> Tables, IEnumerable<ForeignKey> ForeignKeys)> GetDbObjectsAsync();

	public async Task<Schema> GetSchemaAsync()
	{
		var (Tables, ForeignKeys) = await GetDbObjectsAsync();

		return new()
		{
			Tables = Tables,
			ForeignKeys = ForeignKeys
		};
	}
}
