using Ensync.Core.Abstract;

namespace Ensync.Core.DbObjects;

public class Column : DbObject
{
	public override DbObjectType Type => DbObjectType.Column;
	public string DataType { get; init; } = default!;
	public bool IsNullable { get; init; }
	public string? DefaultValue { get; init; }
	/// <summary>
	/// true when adding required column to table with data
	/// </summary>
	public bool IsDefaultRequired { get; init; }
	public int InternalId { get; init; }
	public int? Position { get; init; }

	public override (bool Result, string? Message) IsAltered(DbObject compareWith)
	{
		if (compareWith is Column column)
		{
			if (!DataType.Equals(column.DataType)) return (true, $"Data type changed from {column.DataType} to {DataType}");
			if (IsNullable != column.IsNullable) return (true, $"Nullability changed from {column.IsNullable} to {IsNullable}");
			if (DefaultValue != column.DefaultValue) return (true, $"Default value changed from {column.DefaultValue} to {DefaultValue}");
		}

		return (false, default);
	}

	public override IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema, List<ScriptAction> script)
	{
		List<(DbObject?, DbObject)> results = new();

		if (Parent is Table table)
		{
			results.AddRange(GetIndexes(table).Select(ndx => ((DbObject?)table, (DbObject)ndx)));

			// original code does another insert, but I don't know why
			// https://github.com/adamfoneil/ModelSync/blob/master/ModelSync.Library/Models/Column.cs#L131-L135

			var impactedFKs =
				schema.ForeignKeys.Where(fk => fk.Parent!.Equals(Parent) && fk.Columns.Any(col => col.ReferencingName.Equals(Name)))
				.ToArray();

			results.AddRange(impactedFKs.Select(fk => ((DbObject?)table, (DbObject)fk)));
		}

		return results;
	}

	private IEnumerable<Index> GetIndexes(Table table) => table.Indexes.Where(ndx => ndx.Columns.Any(col => col.Name.Equals(Name)));
}
