using System.CommandLine;
using YamlDotNet.Serialization;

namespace Azure.Sdk.PipelineTemplateConverter;

public class StageTemplate
{
    [YamlMember(Alias = "resources")]
    public Dictionary<string, object>? Resources { get; set; }

    [YamlMember(Alias = "parameters")]
    public List<object>? Parameters { get; set; }

    [YamlMember(Alias = "variables")]
    public List<object>? Variables { get; set; }

    [YamlMember(Alias = "stages")]
    public List<Dictionary<string, object>>? Stages { get; set; }

    [YamlMember(Alias = "extends")]
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
            description: "The pipeline yaml template to convert")
            { IsRequired = true };
        var overwrite = new Option<Boolean>(
            new[] { "--overwrite" },
            description: "Write changes back to pipeline file");

        var rootCommand = new RootCommand("Pipeline template converter");
        rootCommand.AddOption(pipelineTemplate);

        rootCommand.SetHandler((file, overwrite) =>
            {
                Run(file, overwrite);
            },
            pipelineTemplate, overwrite);

        return await rootCommand.InvokeAsync(args);
    }

    public static void Run(FileInfo file, bool overwrite)
    {
        var deserializer = new DeserializerBuilder().Build();
        var contents = File.ReadAllText(file.FullName);
        var comments = BackupComments(contents);
        var template = deserializer.Deserialize<StageTemplate>(contents);

        ConvertStageTemplate(template);

        var serializer = new SerializerBuilder()
                            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                            .WithIndentedSequences()
                            .Build();
        var output = serializer.Serialize(template);

        output = RestoreComments(output, comments);
        output = AddStageTemplateWhitespace(output);

        if (overwrite)
        {
            File.WriteAllText(file.FullName, output);
            return;
        }
        Console.WriteLine(output);
    }

    public static List<Comment> BackupComments(string template)
    {
        var comments = new List<Comment>();
        var lines = template.Split(Environment.NewLine);
        for(var i = 0; i < lines.Length-1; i++)
        {
            var comment = new List<string>();
            var precedingWhitespace = false;
            while (i < lines.Length && lines[i].TrimStart(' ').StartsWith("#"))
            {
                comment.Add(lines[i].Trim(' '));
                if (lines[i-1].Trim(' ').Length == 0)
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

    public static string AddStageTemplateWhitespace(string template)
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
        template.Extends.Add("template", "v1/1ES.Official.PipelineTemplate.yml@1ESPipelineTemplates");
        template.Extends.Add("parameters", parameters);

        parameters.Add("sdl", sdl);
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
