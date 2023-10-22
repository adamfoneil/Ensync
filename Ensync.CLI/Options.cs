using CommandLine;

namespace Ensync.CLI;

public enum Action
{
	/// <summary>
	/// show sql statements in console only
	/// </summary>
	Preview,
	/// <summary>
	/// execute SQL statements
	/// </summary>
	Merge,
	/// <summary>
	/// view .sql file for manual inspection and running
	/// </summary>
	Script,
	/// <summary>
	/// add a script action to the ignore list
	/// </summary>
	Ignore,
	/// <summary>
	/// creates a zip file of source and dest schemas along with the generated statements for later inspection
	/// </summary>
	CaptureTestCase,
	/// <summary>
	/// save the source and dest models as markdown along with the SQL script for manual comparison and debugging purposes
	/// </summary>
	Debug
}

internal class Options
{
	[Option('i', "init", HelpText = "Creates a blank config file in the current directory")]
	public bool Init { get; set; }

	[Option('c', "config", Required = false, Default = ".", HelpText = "Path to config file. Omit to use current directory")]
	public string ConfigPath { get; set; } = default!;

	[Option('s', "source", HelpText = "Leave blank to merge from the project Assembly or specify a database target")]
	public string Source { get; set; } = default!;

	[Option('t', "target", Required = false, HelpText = "Named database target to merge to. Omit to use the first one in your config file")]
	public string DbTarget { get; set; } = default!;

	[Option('a', "action", Required = false, Default = "Preview", HelpText = "Action to perform")]
	public string ActionName { get; set; } = default!;

	[Option('m', "merge", HelpText = "Executes the merge script. Same as setting action = Merge")]
	public bool Merge { get; set; }

	[Option('r', "script", HelpText = "Builds a .sql script file and launches it. Same as setting action = Script")]
	public bool Script { get; set; }

	[Option('d', "debug", HelpText = "Emits comments that help explain how SQL is being generated")]
	public bool Debug { get; set; }

	public Action Action => Enum.Parse<Action>(ActionName);
	public bool UseAssemblySource => string.IsNullOrWhiteSpace(Source);
}
