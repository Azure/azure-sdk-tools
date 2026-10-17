using System.Collections.Generic;
using System.Text;

namespace APIViewWeb.Helpers
{
    public class CosmosQueryHelpers
    {
        public static string ArrayToQueryString<T>(IEnumerable<T> items)
        {
            var result = new StringBuilder();
            result.Append("(");
            foreach (var item in items)
            {
                if (item is int)
                {
                    result.Append($"{item},");
                }
                else
                {
                    result.Append($"\"{item}\",");
                }

            }
            if (result[result.Length - 1] == ',')
            {
                result.Remove(result.Length - 1, 1);
            }
            result.Append(")");
            return result.ToString();
        }
    }
}
