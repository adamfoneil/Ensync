using CommandLine;

namespace Ensync.CLI;

public enum Action
{
	/// <summary>
	/// show sql statements in console only
	/// </summary>
	ScriptOnly,
	/// <summary>
	/// execute SQL statements
	/// </summary>
	Merge,
	/// <summary>
	/// view .sql file for manual inspection and running
	/// </summary>
	LaunchSqlFile,
	/// <summary>
	/// add a script action to the ignore list
	/// </summary>
	Ignore
}

internal class Options
{
	[Option('c', "config", Required = false, Default = ".", HelpText = "Path to config file. Omit to use current directory")]
	public string ConfigPath { get; set; } = default!;

	[Option('t', "target", Required = false, HelpText = "Named database target to merge to. Omit to use the first one in your config file")]
	public string DbTarget { get; set; } = default!;

	[Option('a', "action", Required = false, Default = "ScriptOnly", HelpText = "Action to perform")]
	public string ActionName { get; set; } = default!;

	public Action Action => Enum.Parse<Action>(ActionName);
}
