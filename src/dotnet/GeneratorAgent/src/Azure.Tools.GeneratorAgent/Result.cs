using Azure.Tools.GeneratorAgent.Exceptions;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail.
    /// The class is specifically used for return type of processes where the errors are recoverable using OpenAI
    /// </summary>
    /// <typeparam name="T">The type of the success value. Use object for operations that don't return a specific value.</typeparam>
    internal record Result<T>
    {
        /// <summary>
        /// Indicates whether the operation succeeded
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// The value if the operation succeeded
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// The process-specific RECOVERABLE exception if the operation failed with a process error
        /// </summary>
        public ProcessExecutionException? ProcessException { get; }

        /// <summary>
        /// Indicates whether the operation failed
        /// </summary>
        public bool IsFailure => !IsSuccess;

        private Result(bool isSuccess, T? value, ProcessExecutionException? processException = null)
        {
            if (isSuccess && processException != null)
                throw new ArgumentException("Success result cannot have a ProcessExecutionException");
            
            if (!isSuccess && processException == null)
                throw new ArgumentException("Failure result must have a ProcessExecutionException");
            
            IsSuccess = isSuccess;
            Value = value;
            ProcessException = processException;
        }

        // Success factory method
        public static Result<T> Success(T value) => 
            new(true, value);
        
        // Failure factory methods
        public static Result<T> Failure(ProcessExecutionException exception) => 
            new(false, default, processException: exception);
    }
}
