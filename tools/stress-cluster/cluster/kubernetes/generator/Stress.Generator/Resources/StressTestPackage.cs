using System;
using System.Collections.Generic;
using System.Linq;
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
  namespace: (( Namespace ))
  stressTest: 'true'  # do not change, required for deployment auto-discovery

dependencies:
- name: stress-test-addons
  version: 0.1.8
  repository: https://stresstestcharts.blob.core.windows.net/helm/
";

        public override string Help { get; set; } = "Top level information for the stress test package.";

        private string SrcReadmeContents = @"
Place files relevant to your application code within this directory
";

        private string DockerfileContents = @"
# Build your Dockerfile here
# Reference docs: https://docs.docker.com/engine/reference/builder/
# Best practices docs: https://docs.docker.com/develop/develop-images/dockerfile_best-practices/

FROM <appropriate base image, e.g. node:12-alpine>

RUN mkdir /src
ADD ./src /src

WORKDIR /src
RUN <your build commands>

CMD [""your"", ""app"", ""command""];
";

        private string HelmIgnoreContents = @"
# Add files that should not be included in the stress test helm package.
# The package should consist mainly of yaml manifests and bicep/arm templates.
# Docs: https://helm.sh/docs/chart_template_guide/helm_ignore_file/

src/
.env
";

        private string ValuesContents = @"
# Leave this file empty unless you plan to support multiple test cases and want to avoid duplicating yaml.
# See https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#scenarios-and-valuesyaml

# scenarios:
#   - myScenarioA
#   - myScenarioB
#   - myScenarioC
";

        private string ReadmeContents = @"
This is a stress test package, used for deploying and testing Azure SDKs in real world scenarios.
Docs: https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md

Examples:
  - https://github.com/Azure/azure-sdk-tools/tree/main/tools/stress-cluster/chaos/examples
  - https://github.com/Azure/azure-sdk-for-python/tree/main/sdk/eventhub/azure-eventhub/stress
  - https://github.com/Azure/azure-sdk-for-js/tree/main/sdk/servicebus/service-bus/test/stress
  - https://github.com/Azure/azure-sdk-for-net/tree/feature/storage/chaos-testing-prototype/sdk/storage/chaos
";

        [ResourceProperty("Stress Test Name")]
        public string? Name { get; set; }

        [ResourceProperty("Stress Test Namespace (e.g. language name or alias for development)")]
        public string? Namespace { get; set; }

        [NestedResourceProperty(
            "Which resource(s) would you like to generate? Available resources are:",
            new Type[]{
                typeof(JobWithoutAzureResourceDeployment),
                typeof(JobWithAzureResourceDeployment),
                typeof(NetworkChaos),
            }
        )]
        public List<IResource>? Resources { get; set; }

        public override void Render()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new Exception($"Property {this.GetType().Name}{nameof(Name)} cannot be empty.");
            }

            foreach (var resource in Resources ?? Enumerable.Empty<IResource>())
            {
                var prop = resource.GetType().GetProperty(nameof(Name));
                if (prop != null)
                {
                    resource.SetProperty(prop, Name);
                }

                resource.Render();
            }

            base.Render();
        }

        public override void Write()
        {
            Write("Chart.yaml");

            Directory.CreateDirectory("src");
            WriteAllText(Path.Join("src", "README.md"), SrcReadmeContents);
            WriteAllText("Dockerfile", DockerfileContents);
            WriteAllText(".helmignore", HelmIgnoreContents);
            WriteAllText("values.yaml", ValuesContents);
            WriteAllText("README.md", ReadmeContents);

            foreach (var resource in Resources ?? Enumerable.Empty<IResource>())
            {
                resource.Write();
            }
        }
    }
}