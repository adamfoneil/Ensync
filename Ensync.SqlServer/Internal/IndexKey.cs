namespace Ensync.SqlServer.Internal;

internal class IndexKey
{
	public int object_id { get; set; }
	public int index_id { get; set; }

	public override bool Equals(object? obj)
	{
		if (obj is IndexKey indexKey)
		{
			return indexKey.object_id == object_id && indexKey.index_id == index_id;
		}

		return false;
	}

	public override int GetHashCode()
	{
		return (object_id + index_id).GetHashCode();
	}
}
