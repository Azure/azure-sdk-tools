using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class JsonComparer
    {
        public static List<string> CompareJson(byte[] json1, byte[] json2)
        {
            var differences = new List<string>();
            JsonDocument doc1;
            JsonDocument doc2;

            // Deserialize the byte arrays to JsonDocument
            try
            {
                doc1 = JsonDocument.Parse(json1);
            }
            catch(Exception ex)
            {
                differences.Add($"Unable to parse the request json body. Content \"{Encoding.UTF8.GetString(json1)}.\" Exception: {ex.Message}");
                return differences;
            }

            try
            {
                doc2 = JsonDocument.Parse(json2);
            }
            
            catch (Exception ex)
            {
                differences.Add($"Unable to parse the record json body. Content \"{Encoding.UTF8.GetString(json2)}.\" Exception: {ex.Message}");
                return differences;
            }

            CompareElements(doc1.RootElement, doc2.RootElement, differences, "");

            return differences;
        }

        private static void CompareElements(JsonElement element1, JsonElement element2, List<string> differences, string path)
        {
            if (element1.ValueKind != element2.ValueKind)
            {
                differences.Add($"{path}: Request and record have different types.");
                return;
            }

            switch (element1.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        var properties1 = element1.EnumerateObject();
                        var properties2 = element2.EnumerateObject();

                        var propDict1 = new Dictionary<string, JsonElement>();
                        var propDict2 = new Dictionary<string, JsonElement>();

                        foreach (var prop in properties1)
                            propDict1[prop.Name] = prop.Value;

                        foreach (var prop in properties2)
                            propDict2[prop.Name] = prop.Value;

                        foreach (var key in propDict1.Keys)
                        {
                            if (propDict2.ContainsKey(key))
                            {
                                CompareElements(propDict1[key], propDict2[key], differences, $"{path}.{key}");
                            }
                            else
                            {
                                differences.Add($"{path}.{key}: Missing in record JSON");
                            }
                        }

                        foreach (var key in propDict2.Keys)
                        {
                            if (!propDict1.ContainsKey(key))
                            {
                                differences.Add($"{path}.{key}: Missing in request JSON");
                            }
                        }

                        break;
                    }
                case JsonValueKind.Array:
                    {
                        // Use index-based iteration to avoid double-advancing enumerators
                        int len1 = element1.GetArrayLength();
                        int len2 = element2.GetArrayLength();
                        int common = Math.Min(len1, len2);

                        for (int i = 0; i < common; i++)
                        {
                            CompareElements(element1[i], element2[i], differences, $"{path}[{i}]");
                        }

                        for (int i = common; i < len1; i++)
                        {
                            differences.Add($"{path}[{i}]: Extra element in request JSON");
                        }

                        for (int i = common; i < len2; i++)
                        {
                            differences.Add($"{path}[{i}]: Extra element in record JSON");
                        }

                        break;
                    }
                case JsonValueKind.String:
                    {
                        if (element1.GetString() != element2.GetString())
                        {
                            differences.Add($"{path}: \"{element1.GetString()}\" != \"{element2.GetString()}\"");
                        }
                        break;
                    }
                case JsonValueKind.Number:
                    {
                        if (element1.GetDecimal() != element2.GetDecimal())
                        {
                            differences.Add($"{path}: {element1.GetDecimal()} != {element2.GetDecimal()}");
                        }
                        break;
                    }
                case JsonValueKind.True:
                case JsonValueKind.False:
                    {
                        if (element1.GetBoolean() != element2.GetBoolean())
                        {
                            differences.Add($"{path}: {element1.GetBoolean()} != {element2.GetBoolean()}");
                        }
                        break;
                    }
                case JsonValueKind.Null:
                    {
                        // Both are null, nothing to compare
                        break;
                    }
                default:
                    {
                        differences.Add($"{path}: Unhandled value kind {element1.ValueKind}");
                        break;
                    }
            }
        }
    }
}
