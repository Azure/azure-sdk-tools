using Azure.Tools.GeneratorAgent.Exceptions;

namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail.
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
        /// The process-specific exception if the operation failed with a process error
        /// </summary>
        public ProcessExecutionException? ProcessException { get; }

        /// <summary>
        /// The general exception if the operation failed with a non-process error
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Indicates whether the operation failed
        /// </summary>
        public bool IsFailure => !IsSuccess;

        private Result(bool isSuccess, T? value, ProcessExecutionException? processException = null, Exception? exception = null)
        {
            IsSuccess = isSuccess;
            Value = value;
            ProcessException = processException;
            Exception = exception;
        }

        // Success factory method
        public static Result<T> Success(T value) => 
            new(true, value);
        
        // Failure factory methods
        public static Result<T> Failure(ProcessExecutionException exception) => 
            new(false, default, processException: exception);

        public static Result<T> Failure(Exception exception) => 
            new(false, default, exception: exception);

        /// <summary>
        /// Throws the appropriate exception if operation failed
        /// </summary>
        public void ThrowIfFailure()
        {
            if (!IsFailure) return;

            if (ProcessException != null)
            {
                throw ProcessException;
            }
            
            if (Exception != null)
            {
                throw Exception;
            }
        }
    }
}
