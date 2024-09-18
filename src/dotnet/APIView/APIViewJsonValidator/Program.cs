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
        var outputDir = new Option<DirectoryInfo>("--outputDir", "Path to the output Directory(Optional).");
        var dumpOption = new Option<bool>("--dumpApiText", "Dump the API text to a txt file.");
        var convertOption = new Option<bool>("--convertToTree", "Convert old APIView token model to new tree token model.");

        var rootCommand = new RootCommand("Generate API review output from token JSON file to verify the input json file")
        {
            jsonFilePath,
            outputDir,
            dumpOption,
            convertOption
        };

        rootCommand.SetHandler(static async (jsonFilePath, outputDir, dumpOption, convertOption) =>
        {
            if(!dumpOption && !convertOption)
            {
                Console.Error.WriteLine("Please specify either --dumpApiText or --convertToTree option.");
                return;
            }

            try
            {
                var outputFileDirectory = outputDir?.FullName ?? jsonFilePath.Directory.FullName;
                if (dumpOption)
                {

                    var outputFilePath = Path.Combine(outputFileDirectory, jsonFilePath.Name.Replace(".json", ".txt"));
                    Console.WriteLine($"Dumping API text to {outputFilePath}");
                    using (var stream = jsonFilePath.OpenRead())
                    {
                        await GenerateReviewTextFromJson(stream, outputFilePath);
                    }
                }

                if (convertOption)
                {
                    var outputFilePath = Path.Combine(outputFileDirectory, jsonFilePath.Name.Replace(".json", "_new.json"));
                    Console.WriteLine($"Converting old Json API view tokens to new Json model API tokens. New json file: {outputFilePath}");
                    using (var stream = jsonFilePath.OpenRead())
                    {
                        await ConvertToTreeModel(stream, outputFilePath);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading input json file : {ex.Message}");
                throw;
            }
        }, jsonFilePath, outputDir, dumpOption, convertOption);
        return rootCommand.InvokeAsync(args).Result;
    }

    private static async Task GenerateReviewTextFromJson(Stream stream, string outputFilePath)
    {
        var codeFile = await CodeFile.DeserializeAsync(stream, false, true);
        string apiOutput = codeFile.GetApiText(false);
        await File.WriteAllTextAsync(outputFilePath, apiOutput);
    }

    private static async Task ConvertToTreeModel(Stream stream, string outputFilePath)
    {
        CodeFile codeFile = null;
        try
        {
            codeFile = await CodeFile.DeserializeAsync(stream, false, false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Input json is probably not using old token model. Error reading input json file. : {ex.Message}");
            throw;
        }

        if(codeFile != null)
        {
            codeFile.ConvertToTreeTokenModel();
            Console.WriteLine("Converted APIView token model to new schema");
            codeFile.Tokens = null;
            using var fileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
            await codeFile.SerializeAsync(fileStream);
            Console.WriteLine($"New APIView json token file generated at {outputFilePath}");
        }        
    }
}
