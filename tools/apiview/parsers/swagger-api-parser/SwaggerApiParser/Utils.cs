using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using SwaggerApiParser.Specs;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser
{
    public struct PathNode
    {
        public readonly string CommonPath;
        public readonly List<PathNode> Children;
        public readonly int Level;
        public bool Leaf;

        public PathNode(string commonPath, List<PathNode> children, int level, bool leaf)
        {
            this.Children = children;
            this.CommonPath = commonPath;
            this.Level = level;
            this.Leaf = leaf;
        }
    }

    public static class Utils
    {
        public static string GetCommonPath(IEnumerable<string> paths)
        {
            var pathsArray = new List<List<string>>() { };
            if (pathsArray == null)
            {
                throw new ArgumentNullException(nameof(pathsArray));
            }

            pathsArray.AddRange(paths.Select(path => path.Split('/')).Select(pathParts => new List<string>(pathParts)));

            var commonPathList = new List<string>() { };

            bool found = false;
            var idx = 0;
            string commonPath = "";
            while (!found)
            {
                if (idx < pathsArray[0].Count)
                {
                    commonPath = pathsArray[0][idx];
                }

                if (pathsArray.Any(it => idx >= it.Count || it[idx] != commonPath))
                {
                    found = true;
                }

                if (found)
                {
                    continue;
                }

                idx++;
                commonPathList.Add(commonPath);
            }

            return string.Join("/", commonPathList);
        }

        public static string GetResourceProviderFromPath(string path)
        {
            const string resourceProviderPattern = "/providers/(:?[^{/]+)";
            var match = Regex.Match(path, resourceProviderPattern, RegexOptions.RightToLeft);
            return match.Success ? match.Groups[1].Value : "";
        }

        public static PathNode BuildPathTree(IEnumerable<string> paths)
        {
            var sortedPath = paths.OrderBy(s => s);

            PathNode root = new PathNode("", new List<PathNode>() { }, level: 0, leaf: false);
            foreach (var path in sortedPath)
            {
                root.Children.Add(new PathNode(commonPath: path,
                    children: new List<PathNode>(), level: 1, leaf: true));
            }

            return BuildPathTreeInternal(root, 0);
        }

        public static string VisualizePathTree(PathNode root)
        {
            Queue<PathNode> qu = new Queue<PathNode>();
            qu.Enqueue(root);
            string res = "";
            while (qu.TryDequeue(out var cur))
            {
                Console.WriteLine($"{cur.Level}, {cur.CommonPath}");
                res += $"{cur.Level}, {cur.CommonPath}\n";
                foreach (var child in cur.Children)
                {
                    qu.Enqueue(child);
                }
            }

            return res;
        }

        private static PathNode BuildPathTreeInternal(PathNode root, int level)
        {
            var index = 0;
            var prevCommonPath = "";

            var newRoot = new PathNode(root.CommonPath, new List<PathNode>(), level, false);
            while (index < root.Children.Count)
            {
                // item has common path with next item.
                var currentCommonPath = "";
                if (index + 1 < root.Children.Count &&
                    (currentCommonPath = GetCommonPath(new List<string>() { root.Children[index].CommonPath, root.Children[index + 1].CommonPath })) != "")
                {
                    var childNode = new PathNode(currentCommonPath, new List<PathNode>(), level + 1, false);
                    while (index < root.Children.Count && (prevCommonPath == "" || prevCommonPath == currentCommonPath))
                    {
                        prevCommonPath = currentCommonPath;
                        childNode.Children.Add(new PathNode(root.Children[index].CommonPath.Substring(currentCommonPath.Length, root.Children[index].CommonPath.Length - currentCommonPath.Length), new List<PathNode>(), level + 2, false));

                        index++;
                        if (index < root.Children.Count)
                        {
                            currentCommonPath = GetCommonPath(new List<string>() { currentCommonPath, root.Children[index].CommonPath });
                        }
                    }

                    prevCommonPath = "";
                    newRoot.Children.Add(childNode);
                }
                else
                {
                    // single leaf node
                    var childNode = root.Children[index];
                    newRoot.Children.Add(childNode);
                    index++;
                }
            }

            if (newRoot.Children.Count == 0)
            {
                newRoot.Leaf = true;
            }

            // build child nodes
            for (var i = 0; i < newRoot.Children.Count; i++)
            {
                newRoot.Children[i] = BuildPathTreeInternal(newRoot.Children[i], level + 1);
            }

            return newRoot;
        }

        public static string GetOperationIdPrefix(string operationId)
        {
            return operationId.Split("_")[0];
        }

        public static string GetOperationIdAction(string operationId)
        {
            var items = operationId.Split("_");
            return items.Length < 2 ? "" : items[1];
        }

        public static string BuildDefinitionId(IEnumerable<string> paths)
        {
            return $"{string.Join('-', paths).TrimStart('#')}";
        }

        public static string GetDefinitionType(string Ref)
        {
            return Ref == null ? "" : Ref.Split("/").Last();
        }

        public static string GetRefDefinitionIdPath(string Ref)
        {
            if (Ref.Contains("parameters"))
            {
                return $"-Parameters-{GetDefinitionType(Ref)}";
            }

            if (Ref.Contains("definitions"))
            {
                return $"-Definitions-{GetDefinitionType(Ref)}";
            }

            return "";
        }

        public static string GetReferencedSwaggerFile(string Ref, string currentSwaggerFilePath)
        {
            if (string.IsNullOrEmpty(Ref))
            {
                return currentSwaggerFilePath;
            }

            var idx = Ref.IndexOf("#", StringComparison.Ordinal);
            var relativePath = Ref[..idx];
            if (relativePath == "")
            {
                relativePath = ".";
            }
            else
            {
                currentSwaggerFilePath = Path.GetDirectoryName(currentSwaggerFilePath);
            }

            var referenceSwaggerFilePath = Path.GetFullPath(relativePath, currentSwaggerFilePath!);
            return referenceSwaggerFilePath;
        }

        public static List<string> GetKeywordsFromPatternedObject(List<string> keywords,
            IDictionary<string, JsonElement> patternedObjects)
        {
            if (patternedObjects != null) 
            {
                foreach (var (key, value) in patternedObjects)
                {
                    var sb = new StringBuilder();
                    if (key.StartsWith("x-"))
                    {
                        sb.Append($"{key}: [");
                        string jsonString = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true});
                        sb.Append(jsonString + ']');
                        keywords.Add(sb.ToString());
                    }
                }
            }
            return keywords;
        }
        
        public static void SerializePatternedObjects(IDictionary<string, JsonElement> patternedObjects, List<CodeFileToken> tokens)
        {
            if (patternedObjects != null)
            {
                foreach (var (key, value) in patternedObjects)
                {
                    if (Regex.IsMatch(key, "^x-."))
                    {
                        tokens.Add(new CodeFileToken(key, CodeFileTokenKind.Keyword));
                        tokens.Add(TokenSerializer.Colon());
                        tokens.AddRange(TokenSerializer.TokenSerializeJsonToCodeLines(value));
                    }  
                }
            }
        }

        public static void AddSchemaToRootDefinition(Schema schema, Dictionary<string, Definition> definitions)
        {
            if (schema == null)
            {
                return;
            }
            
            if (!String.IsNullOrEmpty(schema.originalRef))
            {
                var schemaKey = Utils.GetDefinitionType(schema.originalRef);
                if (definitions != null)
                {
                    if (!definitions.ContainsKey(schemaKey))
                    {
                        definitions.Add(schemaKey, (Definition)schema);
                    }
                    else 
                    {
                        Definition def = (Definition)schema;
                        if (definitions[schemaKey].IsRefObject())
                        {
                            definitions[schemaKey] = def;
                        }
                        else 
                        {
                            definitions[schemaKey].description = definitions[schemaKey].description ?? def.description;
                            foreach (var prop in def.properties)
                            {
                                if (!definitions[schemaKey].properties.ContainsKey(prop.Key))
                                {
                                    definitions[schemaKey].properties.Add(prop.Key, prop.Value);
                                }
                            }
                        }

                    }
                }
            }
        }
    }
}
