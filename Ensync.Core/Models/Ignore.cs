namespace Ensync.Core.Models;

public class Ignore
{
    public ScriptActionKey[] Actions { get; set; } = Array.Empty<ScriptActionKey>();

    public IEnumerable<ScriptAction> ToScriptActions() => Actions.Select(a => a.ToScriptAction());
}
