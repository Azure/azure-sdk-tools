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
        /// <returns>A task representing the asynchronous compilation operation</returns>
        /// <exception cref="InvalidOperationException">Thrown when configuration or validation fails</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled</exception>
        Task CompileTypeSpecAsync(CancellationToken cancellationToken = default);
    }
}