namespace Azure.Tools.GeneratorAgent.Authentication
{
    /// <summary>
    /// Represents the runtime environment where the application is executing.
    /// </summary>
    public enum RuntimeEnvironment
    {
        /// <summary>
        /// Local development environment (developer machine).
        /// </summary>
        LocalDevelopment,

        /// <summary>
        /// DevOps pipeline environment (CI/CD, GitHub Actions, etc.).
        /// </summary>
        DevOpsPipeline
    }
}
