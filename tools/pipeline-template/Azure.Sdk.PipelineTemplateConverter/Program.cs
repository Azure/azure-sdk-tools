using System.CommandLine;
using YamlDotNet.Serialization;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace Azure.Sdk.PipelineTemplateConverter;

public enum TemplateType
{
    Stage,
    Job,
    Step,
    ArtifactTask,
    Converted,
    Ignore
}

public class BaseTemplate
{
    [YamlMember(Alias = "resources", Order = 0)]
    public Dictionary<string, object>? Resources { get; set; }

    [YamlMember(Alias = "parameters", Order = 1)]
    public object? Parameters { get; set; }

    [YamlMember(Alias = "trigger", Order = 2)]
    public object? Trigger { get; set; }

    [YamlMember(Alias = "pr", Order = 3)]
    public object? PullRequest { get; set; }

    [YamlMember(Alias = "variables", Order = 4)]
    public List<object>? Variables { get; set; }

    private ISerializer Serializer { get; } = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .WithIndentedSequences()
        .Build();

    public override string ToString()
    {
        return Serializer.Serialize(this) + Environment.NewLine;
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

    [YamlMember(Alias = "extends", Order = 11)]
    public Dictionary<string, object>? Extends { get; set; }

    [YamlMember(Alias = "pool", Order = 10)]
    public Dictionary<string, object>? Pool { get; set; }
}

public class Comment
{
    public List<string> Value { get; set; } = new List<string>();
    // NOTE: this won't handle duplicate lines, but probably not a case that will be hit
    public string AppearsBeforeLine { get; set; } = string.Empty;
    public string AppearsOnLine { get; set; } = string.Empty;
    public bool PrecedingWhitespace { get; set; } = false;
}

public class PublishArtifactTask
{
    public string PublishType { get; set; } = "";
    public int Indent { get; set; } = 0;

    [YamlMember(Alias = "task")]
    public string? Task { get; set; } = "";

    [YamlMember(Alias = "displayName")]
    public string? DisplayName { get; set; } = "";

    [YamlMember(Alias = "inputs")]
    public Inputs TaskInputs { get; set; } = new Inputs();

    public class Inputs
    {

        [YamlMember(Alias = "artifact")]
        public string? Artifact { get; set; } = "";
        [YamlMember(Alias = "artifactName")]
        public string? ArtifactName { get; set; } = "";

        [YamlMember(Alias = "path")]
        public string? Path { get; set; } = "";
        [YamlMember(Alias = "pathtoPublish")]
        public string? PathtoPublish { get; set; } = "";

        // Nuget publish task options
        [YamlMember(Alias = "packagesToPush")]
        public string? PackagesToPush { get; set; } = "";
        [YamlMember(Alias = "packageParentPath")]
        public string? PackageParentPath { get; set; } = "";
        [YamlMember(Alias = "nugetFeedType")]
        public string? NugetFeedType { get; set; } = "";
        [YamlMember(Alias = "publishVstsFeed")]
        public string? PublishVstsFeed { get; set; } = "";
    }

    public string Convert()
    {
        var output =  Indent + $"- template: /eng/common/pipelines/templates/steps/publish-artifact.yml" + Environment.NewLine +
                      Indent + $"  parameters:" + Environment.NewLine +
                      Indent + $"    PublishType: {PublishType}" + Environment.NewLine +
                      Indent + $"    ArtifactName: {TaskInputs.Artifact ?? TaskInputs.ArtifactName}" + Environment.NewLine +
                      Indent + $"    ArtifactPath: {TaskInputs.Path ?? TaskInputs.PathtoPublish}" + Environment.NewLine;

        if (DisplayName != null)
        {
            output += Indent + $"    DisplayName: {DisplayName}" + Environment.NewLine;
        }
        if (TaskInputs.PackagesToPush != null)
        {
            output += Indent + $"    PackagesToPush: {TaskInputs.PackagesToPush}" + Environment.NewLine;
        }
        if (TaskInputs.PackageParentPath != null)
        {
            output += Indent + $"    PackageParentPath: {TaskInputs.PackageParentPath}" + Environment.NewLine;
        }
        if (TaskInputs.NugetFeedType != null)
        {
            output += Indent + $"    NugetFeedType: {TaskInputs.NugetFeedType}" + Environment.NewLine;
        }
        if (TaskInputs.PublishVstsFeed != null)
        {
            output += Indent + $"    PublishVstsFeed: {TaskInputs.PublishVstsFeed}" + Environment.NewLine;
        }

        return output;
    }
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

        var templateTypes = GetTemplateType(contents);
        if (templateTypes.Contains(TemplateType.Ignore))
        {
            return;
        }

        var comments = BackupComments(contents);
        var output = "";

        if (templateTypes.Contains(TemplateType.Stage))
        {
            Console.WriteLine($"Converting {file.FullName} stage template");
            var template = deserializer.Deserialize<StageTemplate>(contents);
            ConvertStageTemplate(template);
            output = template.ToString();
            output = RestoreComments(output, comments);
            output = AddTemplateWhitespace(output);
            output = FixTemplateSpecialCharacters(output);
        }

        if (templateTypes.Contains(TemplateType.ArtifactTask))
        {
            Console.WriteLine($"Converting {file.FullName} publish tasks");
            output = ConvertPublishTasks(output);
        }

        if (overwrite)
        {
            File.WriteAllText(file.FullName, output);
            return;
        }
        Console.WriteLine(output);
    }

    public static List<TemplateType> GetTemplateType(string template)
    {
        var convertedRegex = new Regex(@".*1ESPipelineTemplates.*");
        var stageRegex = new Regex(@".*stages:.*$", RegexOptions.Multiline);
        var jobRegex = new Regex(@"^jobs:.*$", RegexOptions.Multiline);
        var stepRegex = new Regex(@"^steps:.*$", RegexOptions.Multiline);
        var publishRegex = new Regex(@"PublishPipelineArtifact@1.*$", RegexOptions.Multiline);
        var publishBuildRegex = new Regex(@"PublishBuildArtifact@1.*$", RegexOptions.Multiline);
        var nugetRegex = new Regex(@"^NugetCommand@2:.*$", RegexOptions.Multiline);

        var types = new List<TemplateType>();

        if (convertedRegex.IsMatch(template))
        {
            types.Add(TemplateType.Converted);
        }
        if (stageRegex.IsMatch(template))
        {
            types.Add(TemplateType.Stage);
        }
        if (jobRegex.IsMatch(template))
        {
            types.Add(TemplateType.Job);
        }
        if (stepRegex.IsMatch(template))
        {
            types.Add(TemplateType.Step);
        }
        if (publishRegex.IsMatch(template) || publishBuildRegex.IsMatch(template) || nugetRegex.IsMatch(template))
        {
            types.Add(TemplateType.ArtifactTask);
        }

        if (types.Count == 0)
        {
            types.Add(TemplateType.Ignore);
        }

        return types;
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
                // Comments in embedded strings get preserved during serialization so don't restore those
                foreach (var commentLine in comment.Value)
                {
                    if (line.Contains(commentLine))
                    {
                        comment.AppearsBeforeLine = "";
                        comment.AppearsOnLine = "";
                    }
                }

                if (line.Trim(' ') == comment.AppearsBeforeLine &&
                    comment.AppearsBeforeLine != string.Empty)
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
        var addWhitespaceForLines = new List<string>
        {
            "extends",
            "parameters",
            "trigger",
        };
        foreach (var line in template.Split(Environment.NewLine))
        {
            if (addWhitespaceForLines.Any(l => line.StartsWith(l)))
            {
                lines.Add("");
            }
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string ConvertPublishTasks(string template)
    {
        var lines = template.Split(Environment.NewLine);
        var linesOut = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("task: PublishPipelineArtifact@1") &&
                !lines[i].Contains("task: PublishBuildArtifact@1") &&
                !lines[i].Contains("task: NugetCommand@2"))
            {
                linesOut.Add(lines[i]);
                continue;
            }

            var yaml = "";
            var indent = lines[i][..^lines[i].TrimStart(' ').Length].Length;
            var currIndent = int.MaxValue;
            while (i < lines.Length && currIndent > indent)
            {
                yaml += lines[i] + Environment.NewLine;
                i++;
                currIndent = lines[i][..^lines[i].TrimStart(' ').Length].Length;
            }

            var task = new DeserializerBuilder().Build().Deserialize<PublishArtifactTask[]>(yaml);
            task[0].Indent = indent;

            foreach (var line in task[0].Convert().Split(Environment.NewLine))
            {
                linesOut.Add(new string(' ', indent) + line);
            }
        }

        return string.Join(Environment.NewLine, linesOut);
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
            ["template"] = "v1/1ES.Official.PipelineTemplate.yml@1ESPipelineTemplates",
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
