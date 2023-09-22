using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser
{
    public class SwaggerCodeFileMerger
    {
        private readonly Dictionary<string, CodeFile> originalResult;

        public SwaggerCodeFileMerger()
        {
            this.originalResult = new Dictionary<string, CodeFile>();
        }

        public void AppendResult(string swaggerFileName, CodeFile cf)
        {
            if (this.originalResult.TryGetValue(swaggerFileName, out CodeFile existing))
            {
                Console.WriteLine($"{swaggerFileName} already exists.");
            }
            else
            {
                this.originalResult.Add(swaggerFileName, cf);
            }
        }

        private static NavigationItem[] RebuildNavigation(IEnumerable<NavigationItem> navigation,
            string swaggerFileName)
        {
            var result = new List<NavigationItem>();

            var generalNavigationItem = new NavigationItem() { Text = "general", NavigationId = $"{swaggerFileName}_-swagger", };

            var operationIdNavigationItem = new NavigationItem() { Text = "operationIds", NavigationId = $"{swaggerFileName}_-swagger", };

            var pathNavigationItem = new NavigationItem() { Text = "paths", NavigationId = $"{swaggerFileName}_-paths", };

            var aggregatedPaths = new Dictionary<string, NavigationItem[]>();

            string[] generalKeys = new string[] { "swagger", "host", "info", "basePath", "schemes", "consumes", "produces", "securityDefinitions", "security", "x-ms-parameterized-host" };

            void BuildOperationIdNavigationItem(NavigationItem navigationItem)
            {
                foreach (var operationIdItem in navigationItem.ChildItems)
                {
                    operationIdNavigationItem.ChildItems =
                        operationIdNavigationItem.ChildItems.Concat(new NavigationItem[] { operationIdItem })
                            .ToArray();
                }
            }

            void BuildAggregatedPaths(NavigationItem path, string commonPath)
            {
                path.ChildItems = Array.Empty<NavigationItem>();
                if (aggregatedPaths.TryGetValue(commonPath, out NavigationItem[] existing))
                {
                    aggregatedPaths[commonPath] = existing.Concat(new NavigationItem[] { path }).ToArray();
                }
                else
                {
                    aggregatedPaths.Add(commonPath, new NavigationItem[] { path });
                }
            }

            foreach (var item in navigation)
            {
                if (generalKeys.Contains(item.Text))
                {
                    generalNavigationItem.ChildItems =
                        generalNavigationItem.ChildItems.Concat(new NavigationItem[] { item }).ToArray();
                }
                else if (item.Text.Equals("paths") || item.Text.Equals("x-ms-paths"))
                {
                    // extract operationIds from path 
                    var allPaths = item.ChildItems.Select(x => x.Text).ToArray();
                    PathNode node = Utils.BuildPathTree(allPaths);
                    var firstLevelPath = node.Children.Select(child => child.CommonPath).ToList();

                    foreach (var path in item.ChildItems)
                    {
                        var commonPath = "";
                        foreach (var it in firstLevelPath.Where(it => path.Text.Contains(it)))
                        {
                            commonPath = it;
                            break;
                        }

                        BuildOperationIdNavigationItem(path);
                        BuildAggregatedPaths(path, commonPath);
                    }
                }
                else
                {
                    result.Add(item);
                }
            }

            foreach (var (aggregatedPath, pathItems) in aggregatedPaths)
            {
                var resourceProvider = Utils.GetResourceProviderFromPath(aggregatedPath);
                var firstLevelShowText = aggregatedPath;
                if (resourceProvider != "")
                {
                    var index = aggregatedPath.LastIndexOf(resourceProvider, StringComparison.Ordinal);
                    var managementPrefix = aggregatedPath[..(index + resourceProvider.Length)];
                    var managementShortcut = GetResourceManagementTemplatePrefix(managementPrefix, resourceProvider);
                    firstLevelShowText = aggregatedPath.Replace(managementPrefix, managementShortcut);
                }

                var parentNavigationItem = new NavigationItem() { Text = firstLevelShowText, NavigationId = $"{swaggerFileName}_-paths", };

                foreach (var path in pathItems)
                {
                    path.Text = path.Text[aggregatedPath.Length..];

                    // If sub path is empty, should be root path "/"
                    if (path.Text != "")
                    {
                        parentNavigationItem.ChildItems =
                            parentNavigationItem.ChildItems.Concat(new NavigationItem[] { path }).ToArray();
                    }
                }

                pathNavigationItem.ChildItems = pathNavigationItem.ChildItems
                    .Concat(new NavigationItem[] { parentNavigationItem }).ToArray();
            }


            result.Insert(0, generalNavigationItem);
            result.Insert(1, pathNavigationItem);
            result.Insert(result.Count(), operationIdNavigationItem);
            return result.ToArray();
        }

        private static string GetResourceManagementTemplatePrefix(string managementPrefix, string resourceProvider)
        {
            if (managementPrefix.Contains("resourceGroups"))
            {
                return $"/<Sub>/{resourceProvider}/<RG>";
            }
            return managementPrefix.Contains("subscriptions") ? $"/<Sub>/{resourceProvider}/" : $"/{resourceProvider}";
        }

        public async Task GenerateCodeFile(string outputFile, string packageName)
        {
            CodeFile result = new CodeFile
            {
                Language = "Swagger",
                VersionString = "0",
                Name = packageName,
                PackageName = packageName,
                Tokens = new CodeFileToken[] { },
                Navigation = new NavigationItem[] { }
            };

            var globalNavigations = new NavigationItem[] { };
            foreach (var (swaggerFileName, codeFile) in this.originalResult)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(swaggerFileName);
                result.Tokens = result.Tokens.Concat(codeFile.Tokens).Select(it => it).ToArray();
                globalNavigations = MergeSwaggerNavigationItems(globalNavigations, codeFile.Navigation);
            }

            result.Navigation = RebuildNavigation(globalNavigations, packageName);

            Console.WriteLine($"Writing {outputFile}");
            var outputFilePath = Path.GetFullPath(outputFile);
            await using FileStream fileWriteStream = File.Open(outputFilePath, FileMode.Create);
            await result.SerializeAsync(fileWriteStream);
            Console.WriteLine("finished");
        }

        private static NavigationItem[] MergeSwaggerNavigationItems(IEnumerable<NavigationItem> a, IEnumerable<NavigationItem> b)
        {
            var result = new NavigationItem[] { };
            var navigationItemMap = new Dictionary<string, NavigationItem>();

            foreach (var item in a)
            {
                if (!navigationItemMap.ContainsKey(item.Text))
                {
                    navigationItemMap.Add(item.Text, item);
                }
            }

            foreach (var item in b)
            {
                if (!navigationItemMap.ContainsKey(item.Text))
                {
                    navigationItemMap.Add(item.Text, item);
                }
                else
                {
                    var existItem = navigationItemMap.GetValueOrDefault(item.Text);
                    if (existItem != null)
                    {
                        existItem.ChildItems = existItem.ChildItems.Concat(item.ChildItems).ToArray();
                    }
                }
            }

            return navigationItemMap.Values.ToArray();
        }
    }
}
