namespace Ensync.SqlServer.Internal;

internal class IndexColumnResult
{
	public int object_id { get; set; }
	public int index_id { get; set; }
	public string name { get; set; } = default!;
	public byte key_ordinal { get; set; }
	public bool is_descending_key { get; set; }
}
