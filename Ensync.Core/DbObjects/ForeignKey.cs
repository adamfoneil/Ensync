using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

public class ForeignKey : DbObject
{
	public override DbObjectType Type => DbObjectType.ForeignKey;
	public required Table ReferencedTable { get; init; }
	public bool CascadeDelete { get; init; }
	public bool CascadeUpdate { get; init; }
	public required IEnumerable<Column> Columns { get; init; }

	public class Column
	{
		public required string ReferencedName { get; init; }
		public required string ReferencingName { get; init; }
	}
}