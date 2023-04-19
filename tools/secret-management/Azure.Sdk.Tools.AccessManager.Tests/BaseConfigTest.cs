using NUnit.Framework;
using FluentAssertions;
using Moq;
using Microsoft.Graph.Models;

namespace Azure.Sdk.Tools.AccessManager.Tests;

public class BaseConfigTest
{
    public SortedDictionary<string, string> Properties { get; set; } = default!;

    [SetUp]
    public void BeforeEach()
    {
        Properties = new SortedDictionary<string, string>
        {
            { "testProperty1", "foo" },
            { "testProperty2", "bar" }
        };
    }

    [Test]
    public void TestStringRender()
    {
        var config = new StringConfig()
        {
            Foo = "foo {{ testProperty1 }}",
            Bar = "bar {{testProperty2}}{{testProperty2 }} {{ testProperty2}} prop-{{  testProperty2      }} {{       testProperty2  }}"
        };

        var unrendered = config.Render(Properties);

        unrendered.Count().Should().Be(0);
        config.Foo.Should().Be("foo foo");
        config.Bar.Should().Be("bar barbar bar prop-bar bar");

        config.Foo = "foo missing val {{ testPropertyDoesNotExist1 }}";
        config.Bar = "bar missing val {{ testPropertyDoesNotExist1 }} {{ testPropertyDoesNotExist2 }} {{ testPropertyDoesNotExist3 }}";
        unrendered = config.Render(Properties);
        unrendered.Count().Should().Be(3);
    }

    [Test]
    public void TestListRender()
    {
        var config = new ListConfig()
        {
            Foo = new List<string>{ "foo a {{ testProperty1 }}", "foo b {{ testProperty1 }}", "foo multi 1: {{ testProperty1 }} 2: {{ testProperty2 }}" },
            Bar = new List<string>{ "bar {{ testProperty1 }}", "do not replace" },
            Baz = new List<string>{ "do not replace" }
        };

        var unrendered = config.Render(Properties);

        unrendered.Count().Should().Be(0);
        config.Foo.Should().BeEquivalentTo(new List<string>{ "foo a foo", "foo b foo", "foo multi 1: foo 2: bar" });
        config.Bar.Should().BeEquivalentTo(new List<string>{ "bar foo", "do not replace" });
        config.Baz.Should().BeEquivalentTo(new List<string>{ "do not replace" });

        config.Foo.Add("foo missing val {{ testPropertyDoesNotExist1 }}");
        config.Bar.Add("bar missing val {{ testPropertyDoesNotExist1 }} {{ testPropertyDoesNotExist2 }} {{ testPropertyDoesNotExist3 }}");
        unrendered = config.Render(Properties);
        unrendered.Count().Should().Be(3);
    }

    [Test]
    public void TestDictionaryRender()
    {
        var config = new DictionaryConfig()
        {
            Foo = new Dictionary<string, string>
            {
                { "foo key", "foo val {{ testProperty1 }}" },
                { "bar key", "bar val multi 1: {{ testProperty1 }} 2: {{ testProperty2 }}" },
                { "do not replace", "do not replace" }
            },
            Bar = new SortedDictionary<string, string>
            {
                { "foo key", "foo val {{ testProperty1 }}" },
                { "bar key", "bar val multi 1: {{ testProperty1 }} 2: {{ testProperty2 }}" },
                { "do not replace", "do not replace" }
            }
        };

        var unrendered = config.Render(Properties);

        unrendered.Count().Should().Be(0);
        config.Foo.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            { "foo key", "foo val foo" },
            { "bar key", "bar val multi 1: foo 2: bar" },
            { "do not replace", "do not replace" }
        });
        config.Bar.Should().BeEquivalentTo(new SortedDictionary<string, string>
        {
            { "foo key", "foo val foo" },
            { "bar key", "bar val multi 1: foo 2: bar" },
            { "do not replace", "do not replace" }
        });

        config.Foo["foo missing key 1"] = "foo missing val {{ testPropertyDoesNotExist1 }}";
        config.Foo["foo missing key 2"] = "foo missing val {{ testPropertyDoesNotExist2 }} {{ testPropertyDoesNotExist3 }}";
        config.Bar["bar missing key 1"] = "bar missing val {{ testPropertyDoesNotExist3 }} {{ testPropertyDoesNotExist4 }}";
        config.Bar["bar missing key 2"] = "bar missing val {{ testPropertyDoesNotExist5 }} {{ testPropertyDoesNotExist6 }}";
        unrendered = config.Render(Properties);
        unrendered.Count().Should().Be(6);
    }
}

public class StringConfig : BaseConfig
{
    public string Foo { get; set; } = string.Empty;
    public string Bar { get; set; } = string.Empty;
}

public class ListConfig : BaseConfig
{
    public List<string> Foo { get; set; } = new List<string>();
    public List<string> Bar { get; set; } = new List<string>();
    public List<string> Baz { get; set; } = new List<string>();
}

public class DictionaryConfig : BaseConfig
{
    public Dictionary<string, string> Foo { get; set; } = new Dictionary<string, string>();
    public SortedDictionary<string, string> Bar { get; set; } = new SortedDictionary<string, string>();
}