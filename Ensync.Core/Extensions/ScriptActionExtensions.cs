using Ensync.Core.Abstract;

namespace Ensync.Core.Extensions;

public static class ScriptActionExtensions
{
    public static IEnumerable<string> ToSqlStatements(this IEnumerable<ScriptAction> actions) =>
        actions.SelectMany(a => a.Statements);

    public static string ToSqlScript(this IEnumerable<ScriptAction> actions, string separator) =>
        string.Join(separator, ToSqlStatements(actions));    
}
