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
        
        public static void Main()
        {
            var generator = new Generator();
            var typeStrings = string.Join(", ", ResourceTypes.Select(t => t.Name));
            Console.WriteLine("Which resource would you like to generate?");
            Console.WriteLine($"Available types are: {typeStrings}");
            // var resourceTypeName = generator.Prompt<string>();
            var resourceTypeName = "Job";

            Console.WriteLine(resourceTypeName);
            var resourceType = ResourceTypes.Where(t => t.Name == resourceTypeName).First();
            var resource = (Resource)Activator.CreateInstance(resourceType);
            Console.WriteLine($"{resource.Properties().Count()}");
            foreach (var prop in resource.Properties())
            {
                Console.WriteLine($"{prop.Name}");
            }
            foreach (var prop in resource.OptionalProperties())
            {
                Console.WriteLine($"{prop.Name}");
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
                Console.WriteLine("Enter another value (y/n)?");
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
