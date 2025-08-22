namespace Azure.Sdk.Tools.Cli.Microagents.Tools
{
    /// <summary>
    /// Let's you create a tool that's just a function or method.
    /// </summary>
    /// <typeparam name="ToolInputT">The input fields for your tool.</typeparam>
    /// <typeparam name="ToolOutputT">The output fields for your tool.</typeparam>
    /// <param name="name">The name of your tool.</param>
    /// <param name="description">The description of your tool.</param>
    /// <param name="invoke">The function that will be invoked, when this tool is used.</param>
    public class FuncAdapterTool<ToolInputT, ToolOutputT>(string name, string description, Func<ToolInputT, CancellationToken, Task<ToolOutputT>> invoke) : AgentTool<ToolInputT, ToolOutputT>
    {
        private readonly Func<ToolInputT, CancellationToken, Task<ToolOutputT>> invoke = invoke;

        public override string Name { get; init; } = name;
        public override string Description { get; init; } = description;

        public static AgentTool<ToolInputT, ToolOutputT> Create(string name, string description, Func<ToolInputT, CancellationToken, Task<ToolOutputT>> invokeHandler)
        {
            return new FuncAdapterTool<ToolInputT, ToolOutputT>(name, description, invokeHandler);
        }

        public override Task<ToolOutputT> Invoke(ToolInputT input, CancellationToken ct)
        {
            return invoke(input, ct);
        }
    }
}
