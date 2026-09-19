namespace DeckState.StreamDeck;

public sealed record StreamDeckPluginArguments(int Port, string PluginUuid, string RegisterEvent)
{
    public static StreamDeckPluginArguments Parse(string[] args)
    {
        string Value(string name) => args.SkipWhile(value => value != name).Skip(1).FirstOrDefault()
            ?? throw new ArgumentException($"Missing {name}.");
        return new(int.Parse(Value("-port")), Value("-pluginUUID"), Value("-registerEvent"));
    }
}
