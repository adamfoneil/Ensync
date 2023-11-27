using Ensync.Core.Abstract;

namespace Ensync.Core.Extensions;

public static class ScriptActionExtensions
{
	public static IEnumerable<string> ToSqlStatements(this IEnumerable<ScriptAction> actions, SqlScriptBuilder scriptBuilder, bool allowDestruction = false) =>
		actions.SelectMany(a => a.Statements.Select(cmd => a.IsDestructive && !allowDestruction ? scriptBuilder.ToBlockComment(cmd.Sql) : cmd.Sql)).ToHashSet();

	public static string ToSqlScript(this IEnumerable<ScriptAction> actions, string separator, SqlScriptBuilder scriptBuilder, bool allowDestruction = false) =>
		string.Join(separator, ToSqlStatements(actions, scriptBuilder, allowDestruction));

	public static string[] ToCompactStatements(this IEnumerable<ScriptAction> actions) => actions.Select(sa => sa.ToString()).ToArray();
}
