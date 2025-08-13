namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Defines the contract for SDK generation from TypeSpec sources.
    /// </summary>
    internal interface ISdkGenerationService
    {
        /// <summary>
        /// Compiles a TypeSpec project into an SDK.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A Result indicating success or failure with detailed error information</returns>
        Task<Result<object>> CompileTypeSpecAsync(CancellationToken cancellationToken = default);
    }
}