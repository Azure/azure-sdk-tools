namespace Azure.Tools.GeneratorAgent.Interfaces
{
    public interface IAppSettings
    {
        string ProjectEndpoint { get; }
        string Model { get; }
        string AgentName { get; }
        string AgentInstructions { get; }
    }
}