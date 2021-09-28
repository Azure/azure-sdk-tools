using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stress.Generator
{
    public class Generator
    {
        public IPrompter Prompter;

        public static List<Type> ResourceTypes = new List<Type>{ typeof(Job), typeof(NetworkChaos) };

        public Generator(IPrompter prompter = null)
        {
            Prompter = prompter ?? new Prompter();
        }

        public static void Main()
        {
            var generator = new Generator();
            var resources = generator.GenerateResources();

            Console.WriteLine("Done");
        }

        public List<Resource> GenerateResources()
        {
            var resources = new List<Resource>();

            var another = "y";
            while (true)
            {
                if (another == "n")
                {
                    break;
                }
                else if (another == "y")
                {
                    resources.Add(GenerateResource());
                }

                Console.Write("Enter another resource? (y/n): ");
                another = Prompter.Prompt();
            }

            return resources;
        }


        public Resource GenerateResource()
        {
            var typeStrings = string.Join(", ", ResourceTypes.Select(t => t.Name));

            IEnumerable<Type> resourceType = new List<Type>();
            while (resourceType.Count() == 0)
            {
                Console.WriteLine("Which resource would you like to generate?");
                Console.WriteLine($"Available types are: {typeStrings}");
                var resourceTypeName = Prompt<string>();
                resourceType = ResourceTypes.Where(t => t.Name == resourceTypeName);
            }

            var resource = (Resource)Activator.CreateInstance(resourceType.First());

            foreach (var prop in resource.Properties())
            {
                PromptSetProperty(resource, prop);
            }

            foreach (var prop in resource.OptionalProperties())
            {
                var set = "";
                while (set != "y" && set != "n")
                {
                    set = Prompt<string>($"Set a value for optional property {prop.Name}? (y/n): ");
                }
                if (set == "y")
                {
                    PromptSetProperty(resource, prop);
                }
            }

            return resource;
        }

        public void PromptSetProperty(Resource resource, PropertyInfo property)
        {
            if (property.PropertyType == typeof(string))
            {
                resource.SetProperty(property, Prompt<string>($"Enter value for {property.Name}: "));
            }
            else if (property.PropertyType == typeof(double))
            {
                resource.SetProperty(property, Prompt<double>($"Enter number value for {property.Name}: "));
            }
            else if (property.PropertyType == typeof(bool))
            {
                resource.SetProperty(property, Prompt<bool>($"Enter true/false for {property.Name}: "));
            }
            else if (property.PropertyType == typeof(List<string>))
            {
                resource.SetProperty(property, PromptList<string>($"Enter first value for {property.Name} list: "));
            }
            else
            {
                throw new Exception("Unsupported value type: {property.PropertyType.Name}");
            }
        }

        public List<T> PromptList<T>(string promptMessage = "Enter Value:")
        {
            var values = new List<T>();
            values.Add(Prompt<T>());
            var another = "";
            while (true)
            {
                if (another == "n")
                {
                    break;
                }
                if (another == "y")
                {
                    values.Add(Prompt<T>());
                }
                Console.Write("Enter another value? (y/n): ");
                another = Prompter.Prompt();
            }
            return values;
        }

        public T Prompt<T>(string promptMessage = "Enter Value: ")
        {
            var retryMessage = $"Invalid value, expected {typeof(T)}";
            Console.Write(promptMessage);
            var value = Prompter.Prompt();

            if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }

            if (typeof(T) == typeof(double))
            {
                if (!double.TryParse(value, out double parsed))
                {
                    return Prompt<T>(retryMessage);
                }
                return (T)(object)parsed;
            }

            if (typeof(T) == typeof(bool))
            {
                if (!bool.TryParse(value, out bool parsed))
                {
                    return Prompt<T>(retryMessage);
                }
                return (T)(object)parsed;
            }

            throw new Exception($"Unsupported value {typeof(T)}");
        }
    }

    public class Prompter : IPrompter
    {
        public string Prompt()
        {
            return Console.ReadLine();
        }
    }

    public interface IPrompter
    {
        public string Prompt();
    }
}
