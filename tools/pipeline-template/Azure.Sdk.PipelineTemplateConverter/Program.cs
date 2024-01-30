using System.CommandLine;
using YamlDotNet.Serialization;
using System.Text.RegularExpressions;

namespace Azure.Sdk.PipelineTemplateConverter;

public enum TemplateType
{
    Stage,
    Job,
    Step,
    Converted,
    Ignore
}

public class BaseTemplate
{
    [YamlMember(Alias = "resources", Order = 0)]
    public Dictionary<string, object>? Resources { get; set; }

    [YamlMember(Alias = "parameters", Order = 1)]
    public List<object>? Parameters { get; set; }

    [YamlMember(Alias = "variables", Order = 2)]
    public List<object>? Variables { get; set; }

    private ISerializer Serializer { get; } = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .WithIndentedSequences()
        .Build();

    public override string ToString()
    {
        return Serializer.Serialize(this);
    }
}

public class JobTemplate : BaseTemplate
{
    [YamlMember(Alias = "jobs")]
    public List<Dictionary<string, object>>? Stages { get; set; }
}

public class StageTemplate : BaseTemplate
{
    [YamlMember(Alias = "stages")]
    public List<Dictionary<string, object>>? Stages { get; set; }

    [YamlMember(Alias = "extends", Order = 10)]
    public Dictionary<string, object>? Extends { get; set; }
}

public class Comment
{
    public List<string> Value { get; set; } = new List<string>();
    // NOTE: this won't handle duplicate lines, but probably not a case that will be hit
    public string AppearsBeforeLine { get; set; } = string.Empty;
    public string AppearsOnLine { get; set; } = string.Empty;
    public bool PrecedingWhitespace { get; set; } = false;
}

public class PipelineTemplateConverter
{
    public static async Task<int> Main(string[] args)
    {
        var pipelineTemplate = new Option<FileInfo>(
            new[] { "-p", "--pipeline" },
            description: "The pipeline yaml template to convert");
        var pipelineTemplateDirectory = new Option<DirectoryInfo>(
            new[] { "-d", "--directory" },
            description: "The pipeline yaml directory to convert");
        var overwrite = new Option<Boolean>(
            new[] { "--overwrite" },
            description: "Write changes back to pipeline file");

        var rootCommand = new RootCommand("Pipeline template converter");
        rootCommand.AddOption(pipelineTemplate);
        rootCommand.AddOption(pipelineTemplateDirectory);
        rootCommand.AddOption(overwrite);
        rootCommand.AddValidator(result =>
        {
            var args = result.Children.Select(c => c.Symbol.Name).ToList();
            if (args.Contains("pipeline") && args.Contains("directory"))
            {
                result.ErrorMessage = "Cannot specify both --pipeline and --directory";
            }
            if (!args.Contains("pipeline") && !args.Contains("directory"))
            {
                result.ErrorMessage = "Must specify either --pipeline or --directory";
            }
        });

        rootCommand.SetHandler((file, directory, overwrite) =>
            {
                Run(file, directory, overwrite);
            },
            pipelineTemplate, pipelineTemplateDirectory, overwrite);

        return await rootCommand.InvokeAsync(args);
    }

    public static void Run(FileInfo pipelineTemplate, DirectoryInfo directory, bool overwrite)
    {
        var files = new List<FileInfo>();
        if (pipelineTemplate != null)
        {
            files.Add(pipelineTemplate);
        }
        else
        {
            foreach (var file in directory.GetFiles("*.yml", SearchOption.AllDirectories))
            {
                files.Add(file);
            }
        }
        foreach (var file in files)
        {
            Convert(file, overwrite);
        }
    }

    public static void Convert(FileInfo file, bool overwrite)
    {
        var deserializer = new DeserializerBuilder().Build();
        var contents = File.ReadAllText(file.FullName);

        var templateType = GetTemplateType(contents);
        if (templateType == TemplateType.Ignore)
        {
            return;
        }

        var comments = BackupComments(contents);
        var output = "";

        if (templateType == TemplateType.Stage)
        {
            var template = deserializer.Deserialize<StageTemplate>(contents);
            ConvertStageTemplate(template);
            output = template.ToString();
        }

        output = RestoreComments(output, comments);
        output = AddTemplateWhitespace(output);
        output = FixTemplateSpecialCharacters(output);

        if (overwrite)
        {
            File.WriteAllText(file.FullName, output);
            return;
        }
        Console.WriteLine(output);
    }

    public static TemplateType GetTemplateType(string template)
    {
        var convertedRegex = new Regex(@".*1ESPipelineTemplates.*");
        var stageRegex = new Regex(@".*stages.*$", RegexOptions.Multiline);
        var jobRegex = new Regex(@"^jobs:.*$", RegexOptions.Multiline);
        var stepRegex = new Regex(@"^steps:.*$", RegexOptions.Multiline);

        if (convertedRegex.IsMatch(template))
        {
            return TemplateType.Converted;
        }
        if (stageRegex.IsMatch(template))
        {
            return TemplateType.Stage;
        }
        if (jobRegex.IsMatch(template))
        {
            return TemplateType.Job;
        }
        if (stepRegex.IsMatch(template))
        {
            return TemplateType.Step;
        }
        return TemplateType.Ignore;
    }

    public static List<Comment> BackupComments(string template)
    {
        var comments = new List<Comment>();
        var lines = template.Split(Environment.NewLine);
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var comment = new List<string>();
            var precedingWhitespace = false;
            while (i < lines.Length && lines[i].TrimStart(' ').StartsWith("#"))
            {
                comment.Add(lines[i].Trim(' '));
                if (lines[i - 1].Trim(' ').Length == 0)
                {
                    precedingWhitespace = true;
                }
                i++;
            }

            if (comment.Count > 0)
            {
                comments.Add(new Comment
                {
                    Value = comment,
                    AppearsBeforeLine = lines[i].Trim(' '),
                    PrecedingWhitespace = precedingWhitespace,
                });
            }

            if (lines[i].Contains('#'))
            {
                var inline = lines[i][lines[i].IndexOf("#")..];
                comments.Add(new Comment
                {
                    Value = new List<string> { inline },
                    AppearsOnLine = lines[i].Substring(0, lines[i].IndexOf("#")).Trim(' '),
                });
            }
        }

        return comments;
    }

    public static string RestoreComments(string template, List<Comment> comments)
    {
        var lines = new List<string>();

        foreach (var line in template.Split(Environment.NewLine))
        {
            var _line = line;
            foreach (var comment in comments)
            {
                if (line.Trim(' ') == comment.AppearsBeforeLine && comment.AppearsBeforeLine != string.Empty)
                {
                    var indentation = line.Substring(0, line.Length - line.TrimStart(' ').Length);
                    if (comment.PrecedingWhitespace)
                    {
                        lines.Add("");
                    }
                    foreach (var commentLine in comment.Value)
                    {
                        lines.Add(indentation + commentLine);
                    }
                }
                if (line.Trim(' ') == comment.AppearsOnLine && comment.AppearsOnLine != string.Empty)
                {
                    _line = line + "  " + comment.Value.First();
                }

            }
            lines.Add(_line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    // Yaml serialization adds quotes when special characters are present,
    // such as ones used for azure pipelines templating logic.
    public static string FixTemplateSpecialCharacters(string template)
    {
        template = template.Replace("'${{", "${{");
        template = template.Replace("}}:':", "}}:");
        template = template.Replace("}}:'", "}}:");
        template = template.Replace("\"${{", "${{");
        template = template.Replace("}}:\":", "}}:");
        template = template.Replace("}}:\"", "}}:");
        return template;
    }

    public static string AddTemplateWhitespace(string template)
    {
        var lines = new List<string>();
        foreach (var line in template.Split(Environment.NewLine))
        {
            if (line.StartsWith("extends") || line.StartsWith("parameters"))
            {
                lines.Add("");
            }
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static void ConvertJobTemplate(JobTemplate template)
    {
    }

    public static void ConvertStageTemplate(StageTemplate template)
    {
        var extends = new Dictionary<string, object>();
        var parameters = new Dictionary<string, object>();
        var repositories = template.Resources?["repositories"] as List<object> ?? new List<object>();

        var sdl = new Dictionary<string, object>
        {
            ["sourceAnalysisPool"] = new Dictionary<string, object>
            {
                ["name"] = "azsdk-pool-mms-win-2022-1es-pt",
                ["image"] = "azsdk-pool-mms-win-2022-1espt",
                ["os"] = "windows",
            }
        };

        var repository = new Dictionary<string, object>
        {
            ["repository"] = "1ESPipelineTemplates",
            ["type"] = "git",
            ["name"] = "1ESPipelineTemplates/1ESPipelineTemplates",
            ["ref"] = "refs/tags/release",
        };

        repositories.Add(repository);
        if (template.Resources == null)
        {
            template.Resources = new Dictionary<string, object>();
            template.Resources["repositories"] = repositories;
        }

        template.Extends = extends;
        template.Extends.Add("${{ if eq(variables['System.TeamProject'], 'internal') }}:", new Dictionary<string, object>
        {
            ["template"] = "v1/1ES.Unofficial.PipelineTemplate.yml@1ESPipelineTemplates",
        });
        template.Extends.Add("${{ else }}:", new Dictionary<string, object>
        {
            ["template"] = "stage-redirect.yml",
        });
        template.Extends.Add("parameters", parameters);

        parameters.Add("${{ if eq(variables['System.TeamProject'], 'internal') }}:", new Dictionary<string, object>
        {
            ["sdl"] = sdl
        });

        if (template.Stages != null && template.Stages.Count > 0)
        {
            if (template.Variables != null)
            {
                template.Stages[0]["variables"] = template.Variables;
            }
            parameters.Add("stages", template.Stages);
        }
        template.Stages = null;
        template.Variables = null;
    }
}
