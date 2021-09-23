using System;
using System.Collections.Generic;

namespace Stress.Generator
{
    public class generator
    {
        public IPrompter Prompter;

        public void Generate(IPrompter prompter = null)
        {
            Prompter = prompter ?? new Prompter();
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
                another = Console.ReadLine();
            }
            return values;
        }
        
        public T Prompt<T>(string promptMessage = "Enter Value:")
        {
            var retryMessage = $"Invalid value, expected {typeof(T)}";
            Console.WriteLine(promptMessage);
            var value = Console.ReadLine(); 

            if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }

            if (typeof(T) == typeof(float))
            {
                if (!float.TryParse(value, out float parsed))
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
