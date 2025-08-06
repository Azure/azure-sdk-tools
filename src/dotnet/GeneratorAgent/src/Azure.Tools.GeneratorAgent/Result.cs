namespace Azure.Tools.GeneratorAgent
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail.
    /// </summary>
    /// <typeparam name="T">The type of the success value.</typeparam>
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public T Value { get; }
        public string Error { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = string.Empty;
        }

        private Result(string error)
        {
            IsSuccess = false;
            Value = default(T)!;
            Error = error;
        }

        public static Result<T> Success(T value) => new(value);
        public static Result<T> Failure(string error) => new(error);

        // Implicit conversions for convenience
        public static implicit operator Result<T>(T value) => Success(value);
        public static implicit operator Result<T>(string error) => Failure(error);
    }

    /// <summary>
    /// Non-generic result for operations that don't return a value.
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public string Error { get; }
        public string Output { get; }
        public Exception? Exception { get; }

        private Result(bool isSuccess, string error, string output = "", Exception? exception = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Output = output;
            Exception = exception;
        }

        public static Result Success(string output = "") => new(true, string.Empty, output);
        public static Result Failure(string error, string output = "") => new(false, error, output);
        public static Result Failure(string error, Exception exception) => new(false, error, string.Empty, exception);
        public static Result Failure(Exception exception) => new(false, exception.Message, string.Empty, exception);

        /// <summary>
        /// Throws the original exception if one was captured, otherwise throws InvalidOperationException
        /// </summary>
        public void ThrowIfFailure()
        {
            if (IsFailure)
            {
                if (Exception != null)
                {
                    throw Exception;
                }
                throw new InvalidOperationException(Error);
            }
        }

        // Implicit conversion for convenience
        public static implicit operator Result(string error) => Failure(error);
    }
}
