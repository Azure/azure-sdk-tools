using System.CommandLine;
using System.CommandLine.Invocation;

namespace Azure.Sdk.Tools.Cli.Contract
{
    /// <summary>
    /// This is the base class defining how an MCP enabled tool will interface with the server.
    ///
    /// This covers:
    ///     - route registration/disambiguation
    ///     - compilation trim avoidance for reflection-included MCP tools
    /// </summary>
    public abstract class MCPTool
    {
        public MCPTool() { }

        public Command? Command;

        public int ExitCode { get; set; } = 0;

        public void SetFailure(int exitCode = 1)
        {
            ExitCode = exitCode;
        }

        public CommandGroup[] CommandHierarchy { get; set; } = Array.Empty<CommandGroup>();

        public abstract Command GetCommand();

        public abstract Task HandleCommand(InvocationContext ctx, CancellationToken ct);
    }
}
