using System;

namespace Stress.Generator
{
    public interface IPrompter
    {
        public string Prompt();
    }

    public class Prompter : IPrompter
    {
        public string Prompt()
        {
            return Console.ReadLine() ?? "";
        }
    }
}