using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

public enum SortDirection
{
	Ascending,
	Descending
}

public enum IndexType
{
	PrimaryKey = 1,
	UniqueIndex = 2,
	UniqueConstraint = 3,
	NonUnique = 4
}

public class Index : DbObject
{
	public override DbObjectType Type => DbObjectType.Index;
	public IndexType IndexType { get; init; }
	public required IEnumerable<Column> Columns { get; set; }
	public int InternalId { get; init; }
	public bool IsClustered { get; init; }

	public override (bool Result, string? Message) IsAltered(DbObject compareWith)
	{
		if (compareWith is Index ndx)
		{
			if (IndexType != ndx.IndexType) return (true, $"Index type changed from {ndx.IndexType} to {IndexType}");

			var removedColumns = Columns.Select(col => col.Name).Except(ndx.Columns.Select(col => col.Name));
			if (removedColumns.Any()) return (true, $"Removed columns {string.Join(", ", removedColumns)}");

			var addedColumns = ndx.Columns.Select(col => col.Name).Except(Columns.Select(col => col.Name));
			if (addedColumns.Any()) return (true, $"Added columns {string.Join(", ", addedColumns)}");

			// todo: direction and order checks
		}

		return (false, default);
	}

	public class Column
	{
		public required string Name { get; init; }
		public SortDirection Direction { get; init; }
		public int Order { get; init; }
	}

	public override IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema, List<ScriptAction> script) =>
		(IndexType == IndexType.PrimaryKey || IndexType == IndexType.UniqueConstraint) ?
			schema.ForeignKeys.Where(
				fkInfo => fkInfo.ReferencedTable.Equals(Parent) && 
				Columns.Select(col => col.Name).Intersect(fkInfo.Columns.Select(fkCol => fkCol.ReferencedName)).Any())
				.Select(fkInfo => ((DbObject?)fkInfo.Parent, (DbObject)fkInfo)) :
			Enumerable.Empty<(DbObject?, DbObject)>();
}