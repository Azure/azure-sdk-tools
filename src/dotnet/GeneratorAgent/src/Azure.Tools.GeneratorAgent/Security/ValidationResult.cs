namespace Azure.Tools.GeneratorAgent.Security
{
    /// <summary>
    /// Represents the result of input validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string Value { get; private set; }
        public string ErrorMessage { get; private set; }

        private ValidationResult(bool isValid, string value, string errorMessage)
        {
            IsValid = isValid;
            Value = value;
            ErrorMessage = errorMessage;
        }

        public static ValidationResult Valid(string value) => new(true, value, string.Empty);
        public static ValidationResult Invalid(string errorMessage) => new(false, string.Empty, errorMessage);
    }
}
