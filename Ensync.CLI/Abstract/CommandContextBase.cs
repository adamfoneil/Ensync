namespace Ensync.CLI.Abstract;

internal abstract class CommandContextBase
{
    private readonly string[] _args;

    public CommandContextBase(string[] args)
    {
        _args = args;        
        Initialize();
    }

    protected abstract void Initialize();
    
    protected string ParseArgument(int index, string defaultValue) => _args.Length >= index + 1 ? _args[index] : defaultValue;
}
