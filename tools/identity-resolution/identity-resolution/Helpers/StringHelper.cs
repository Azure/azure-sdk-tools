namespace common.Helpers
{
    public static class StringHelper
    {
        public static string MaxLength(string input, int maxLength)
        {
            return input.Length > maxLength
                ? input.Substring(0, maxLength)
                : input;
        }
    }
}
