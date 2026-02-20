// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;

// Just a lil easter egg :)

namespace Azure.Sdk.Tools.Cli.Tools.Example;

// NOTE: Other tool classes should not inject output helper directly
public class QuokkaTool(IOutputHelper output) : MCPTool
{
    protected override Command GetCommand() => new("quokka") { Hidden = true };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var result = GetQuokkaAnsi();
        output.Output(result);
        return await Task.FromResult<CommandResponse>(new DefaultCommandResponse());
    }

    public string GetQuokkaAnsi()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var names = assembly.GetManifestResourceNames();
            using var stream = assembly.GetManifestResourceStream("Azure.Sdk.Tools.Cli.Images.quokka.ansi");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception)
        {
            return "Quokka is unavailable T-T";
        }
    }
}
