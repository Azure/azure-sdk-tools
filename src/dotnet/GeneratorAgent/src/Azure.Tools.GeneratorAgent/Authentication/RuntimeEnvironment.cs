namespace Azure.Tools.GeneratorAgent.Authentication
{
    /// <summary>
    /// Represents the runtime environment where the application is executing.
    /// </summary>
    internal enum RuntimeEnvironment
    {
        /// <summary>
        /// Local development environment (developer machine).
        /// </summary>
        LocalDevelopment,

        /// <summary>
        /// DevOps pipeline environment (Azure DevOps CI/CD)
        /// </summary>
        DevOpsPipeline
    }
}
