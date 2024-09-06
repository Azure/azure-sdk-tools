// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.CommandLine.Invocation;
using System.CommandLine;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using APIView;
using ApiView;

public static class Program
{
    public static int Main(string[] args)
    {
        var jsonFilePath = new Option<FileInfo>("--path", "Path to the input json file").ExistingOnly();
        jsonFilePath.IsRequired = true;

        var rootCommand = new RootCommand("Generate API review output from token JSON file to verify the input json file")
        {
            jsonFilePath
        };

        rootCommand.SetHandler(async (FileInfo jsonFilePath) =>
        {
            try
            {
                var parentDirectory = jsonFilePath.Directory;
                var outputFilePath = Path.Combine(parentDirectory?.FullName, jsonFilePath.Name.Replace(".json", ".txt"));
                using (var stream = jsonFilePath.OpenRead())
                {
                    await GenerateReviewTextFromJson(stream, outputFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading input json file : {ex.Message}");
                throw;
            }
        }, jsonFilePath);
        return rootCommand.InvokeAsync(args).Result;
    }

    private static async Task GenerateReviewTextFromJson(Stream stream, string outputFilePath)
    {
        var codeFile = await CodeFile.DeserializeAsync(stream, false, true);
        string apiOutput = codeFile.GetApiText();
        await File.WriteAllTextAsync(outputFilePath, apiOutput);
    }
}
