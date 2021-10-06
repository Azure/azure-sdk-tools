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
        List<string> Rendered { get; set; }
        IEnumerable<ResourcePropertyInfo<ResourceProperty>> Properties();
        IEnumerable<ResourcePropertyInfo<OptionalResourceProperty>> OptionalProperties();
        IEnumerable<ResourcePropertyInfo<NestedResourceProperty>> NestedProperties();
        IEnumerable<ResourcePropertyInfo<OptionalNestedResourceProperty>> OptionalNestedProperties();
        void Render();
        void SetProperty(PropertyInfo prop, object value);
        void Write(string outputPath);
        void Write();
    }

    public abstract class Resource : IResource
    {
        public List<string> Rendered { get; set; }
        public bool IsRendered = false;
        public abstract string Template { get; set; }
        public abstract string Help { get; set; }

        public Resource()
        {
            Rendered = new List<string>();
        }

        public IEnumerable<ResourcePropertyInfo<T>> TProperties<T>()
            where T : BaseResourceProperty
        {
            return this.GetType().GetProperties()
                   .Where(p =>  p.GetCustomAttribute<T>() != null)
#nullable disable
                   .Select(p => new ResourcePropertyInfo<T>(p, p.GetCustomAttribute<T>()));
#nullable enable
        }

        public IEnumerable<ResourcePropertyInfo<ResourceProperty>> Properties() => TProperties<ResourceProperty>();

        public IEnumerable<ResourcePropertyInfo<OptionalResourceProperty>> OptionalProperties() => TProperties<OptionalResourceProperty>();

        public IEnumerable<ResourcePropertyInfo<NestedResourceProperty>> NestedProperties() => TProperties<NestedResourceProperty>();

        public IEnumerable<ResourcePropertyInfo<OptionalNestedResourceProperty>> OptionalNestedProperties() => TProperties<OptionalNestedResourceProperty>();

        public void SetProperty(PropertyInfo prop, object value)
        {
            if (value == null)
            {
                throw new NullReferenceException($"Cannot set property {this.GetType().Name}.{prop.PropertyType.Name} to null");
            }
            var source = this.GetType().Name;
            var dest = value.GetType().Name;
            var t = prop.PropertyType.Name;
            prop.SetValue(this, value);
        }

        // Pulling in a large templating library like Razor is more trouble than it's worth
        // and other popular mustache-style templating libraries don't have great support
        // for overridding the delimeters from curly braces, in order to not overlap
        // with helm. It ends up being simpler to handle it ourselves :/
        public virtual void Render()
        {
            // match '((propertyName))', '(( propertyName ))', etc.
            var re = new Regex(@"\(\(\s*(\w*)\s*\)\)");
            var hasError = false;
            var _template = Template.Trim('\n').Split('\n').ToList();
            var lineNumber = 0;
            var sentinel = ";;EXCLUDE;;";

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

                var propName = match.Groups[1].Value;
                var prop = this.GetType().GetProperty(propName);

                // NOTE: This will skip over any missing properties declared after the first missing property on a line.
                if (prop == null)
                {
                    Console.WriteLine($"Error rendering template for {this.GetType().Name}: Missing property {prop} on line {lineNumber}");
                    Console.WriteLine($">>> {line}");
                    hasError = true;
                    lineNumber++;
                    continue;
                }

                var val = prop.GetValue(this);

                // Exclude lines with optional/nullable properties that aren't set
                if (val == null)
                {
                    _template[lineNumber] = sentinel;
                    lineNumber++;
                    continue;
                }

                var resourceVal = val as IResource;
                if (resourceVal != null)
                {
                    try
                    {
                        resourceVal.Render();
                    }
                    catch (Exception)
                    {
                        hasError = true;
                    }
                    val = string.Join('\n', resourceVal.Rendered);
                }
                else
                {
                    val = JsonSerializer.Serialize(val).Trim('"');
                }

                var matchString = match.Groups[0].Value;
                var matchPos = line.IndexOf(matchString);
                // matchPos should never be -1 (indexOf fail to find). If it is, then fail catastrophically trying to Substring with -1.
                _template[lineNumber] = line.Substring(0, matchPos) + val + line.Substring(matchPos + matchString.Length);
            }

            if (hasError)
            {
                throw new Exception("Error rendering template, see details above.");
            }

            Rendered = _template.Where(l => l != sentinel).ToList();
            IsRendered = true;
        }

        public void WriteAllText(string outputPath, string text)
        {
            Console.WriteLine($"Writing {outputPath}");
            File.WriteAllText(outputPath, text.TrimStart('\n'));
        }

        public void Write(string outputPath)
        {
            if (!IsRendered)
            {
                throw new Exception($"Render() must be called before Write() for {GetType().Name}.");
            }

            var dirs = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dirs))
            {
                Directory.CreateDirectory(dirs);
            }
            Console.WriteLine($"Writing {outputPath}");
            File.WriteAllLines(outputPath, Rendered);
        }

        public virtual void Write()
        {
            throw new NotImplementedException();
        }
    }
}
