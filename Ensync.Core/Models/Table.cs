using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class Table : DbObject
{
    public override DbObjectType Type => DbObjectType.Table;

    public HashSet<Column> Columns { get; init; } = new();
    public HashSet<Index> Indexes { get; init; } = new();
    public HashSet<ForeignKey> ForeignKeys { get; init; } = new();
    public HashSet<CheckConstraint> CheckConstraints { get; init; } = new();    

    public long RowCount { get; init; }

    public override IEnumerable<(DbObject? Parent, DbObject Child)> GetDependencies(Schema schema) =>
        schema.Tables
           .SelectMany(
                t => t.ForeignKeys.Where(fk => fk.ReferencedTable.Equals(this)),
                (t, fk) => ((DbObject?)t, (DbObject)fk));
}
