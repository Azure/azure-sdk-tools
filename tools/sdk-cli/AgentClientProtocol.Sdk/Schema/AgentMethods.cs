// Agent Client Protocol - .NET SDK
// Methods that clients call on agents

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Method names for requests that clients send to agents.
/// </summary>
public static class AgentMethods
{
    public const string Initialize = "initialize";
    public const string Authenticate = "authenticate";
    
    // Session methods
    public const string SessionNew = "session/new";
    public const string SessionLoad = "session/load";
    public const string SessionList = "session/list";
    public const string SessionFork = "session/fork";
    public const string SessionResume = "session/resume";
    public const string SessionPrompt = "session/prompt";
    public const string SessionCancel = "session/cancel";
    public const string SessionSetMode = "session/set_mode";
    public const string SessionSetModel = "session/set_model";
    public const string SessionSetConfigOption = "session/set_config_option";
}
