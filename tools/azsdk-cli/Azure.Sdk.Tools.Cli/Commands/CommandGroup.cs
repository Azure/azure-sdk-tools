using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Commands;

public record CommandGroup(
    string Verb,
    string Description,
    List<Option>? Options = null
);
