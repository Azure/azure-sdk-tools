namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// A condition that can be used to apply an action based on the presence of a header.
    /// </summary>
    public class HeaderCondition
    {
        /// <summary>
        /// The header key that must be present for the condition to pass.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// An optional regex that can be applied to the value of the header.
        /// If the header contains multiple values, at least one of the value must match
        /// the regex for the condition to pass.
        /// </summary>
        public string ValueRegex
        {
            get => _valueRegex;
            set
            {
                StringSanitizer.ConfirmValidRegex(value);
                _valueRegex = value;
            }
        }

        private string _valueRegex;
    }
}