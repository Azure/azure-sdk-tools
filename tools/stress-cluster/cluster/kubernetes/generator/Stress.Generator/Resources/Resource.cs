using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.IO;

namespace Stress.Generator
{
    public interface IResource
    {
        string Template { get; set; }
        string Help { get; set; }

        IEnumerable<ResourcePropertyInfo> OptionalProperties();
        IEnumerable<ResourcePropertyInfo> Properties();
        void Render();
        void SetProperty(PropertyInfo prop, object value);
        void Write(string outputPath);
        void Write();
    }

    public abstract class Resource : IResource
    {
        public List<string> Rendered;
        public bool IsRendered = false;
        public abstract string Template { get; set; }
        public abstract string Help { get; set; }

        public Resource()
        {
            Rendered = new List<string>();
        }

        public IEnumerable<ResourcePropertyInfo> TProperties<T>()
            where T : BaseResourceProperty
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute<T>() != null)
                   .Select(p => new ResourcePropertyInfo(p, p.GetCustomAttribute<T>()));
        }

        public IEnumerable<ResourcePropertyInfo> Properties() => TProperties<ResourceProperty>();

        public IEnumerable<ResourcePropertyInfo> OptionalProperties() => TProperties<OptionalResourceProperty>();

        public void SetProperty(PropertyInfo prop, object value) => prop.SetValue(this, value);

        // Pulling in a large templating library like Razor is more trouble than it's worth
        // and other popular mustache-style templating libraries don't have great support
        // for overridding the delimeters from curly braces, in order to not overlap
        // with helm. It ends up being simpler to handle it ourselves :/
        public void Render()
        {
            // match '((propertyName))', '(( propertyName ))', etc.
            var re = new Regex(@"\(\(\s*(\w*)\s*\)\)");
            var hasError = false;
            var _template = Template.Trim('\n').Split('\n').ToList();
            var lineNumber = 0;

            while (lineNumber < _template.Count)
            {
                var line = _template[lineNumber];
                var match = re.Match(line);

                // Done matching current line, move to next. This approach is easier to understand than
                // a regex statement for capturing repeated groups and parsing a nested capture.
                if (!match.Success)
                {
                    lineNumber++;
                    continue;
                }

                var prop = match.Groups[1].Value;
                var val = this.GetType().GetProperty(prop)?.GetValue(this);

                // NOTE: This will skip over any missing properties declared after the first missing property on a line.
                if (val == null)
                {
                    Console.WriteLine($"Error rendering template for {this.GetType().Name}: Missing property {prop} on line {lineNumber}");
                    Console.WriteLine($">>> {line}");
                    hasError = true;
                    lineNumber++;
                    continue;
                }

                var matchString = match.Groups[0].Value;
                var matchPos = line.IndexOf(matchString);
                // matchPos should never be -1 (indexOf fail to find). If it is, then fail catastrophically trying to Substring with -1.
                _template[lineNumber] = line.Substring(0, matchPos) +
                                        JsonSerializer.Serialize(val).Trim('"') +
                                        line.Substring(matchPos + matchString.Length);
            }

            if (hasError)
            {
                throw new Exception("Error rendering template, see details above.");
            }

            Rendered = _template;
            IsRendered = true;
        }

        public void Write(string outputPath)
        {
            var dirs = Path.GetDirectoryName(outputPath);
            Directory.CreateDirectory(dirs);
            File.WriteAllLines(outputPath, Rendered);
        }

        public virtual void Write()
        {
            throw new NotImplementedException();
        }
    }
}
