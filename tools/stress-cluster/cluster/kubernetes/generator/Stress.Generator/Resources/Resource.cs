using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.IO;

namespace Stress.Generator
{
    public abstract class Resource
    {
        public List<string> Template;
        public List<string> Rendered;
        public bool IsRendered = false;

        public Resource(params string[] templates)
        {
            Template = string.Join('\n', templates).Split('\n').ToList();
            Rendered = new List<string>();
        }

        public IEnumerable<ResourcePropertyInfo> Properties()
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute(typeof(ResourceProperty)) != null)
                   .Select(p => {
                       var rp = p.GetCustomAttribute(typeof(ResourceProperty)) as ResourceProperty;
                       return new ResourcePropertyInfo(p, rp);
                   });
        }

        public IEnumerable<ResourcePropertyInfo> OptionalProperties()
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute(typeof(OptionalResourceProperty)) != null)
                   .Select(p => {
                       var rp = p.GetCustomAttribute(typeof(OptionalResourceProperty)) as OptionalResourceProperty;
                       return new ResourcePropertyInfo(p, rp);
                   });
        }

        public void SetProperty(PropertyInfo prop, object value)
        {
            prop.SetValue(this, value);
        }

        // Pulling in a large templating library like Razor is more trouble than it's worth
        // and other popular mustache-style templating libraries don't have great support
        // for overridding the delimeters from curly braces, in order to not overlap
        // with helm. It ends up being simpler to handle it ourselves :/
        public void Render()
        {
            var expr = new Regex(@"\(\(\s*(\w*)\s*\)\)");
            var hasError = false;
            var _rendered = new List<string>();

            for (var lineNumber = 0; lineNumber < Template.Count(); lineNumber++)
            {
                var line = Template[lineNumber];
                var match = expr.Match(line);
                if (!match.Success)
                {
                    _rendered.Add(line);
                    continue;
                }
                var prop = match.Groups[1].Value;
                var val = this.GetType().GetProperty(prop)?.GetValue(this);
                if (val == null)
                {
                    Console.WriteLine($"Missing property {prop} on line {lineNumber}");
                    Console.WriteLine($">>> line");
                    hasError = true;
                    _rendered.Add(line);
                    continue;
                }

                var replaced = line.Replace(match.Groups[0].ToString(), JsonSerializer.Serialize(val).Trim('"'));
                _rendered.Add(replaced);
            }

            if (hasError)
            {
                throw new Exception("Error rendering template, see details above.");
            }

            Rendered = _rendered;
            IsRendered = true;
        }

        public void Write(string outputPath)
        {
            var dirs = Path.GetDirectoryName(outputPath);
            Directory.CreateDirectory(dirs);
            File.WriteAllLines(outputPath, Rendered);
        }

        public abstract void Write();
    }
}
