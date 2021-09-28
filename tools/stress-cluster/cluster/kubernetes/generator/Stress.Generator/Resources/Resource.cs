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

        public IEnumerable<ResourcePropertyInfo> Properties()
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute(typeof(ResourceProperty)) != null)
                   .Select(p =>
                   {
                       var rp = p.GetCustomAttribute(typeof(ResourceProperty)) as ResourceProperty;
                       return new ResourcePropertyInfo(p, rp);
                   });
        }

        public IEnumerable<ResourcePropertyInfo> OptionalProperties()
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute(typeof(OptionalResourceProperty)) != null)
                   .Select(p =>
                   {
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
            var _template = Template.Split('\n');

            for (var lineNumber = 0; lineNumber < _template.Count(); lineNumber++)
            {
                var line = _template[lineNumber];
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
                    Console.WriteLine($"Error rendering template for {this.GetType().Name}: Missing property {prop} on line {lineNumber}");
                    Console.WriteLine($">>> {line}");
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

        public virtual void Write()
        {
            throw new NotImplementedException();
        }
    }
}
