using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIView;

namespace swagger_api_parser;

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

        var generalNavigationItem = new NavigationItem() {Text = "general", NavigationId = $"{swaggerFileName}_-swagger",};

        var operationIdNavigationItem = new NavigationItem() {Text = "operationIds", NavigationId = $"{swaggerFileName}_-swagger",};

        var pathNavigationItem = new NavigationItem() {Text = "paths", NavigationId = $"{swaggerFileName}_-paths",};

        var aggregatedPaths = new Dictionary<string, NavigationItem[]>();

        string[] generalKeys = new string[] {"swagger", "host", "info", "basePath", "schemes", "consumes", "produces", "securityDefinitions", "security", "x-ms-parameterized-host"};

        void BuildOperationIdNavigationItem(NavigationItem navigationItem)
        {
            foreach (var operationIdItem in navigationItem.ChildItems)
            {
                operationIdNavigationItem.ChildItems =
                    operationIdNavigationItem.ChildItems.Concat(new NavigationItem[] {operationIdItem})
                        .ToArray();
            }
        }

        void BuildAggregatedPaths(NavigationItem path, string commonPath)
        {
            path.ChildItems = Array.Empty<NavigationItem>();
            if (aggregatedPaths.TryGetValue(commonPath, out NavigationItem[] existing))
            {
                aggregatedPaths[commonPath] = existing.Concat(new NavigationItem[] {path}).ToArray();
            }
            else
            {
                aggregatedPaths.Add(commonPath, new NavigationItem[] {path});
            }
        }

        foreach (var item in navigation)
        {
            if (generalKeys.Contains(item.Text))
            {
                generalNavigationItem.ChildItems =
                    generalNavigationItem.ChildItems.Concat(new NavigationItem[] {item}).ToArray();
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
            var parentNavigationItem = new NavigationItem() {Text = aggregatedPath, NavigationId = $"{swaggerFileName}_-paths",};

            foreach (var path in pathItems)
            {
                path.Text = path.Text[aggregatedPath.Length..];

                // If sub path is empty, should be root path "/"
                if (path.Text != "")
                {
                    parentNavigationItem.ChildItems =
                        parentNavigationItem.ChildItems.Concat(new NavigationItem[] {path}).ToArray();
                }
            }

            pathNavigationItem.ChildItems = pathNavigationItem.ChildItems
                .Concat(new NavigationItem[] {parentNavigationItem}).ToArray();
        }


        result.Insert(0, generalNavigationItem);
        result.Insert(1, pathNavigationItem);
        result.Insert(result.Count(), operationIdNavigationItem);
        return result.ToArray();
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


        foreach (var (swaggerFileName, codeFile) in this.originalResult)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(swaggerFileName);
            result.Tokens = result.Tokens.Concat(codeFile.Tokens).Select(it => it).ToArray();

            // Add file name as top level to navigation
            var navigation = new NavigationItem {Text = swaggerFileName, NavigationId = $"{swaggerFileName}_-swagger", ChildItems = RebuildNavigation(codeFile.Navigation, swaggerFileName)};
            result.Navigation = result.Navigation.Concat(new NavigationItem[] {navigation}).ToArray();
        }

        Console.WriteLine($"Writing {outputFile}");
        var outputFilePath = Path.GetFullPath(outputFile);
        await using FileStream fileWriteStream = File.Open(outputFilePath, FileMode.Create);
        await result.SerializeAsync(fileWriteStream);
        Console.WriteLine("finished");
    }
}
