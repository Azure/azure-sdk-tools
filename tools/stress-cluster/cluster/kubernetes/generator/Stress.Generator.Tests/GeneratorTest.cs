using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Stress.Generator;

namespace Stress.Generator.Tests
{
    public class TestPrompter : IPrompter
    {
        public Queue<string> PromptValues;
        
        public void SetList(List<string> promptValues)
        {
            PromptValues = new Queue<string>();
            for (var i = 0; i < promptValues.Count; i++)
            {
                PromptValues.Enqueue(promptValues[i]);
                if (i == promptValues.Count - 1)
                {
                    PromptValues.Enqueue("n");
                }
                else
                {
                    PromptValues.Enqueue("y");
                }
            }
        }

        public void SetString(string promptValue)
        {
            PromptValues = new Queue<string>();
            PromptValues.Enqueue(promptValue);
        }

        public string Prompt()
        {
            return PromptValues.Dequeue();
        }
    }

    public class GeneratorTests
    {
        [Fact]
        public void PromptString()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);
            
            prompter.SetString("stringvalue");
            generator.Prompt<string>().Should().Be("stringvalue");

            prompter.SetString("1.5");
            generator.Prompt<double>().Should().Be(1.5);
            prompter.SetString("1");
            generator.Prompt<double>().Should().Be(1);

            prompter.SetString("true");
            generator.Prompt<bool>().Should().Be(true);
            prompter.SetString("false");
            generator.Prompt<bool>().Should().Be(false);

            prompter.SetList(new List<string>{"itemvalue1", "itemvalue2", "itemvalue3"});
            List<string> list = generator.PromptList<string>();
            list.Count.Should().Be(3);
            list[0].Should().Be("itemvalue1");
            list[1].Should().Be("itemvalue2");
            list[2].Should().Be("itemvalue3");
        }
    }
}
