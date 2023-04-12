using System.Reflection;
using System.Text.RegularExpressions;

public abstract class BaseConfig
{
    public void Render(Dictionary<string, string> properties)
    {
        foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            foreach (var prop in properties)
            {
                var val = property.GetValue(this);
                var query = @"{{\s*" + prop.Key + @"\s*}}";

                if (val is null)
                {
                    continue;
                }

                var str = val as string;
                if (str is not null && Regex.IsMatch(str, query))
                {
                    property.SetValue(this, Regex.Replace(str, query, prop.Value.ToString()));
                }

                var list = val as List<string>;
                if (list?.Count > 0)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (Regex.IsMatch(list[i], query))
                        {
                            list[i] = Regex.Replace(list[i], query, prop.Value.ToString());
                        }
                    }
                }

                var dict = val as Dictionary<string, string>;
                if (dict is not null && dict.Count > 0)
                {
                    foreach (var key in dict.Keys)
                    {
                        if (Regex.IsMatch(dict[key], query))
                        {
                            dict[key] = Regex.Replace(dict[key], query, prop.Value.ToString());
                        }
                    }
                }
            }
        }
    }
}