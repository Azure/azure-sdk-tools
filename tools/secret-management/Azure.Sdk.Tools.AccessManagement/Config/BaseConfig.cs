using System.Reflection;
using System.Text.RegularExpressions;

namespace Azure.Sdk.Tools.AccessManagement;

public abstract class BaseConfig
{
    private (string, HashSet<string>) RenderValue(string template, IDictionary<string, string> properties)
    {
        var rendered = template;
        var unrendered = new HashSet<string>();
        var matches = Regex.Matches(template, @"{{\s*([a-zA-Z0-9_-]+)\s*}}");
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            if (properties.ContainsKey(key))
            {
                var query = @"{{\s*" + key + @"\s*}}";
                rendered = Regex.Replace(rendered, query, properties[key]);
            }
            else
            {
                unrendered.Add(key);
            }
        }

        return (rendered, unrendered);
    }

    public HashSet<string> Render(IDictionary<string, string> properties)
    {
        var allUnrendered = new HashSet<string>();
        var unrendered = new HashSet<string>();
        foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var obj = property.GetValue(this);

            if (obj is null)
            {
                return new HashSet<string>();
            }
            else if (obj is string str)
            {
                (var rendered, unrendered) = RenderValue(str, properties);
                property.SetValue(this, rendered);
                allUnrendered.UnionWith(unrendered);
            }
            else if (obj is List<string> list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    (list[i], unrendered) = RenderValue(list[i], properties);
                    allUnrendered.UnionWith(unrendered);
                }
            }
            else if (obj is IDictionary<string, string> dict)
            {
                foreach (var key in dict.Keys.ToList())
                {
                    (dict[key], unrendered) = RenderValue(dict[key], properties);
                    allUnrendered.UnionWith(unrendered);
                }
            }
        }

        return allUnrendered;
    }
}
