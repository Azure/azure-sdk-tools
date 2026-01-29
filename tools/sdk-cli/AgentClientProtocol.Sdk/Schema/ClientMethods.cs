// Agent Client Protocol - .NET SDK
// Methods that agents call on clients

namespace AgentClientProtocol.Sdk.Schema;

/// <summary>
/// Method names for requests that agents send to clients.
/// </summary>
public static class ClientMethods
{
    // File system methods
    public const string FsReadTextFile = "fs/read_text_file";
    public const string FsWriteTextFile = "fs/write_text_file";
    
    // Session methods
    public const string SessionRequestPermission = "session/request_permission";
    public const string SessionRequestInput = "session/request_input";
    public const string SessionUpdate = "session/update";
    
    // Terminal methods
    public const string TerminalCreate = "terminal/create";
    public const string TerminalOutput = "terminal/output";
    public const string TerminalRelease = "terminal/release";
    public const string TerminalWaitForExit = "terminal/wait_for_exit";
    public const string TerminalKill = "terminal/kill";
}
