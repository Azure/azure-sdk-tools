using System.CommandLine;
using YamlDotNet.Serialization;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.PipelineTemplateConverter;

public class PipelineTemplateConverter
{
    public static void Convert(FileInfo file, bool overwrite)
    {
        var deserializer = new DeserializerBuilder().Build();
        var contents = File.ReadAllText(file.FullName);

        var templateTypes = GetTemplateType(contents);
        if (templateTypes.Contains(TemplateType.Ignore))
        {
            return;
        }

        var processedLines = BackupCommentsAndFormatting(contents, templateTypes);
        var output = "";

        if (templateTypes.Contains(TemplateType.Converted))
        {
            Console.WriteLine($"Skipping {file.FullName} already converted");
            return;
        }

        if (templateTypes.Contains(TemplateType.Stage))
        {
            Console.WriteLine($"Converting {file.FullName} stage template");
            var template = deserializer.Deserialize<StageTemplate>(contents);
            ConvertStageTemplate(template);
            output = template.ToString();
            output = RestoreCommentsAndFormatting(output, processedLines);
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

    public static List<Line> BackupCommentsAndFormatting(string template, List<TemplateType> templateTypes)
    {
        var lineInstances = new Dictionary<string, int>();
        var lines = template.Split(Environment.NewLine);
        var processedLines = new List<Line>();

        for (var i = 0; i < lines.Length; i++)
        {
            var comment = new List<string>();
            var commentHasNewLineBefore = false;
            if (i > 0 && lines[i - 1].Trim().Length == 0)
            {
                commentHasNewLineBefore = true;
            }

            while (i < lines.Length && lines[i].TrimStart(' ').StartsWith("#"))
            {
                comment.Add(lines[i].Trim(' '));
                i++;
            }

            var line = new Line(lines[i]);
            HandleEndOfFileComment(lines, i, ref line, comment);
            SetLineInstance(line, lineInstances);
            HandleComment(line, comment, commentHasNewLineBefore);
            HandleInlineComment(lines, i, line);
            HandleBlockChompIndicator(line, lines, i);
            SetLinePrecedingNewline(line, templateTypes, lines, i);

            processedLines.Add(line);
        }

        return processedLines;
    }

    // What happens when a file simply ends on a comment? there's nowhere to associate the line
    // With. When that occurs, simply insert a new empty string. Files shouldn't end without
    // trailing whitespace anyway, so this is not a destructive update.
    public static void HandleEndOfFileComment(string[] lines, int index, ref Line line, List<string> comment)
    {
        if (index >= lines.Length)
        {
            line = new Line(String.Empty);
        }
    }

    public static void SetLineInstance(Line line, Dictionary<string, int> lineInstances)
    {
        lineInstances[line.LookupKey] = lineInstances.ContainsKey(line.LookupKey) ? lineInstances[line.LookupKey] + 1 : 1;
        line.Instance = lineInstances[line.LookupKey];
    }

    public static void HandleComment(Line line, List<string> comment, bool commentHasNewLineBefore = false)
    {
        if (comment.Count > 0)
        {
            line.Comment = new Comment(comment);
            line.Comment.NewLineBefore = commentHasNewLineBefore;
        }
    }

    public static void HandleInlineComment(string[] lines, int index, Line line)
    {
        if (lines[index].Contains('#'))
        {
            var inlineComment = lines[index][lines[index].IndexOf("#")..];
            line.InlineComment = new Comment(inlineComment);
        }
    }

    // The purpose of this method is preserve the original yaml formatting for readability
    // when a block chomp indicator `>` is encountered.
    // The yamldotnet parser/scanner deletes newlines and block chomp indicators at a low
    // level we can't override, so handle this separately.
    public static void HandleBlockChompIndicator(Line line, string[] lines, int index)
    {
        if (!line.Value.Contains(": >") && !line.Value.Contains(": >-"))
        {
            return;
        }

        var indent = lines[index][..^lines[index].TrimStart(' ').Length].Length;
        var contents = new List<string>();

        do
        {
            var nextIndent = lines[index + 1][..^lines[index + 1].TrimStart(' ').Length].Length;
            if (nextIndent <= indent)
            {
                break;
            }
            index++;
            contents.Add(lines[index].Trim());
        }
        while (index < lines.Length - 1);

        line.BlockChompedLine = contents;
        line.LookupKey = line.Value.Replace(": >-", ":").Replace(": >", ":");
        line.LookupKey += " " + string.Join(" ", line.BlockChompedLine);
    }

    public static void SetLinePrecedingNewline(Line line, List<TemplateType> templateTypes, string[] lines, int index)
    {
        if (index > 0 && lines[index - 1].Trim().Length == 0)
        {
            line.NewLineBefore = true;
        }
        // Handle various special cases where we know whether we want a newline or not
        else if (templateTypes.Contains(TemplateType.Stage))
        {
            if (lines[index].StartsWith("parameters:") || lines[index].StartsWith("trigger:"))
            {
                line.NewLineBefore = true;
            }
            else if (lines[index].StartsWith("variables:"))
            {
                line.NewLineBefore = false;
            }
        }
    }

    public static string RestoreCommentsAndFormatting(string template, List<Line> processedLines)
    {
        var lines = new List<string>();
        var lineInstances = new Dictionary<string, int>();

        var lookup = new Dictionary<(string, int), Line>();
        foreach (var line in processedLines)
        {
            lookup.Add((line.LookupKey, line.Instance), line);
        }

        foreach (var _line in template.Split(Environment.NewLine))
        {
            var line = FixTemplateSpecialCharacters(_line);
            var parsed = new Line(line);
            lineInstances[parsed.LookupKey] = lineInstances.ContainsKey(parsed.LookupKey) ? lineInstances[parsed.LookupKey] + 1 : 1;
            if (!lookup.ContainsKey((parsed.LookupKey, lineInstances[parsed.LookupKey])))
            {
                // Fix preceding newline with newly added extends directive
                if (line.StartsWith("extends:"))
                {
                    lines.Add("");
                }
                lines.Add(line);
                continue;
            }

            var indentation = line[..^line.TrimStart(' ').Length];
            var original = lookup[(parsed.LookupKey, lineInstances[parsed.LookupKey])];
            if (original.NewLineBefore || original.Comment?.NewLineBefore == true)
            {
                lines.Add("");
            }

            // Comments in embedded strings get preserved during serialization so don't restore those
            var lineIsComment = original.Comment?.Value.Any(c => line.Contains(c)) ?? false;
            var lineHasInlineComment = original.InlineComment?.Value.Any(c => line.Contains(c)) ?? false;
            if (lineIsComment || lineHasInlineComment)
            {
                lines.Add(line);
                continue;
            }

            foreach (var commentLine in original.Comment?.Value ?? new List<string>())
            {
                lines.Add(indentation + commentLine);
            }

            var inlineComment = original.InlineComment?.Value.FirstOrDefault();
            if (inlineComment != null)
            {
                lines.Add(indentation + original.Value + "  " + inlineComment);
            }
            else
            {
                lines.Add(indentation + original.Value);
            }

            if (original.BlockChompedLine != null)
            {
                foreach (var chompedLine in original.BlockChompedLine)
                {
                    lines.Add(indentation + "  " + chompedLine);
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    // Yaml serialization adds quotes when special characters are present,
    // such as ones used for azure pipelines templating logic.
    public static string FixTemplateSpecialCharacters(string line)
    {
        line = line.Replace("'${{", "${{");
        line = line.Replace("}}:':", "}}:");
        line = line.Replace("}}:'", "}}:");
        line = line.Replace("\"${{", "${{");
        line = line.Replace("}}:\":", "}}:");
        line = line.Replace("}}:\"", "}}:");
        return line;
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
            var currIndent = indent;

            do
            {
                // The publish tasks have way to much casing variation across our yaml files
                // (e.g. PathToPublish, pathtoPublish)
                // so force lowercase the key here. YamlDotNet only supports lowercasing
                // class properties and not yaml keys.
                var lowercaseKey = lines[i].TrimStart(' ').Split(":")[0].ToLower();
                var line = new string(' ', currIndent) + lowercaseKey + ": " + string.Join("", lines[i].TrimStart(' ').Split(":")[1..]);
                yaml += line + Environment.NewLine;
                i++;
                if (i < lines.Length)
                {
                    currIndent = lines[i][..^lines[i].TrimStart(' ').Length].Length;
                }
            } while (i < lines.Length && currIndent > indent);

            var task = new DeserializerBuilder()
                            .WithNamingConvention(LowerCaseNamingConvention.Instance)
                            .Build()
                            .Deserialize<PublishArtifactTask[]>(yaml);

            foreach (var line in task[0].Convert())
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

        template.Resources ??= new Dictionary<string, object>();
        template.Resources["repositories"] = repositories.Prepend(repository);

        template.Extends = extends;
        template.Extends.Add("${{ if eq(variables['System.TeamProject'], 'internal') }}:", new Dictionary<string, object>
        {
            ["template"] = "v1/1ES.Official.PipelineTemplate.yml@1ESPipelineTemplates",
        });
        template.Extends.Add("${{ else }}:", new Dictionary<string, object>
        {
            ["template"] = "v1/1ES.Unofficial.PipelineTemplate.yml@1ESPipelineTemplates",
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
                foreach (var stage in template.Stages)
                {
                    if (stage.ContainsKey("stage"))
                    {
                        stage["variables"] = template.Variables;
                    }
                }
            }
            parameters.Add("stages", template.Stages);
        }
        template.Stages = null;
        template.Variables = null;
    }
}
