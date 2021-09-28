using System;
using System.Collections.Generic;
using System.Linq;

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

        public List<Resource> GenerateResources()
        {
            var resources = new List<Resource>();
            PromptMultiple(() => resources.Add(GenerateResource()), "Enter another resource?");
            return resources;
        }

        public Resource GenerateResource()
        {
            var resourceTypeNames = ResourceTypes.Select(t => t.Name).ToList();
            var selection = PromptEnum(resourceTypeNames, "Which resource would you like to generate? Available resources are:");
            var resourceType = ResourceTypes.Where(t => t.Name == selection);
            var resource = (Resource)Activator.CreateInstance(resourceType.First());

            foreach (var prop in resource.Properties())
            {
                PromptSetProperty(resource, prop);
            }

            foreach (var prop in resource.OptionalProperties())
            {
                PromptSetOptionalProperty(resource, prop);
            }

            return resource;
        }

        public void PromptSetOptionalProperty(Resource resource, ResourcePropertyInfo prop)
        {
            var set = "";
            while (set != "y" && set != "n")
            {
                set = Prompt<string>($"Set a value for optional property {prop.Info.Name}? (y/n): ");
            }
            if (set == "y")
            {
                PromptSetProperty(resource, prop);
            }
        }

        public void PromptSetProperty(Resource resource, ResourcePropertyInfo prop)
        {
            Console.WriteLine($"--> {prop.Info.Name} ({prop.Property.Help})");

            if (prop.Info.PropertyType == typeof(string))
            {
                resource.SetProperty(prop.Info, Prompt<string>($"(string): "));
            }
            else if (prop.Info.PropertyType == typeof(double))
            {
                resource.SetProperty(prop.Info, Prompt<double>($"(number): "));
            }
            else if (prop.Info.PropertyType == typeof(bool))
            {
                resource.SetProperty(prop.Info, Prompt<bool>($"(true/false): "));
            }
            else if (prop.Info.PropertyType == typeof(List<string>))
            {
                resource.SetProperty(prop.Info, PromptList<string>($"(list item string): "));
            }
            else
            {
                throw new Exception("Unsupported value type: {property.PropertyType.Name}");
            }
        }

        public string PromptEnum(List<string> options, string promptMessage)
        {
            var selected = "";
            while (string.IsNullOrEmpty(selected))
            {
                Console.WriteLine(promptMessage);
                for (int i = 0; i < options.Count; i++)
                {
                    Console.WriteLine($"    ({i}) {options[i]}");
                }

                var optionSelection = Prompt<string>();
                if (uint.TryParse(optionSelection, out var idx) && idx < options.Count)
                {
                    optionSelection = ResourceTypes[(int)idx].Name;
                }
                selected = options.Find(o => o == optionSelection);
            }

            return selected;
        }

        public List<T> PromptList<T>(string promptMessage = "(list item value):")
        {
            var values = new List<T>();
            PromptMultiple(() => values.Add(Prompt<T>(promptMessage)), "Enter another value?");
            return values;
        }

        public T Prompt<T>(string promptMessage = "(value): ")
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

        public void PromptMultiple(Action promptAction, string message)
        {
            var another = "y";
            while (true)
            {
                if (another == "n")
                {
                    break;
                }
                else if (another == "y")
                {
                    promptAction();
                }

                Console.Write(message + " (y/n): ");
                another = Prompter.Prompt();
            }
        }
    }
}
