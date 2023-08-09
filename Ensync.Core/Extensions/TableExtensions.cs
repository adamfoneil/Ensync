using Ensync.Core.DbObjects;

namespace Ensync.Core.Extensions;

public static class TableExtensions
{
    public static IEnumerable<(Table Parent, ForeignKey ForeignKey)> GetForeignKeys(this IEnumerable<Table> tables) => 
        tables.SelectMany(tbl => tbl.ForeignKeys, (tbl, fk) => (tbl, fk));
}
