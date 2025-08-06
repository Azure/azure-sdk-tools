// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;

// Just a lil easter egg :)

namespace Azure.Sdk.Tools.Cli.Tools;

public class QuokkaTool(IOutputService output) : MCPTool
{
    public override Command GetCommand()
    {
        Command command = new("quokka") { IsHidden = true };
        command.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        return command;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        var result = GetQuokkaAnsi();
        output.Output(result);
        await Task.CompletedTask;
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
