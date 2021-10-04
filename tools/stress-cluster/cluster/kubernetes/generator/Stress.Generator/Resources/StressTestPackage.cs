using System;
using System.IO;

namespace Stress.Generator
{
    public class StressTestPackage : Resource
    {
        public override string Template { get; set; } = @"
apiVersion: v2
name: (( Name ))
description: (( Name )) stress test package
version: 0.1.1
appVersion: v0.1
annotations:
  stressTest: 'true'
  namespace: (( Namespace ))

dependencies:
- name: stress-test-addons
  version: 0.1.8
  repository: https://stresstestcharts.blob.core.windows.net/helm/
";

        public override string Help { get; set; } = "Top level information for the stress test package.";

        private string DockerfileContents = @"
# Build your dockerfile here
# Reference docs: https://docs.docker.com/engine/reference/builder/
# Best practices docs: https://docs.docker.com/develop/develop-images/dockerfile_best-practices/
";

        private string SrcReadmeContents = @"
Place files relevant to your application code within this directory
";

        private string HelmIgnoreContents = @"
# Add files that should not be included in the stress test helm package.
# The package should consist mainly of yaml manifests and bicep/arm templates.
# Docs: https://helm.sh/docs/chart_template_guide/helm_ignore_file/

src/
";

        private string ValuesContents = @"
# Leave this file empty unless you plan to generate job files in a loop for a list of scenarios.
# See https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#scenarios-and-valuesyaml
";

        private string ReadmeContents = @"
This is a stress test package, used for deploying and testing Azure SDKs in real world scenarios.
Docs: https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md
";

        public string Namespace => new DirectoryInfo(Directory.GetCurrentDirectory()).Name;

        [ResourceProperty("Stress Test Name")]
        public string? Name { get; set; }

        [NestedResourceProperty(
            "Which resource(s) would you like to generate? Available resources are:",
            new Type[]{
                typeof(JobWithoutAzureResourceDeployment),
                typeof(JobWithAzureResourceDeployment),
                typeof(NetworkChaos),
            }
        )]
        public IResource? StressTestResource { get; set; }

        public override void Write()
        {
            Write(Path.Join($"Chart.yaml"));

            File.WriteAllText("Dockerfile", DockerfileContents);
            Directory.CreateDirectory("src");
            File.WriteAllText(Path.Join("src", "README.md"), SrcReadmeContents);
            File.WriteAllText(".helmignore", HelmIgnoreContents);
            File.WriteAllText("values.yaml", ValuesContents);
            File.WriteAllText("README.md", ReadmeContents);
        }
    }
}