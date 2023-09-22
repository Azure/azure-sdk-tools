namespace Cdk.Core
{
    internal static class Extensions
    {
        public static string ToCamelCase(this string str)
        {
            return char.ToLowerInvariant(str[0]) + str[1..];
        }
    }
}
