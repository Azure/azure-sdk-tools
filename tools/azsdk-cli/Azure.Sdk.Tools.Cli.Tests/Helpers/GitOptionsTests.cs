// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class GitOptionsTests
{
    [Test]
    public void StructuredConstructor_ForwardsEnvironmentWithoutChangingProcessEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("GIT_INDEX_FILE");
        var environment = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = "isolated.index" };
        var options = new GitOptions(["read-tree", "HEAD"], Environment.CurrentDirectory, environmentVariables: environment);

        Assert.That(options.EnvironmentVariables, Is.SameAs(environment));
        Assert.That(options.SubCommand, Is.EqualTo("read-tree"));
        Assert.That(options.LogOutputStream, Is.False);
        Assert.That(options.OutputEncoding, Is.EqualTo(Encoding.UTF8));
        Assert.That(Environment.GetEnvironmentVariable("GIT_INDEX_FILE"), Is.EqualTo(previous));
    }

    [Test]
    public void StringConstructor_ForwardsEnvironmentAndExistingOptions()
    {
        var environment = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = "isolated.index" };
        var timeout = TimeSpan.FromSeconds(11);
        var options = new GitOptions("read-tree HEAD", Environment.CurrentDirectory, true, timeout, environment);

        Assert.That(options.EnvironmentVariables, Is.SameAs(environment));
        Assert.That(options.SubCommand, Is.EqualTo("read-tree"));
        Assert.That(options.LogOutputStream, Is.True);
        Assert.That(options.Timeout, Is.EqualTo(timeout));
        Assert.That(options.OutputEncoding, Is.EqualTo(Encoding.UTF8));
    }

    [Test]
    public void Constructors_KeepEnvironmentOptional()
    {
        Assert.That(new GitOptions(["status"], Environment.CurrentDirectory).EnvironmentVariables, Is.Null);
        Assert.That(new GitOptions("status", Environment.CurrentDirectory).EnvironmentVariables, Is.Null);
        Assert.That(new ProcessOptions("program", []).OutputEncoding, Is.Null);
    }

    [Test]
    public void OutputEncoding_PreservesLegacyInterfaceImplementationsAndGitOverride()
    {
        IProcessOptions legacy = new LegacyProcessOptions();
        IProcessOptions git = new GitOptions(["status"], Environment.CurrentDirectory);

        Assert.That(legacy.OutputEncoding, Is.Null);
        Assert.That(git.OutputEncoding, Is.EqualTo(Encoding.UTF8));
    }

    private sealed class LegacyProcessOptions : IProcessOptions
    {
        public string Command => "program";
        public List<string> Args { get; } = [];
        public string WorkingDirectory => Environment.CurrentDirectory;
        public TimeSpan Timeout => ProcessOptions.DEFAULT_PROCESS_TIMEOUT;
        public bool LogOutputStream => false;
        public string ShortName => Command;
        public IDictionary<string, string>? EnvironmentVariables => null;
    }
}
