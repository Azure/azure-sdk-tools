namespace Azure.Sdk.Tools.TestProxy.ConsoleWrapper
{
    /// <summary>
    /// IConsoleWrapper is just an interace around Console functions. This is necessary for testing
    /// functions, like Reset, which require user input that we need to be able to control.
    /// </summary>
    public interface IConsoleWrapper
    {
        void Write(string message);
        void WriteLine(string message);
        string ReadLine();
    }
}
