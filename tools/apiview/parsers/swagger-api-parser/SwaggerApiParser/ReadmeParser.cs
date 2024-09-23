using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SwaggerApiParser
{
    public class ReadmeBasicConfiguration
    {
        public string title { get; set; }
        public string tag { get; set; }
    }

    public class InputSwaggerFiles
    {
        [YamlMember(Alias = "input-file", ApplyNamingConventions = false)]
        public List<string> input { get; set; }
    }

    public class ReadmeParser
    {
        private string readmeFilePath;

        public Dictionary<string, InputSwaggerFiles> inputSwaggerFilesMap;
        public ReadmeBasicConfiguration basicConfig;

        public ReadmeParser(string readmeFilePath)
        {
            this.readmeFilePath = readmeFilePath;
            this.inputSwaggerFilesMap = new Dictionary<string, InputSwaggerFiles>();
            this.basicConfig = null;
        }

        public static string GetTagFromYamlArguments(string arguments)
        {
            string pattern = @"\$\(tag\)=='(.*)'";
            var matchResult = Regex.Match(arguments.Replace(" ", ""), pattern);
            return matchResult.Success ? matchResult.Groups[1].Value : "";
        }

        public static IEnumerable<string> GetSwaggerFilesFromReadme(string readme, string tag)
        {
            ReadmeParser parser = new ReadmeParser(readme);
            parser.ParseReadmeConfig();
            string readmeTag = tag;
            if (tag == "default" && parser.basicConfig != null)
            {
                readmeTag = parser.basicConfig.tag;
            }

            parser.inputSwaggerFilesMap.TryGetValue(readmeTag, out InputSwaggerFiles inputFiles);
            return inputFiles?.input ?? Enumerable.Empty<string>();
        }
        private void ParseReadmeConfig()
        {
            var readmeContent = File.ReadAllText(readmeFilePath);
            MarkdownDocument document = Markdown.Parse(readmeContent);

            foreach (var block in document)
            {
                if (block is not FencedCodeBlock yamlBlock)
                {
                    continue;
                }

                if (yamlBlock.Info != "yaml")
                {
                    continue;
                }

                var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                if (yamlBlock.Lines.Lines.Any(x => x.ToString().StartsWith("tag")) && basicConfig == null)
                {
                    basicConfig = yamlDeserializer.Deserialize<ReadmeBasicConfiguration>(yamlBlock.Lines.ToString());
                }
                else if (yamlBlock.Lines.ToString().Contains("input-file"))
                {
                    var argument = yamlBlock.Arguments;
                    InputSwaggerFiles inputSwaggerFiles = null;
                    try 
                    {
                        inputSwaggerFiles = yamlDeserializer.Deserialize<InputSwaggerFiles>(yamlBlock.Lines.ToString());
                    } catch (Exception) {
                        Console.WriteLine($"Invalid Yaml Block [ {yamlBlock.Lines.ToString()} ] in Readme. Consider Updating then Run Parser again.");
                        continue;
                    }
                    
                    if (argument == null)
                    {
                        continue;
                    }

                    var tag = ReadmeParser.GetTagFromYamlArguments(argument);
                    if (inputSwaggerFiles != null && inputSwaggerFiles.input != null)
                    {
                        inputSwaggerFiles.input?.Sort(StringComparer.InvariantCultureIgnoreCase);
                        if (!inputSwaggerFilesMap.ContainsKey(tag))
                        {
                            inputSwaggerFilesMap.Add(tag, inputSwaggerFiles);
                        }
                    }
                }
            }
        }
    }
}
