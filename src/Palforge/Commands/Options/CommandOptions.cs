namespace Palforge.Commands.Options;

internal sealed class CommandOptions
{
    public const string SectionName = "Commands";

    public required string Prefix { get; set; }

    public required string SenderName { get; set; }
}