namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class HeaderCondition
    {
        public string Key { get; set; }

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