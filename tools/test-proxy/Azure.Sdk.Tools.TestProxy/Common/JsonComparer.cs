using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class JsonComparer
    {
        public static List<string> CompareJson(byte[] json1, byte[] json2)
        {
            var differences = new List<string>();
            using var doc1 = ParseJson(json1, "request", differences);
            if (doc1 == null)
            {
                return differences;
            }

            using var doc2 = ParseJson(json2, "record", differences);
            if (doc2 != null)
            {
                CompareElements(doc1.RootElement, doc2.RootElement, differences, "");
            }

            return differences;
        }

        internal static bool AreEqual(byte[] json1, byte[] json2)
        {
            using var doc1 = ParseJson(json1, "request", null);
            if (doc1 == null)
            {
                return false;
            }

            using var doc2 = ParseJson(json2, "record", null);
            return doc2 != null && CompareElements(doc1.RootElement, doc2.RootElement, null, null);
        }

        private static JsonDocument ParseJson(byte[] json, string description, List<string> differences)
        {
            try
            {
                return JsonDocument.Parse(json);
            }
            catch (Exception exception)
            {
                differences?.Add($"Unable to parse the {description} json body. Content \"{Encoding.UTF8.GetString(json)}.\" Exception: {exception.Message}");
                return null;
            }
        }

        private static bool CompareElements(JsonElement element1, JsonElement element2, List<string> differences, string path)
        {
            if (element1.ValueKind != element2.ValueKind)
            {
                differences?.Add($"{path}: Request and record have different types.");
                return false;
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

                        if (differences == null && propDict1.Count != propDict2.Count)
                        {
                            return false;
                        }

                        bool equal = true;
                        foreach (var property in propDict1)
                        {
                            bool propertyEqual;
                            if (propDict2.TryGetValue(property.Key, out var otherValue))
                            {
                                propertyEqual = CompareElements(property.Value, otherValue, differences,
                                    differences == null ? null : $"{path}.{property.Key}");
                            }
                            else
                            {
                                differences?.Add($"{path}.{property.Key}: Missing in request JSON");
                                propertyEqual = false;
                            }

                            if (!propertyEqual)
                            {
                                if (differences == null)
                                {
                                    return false;
                                }
                                equal = false;
                            }
                        }

                        foreach (var key in propDict2.Keys)
                        {
                            if (!propDict1.ContainsKey(key))
                            {
                                differences?.Add($"{path}.{key}: Missing in record JSON");
                                equal = false;
                            }
                        }

                        return equal;
                    }
                case JsonValueKind.Array:
                    {
                        if (differences == null && element1.GetArrayLength() != element2.GetArrayLength())
                        {
                            return false;
                        }

                        int index = 0;
                        bool equal = true;
                        var enum1 = element1.EnumerateArray();
                        var enum2 = element2.EnumerateArray();

                        while (enum1.MoveNext())
                        {
                            if (enum2.MoveNext())
                            {
                                if (!CompareElements(enum1.Current, enum2.Current, differences,
                                    differences == null ? null : $"{path}[{index}]"))
                                {
                                    if (differences == null)
                                    {
                                        return false;
                                    }
                                    equal = false;
                                }
                            }
                            else
                            {
                                differences?.Add($"{path}[{index}]: Extra element in request JSON");
                                equal = false;
                            }
                            index++;
                        }

                        while (enum2.MoveNext())
                        {
                            differences?.Add($"{path}[{index}]: Extra element in record JSON");
                            equal = false;
                            index++;
                        }

                        return equal;
                    }
                case JsonValueKind.String:
                    {
                        if (element1.GetString() != element2.GetString())
                        {
                            differences?.Add($"{path}: \"{element1.GetString()}\" != \"{element2.GetString()}\"");
                            return false;
                        }
                        return true;
                    }
                case JsonValueKind.Number:
                    {
                        if (element1.GetDecimal() != element2.GetDecimal())
                        {
                            differences?.Add($"{path}: {element1.GetDecimal()} != {element2.GetDecimal()}");
                            return false;
                        }
                        return true;
                    }
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    return true;
                default:
                    {
                        differences?.Add($"{path}: Unhandled value kind {element1.ValueKind}");
                        return false;
                    }
            }
        }
    }
}
