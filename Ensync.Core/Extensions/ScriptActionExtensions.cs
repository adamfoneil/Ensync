using Ensync.Core.Abstract;

namespace Ensync.Core.Extensions;

public static class ScriptActionExtensions
{
    public static IEnumerable<string> ToSqlStatements(this IEnumerable<ScriptAction> actions) =>
        GetStatements(actions, StatementPlacement.Immediate).Concat(GetStatements(actions, StatementPlacement.Deferred));

    public static string ToSqlScript(this IEnumerable<ScriptAction> actions, string separator) =>
        string.Join(separator, ToSqlStatements(actions));
    
    private static IEnumerable<string> GetStatements(IEnumerable<ScriptAction> actions, StatementPlacement placement) =>
        actions.SelectMany(a => a.Statements.Where(st => st.Item1 == placement)).Select(st => st.Item2);
}
