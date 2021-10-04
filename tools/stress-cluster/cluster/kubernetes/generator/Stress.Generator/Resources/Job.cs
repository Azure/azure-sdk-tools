using System;
using System.Collections.Generic;
using System.IO;

namespace Stress.Generator
{
    public abstract class BaseJob : Resource
    {
        public override string Template { get; set; } = @"
# This template includes the `metadata` and `spec` fields from the kubernetes Pod schema:
# https://kubernetes.io/docs/reference/generated/kubernetes-api/v1.22/#pod-v1-core
# When rendered by helm, the values here will be embedded within a kubernetes Job manifest.
# https://kubernetes.io/docs/concepts/workloads/controllers/job/
{{- include 'stress-test-addons.(( TemplateInclude )).from-pod' (list . 'stress.(( Name ))') -}}
{{- define 'stress.(( Name ))' -}}
metadata:
  labels:
    # Only pods with a `chaos` label will work with chaos resources and services that require this selector.
    chaos: (( ChaosEnabled ))
    # The testInstance label should also be defined for any chaos resources that need to target this pod.
    testInstance: '(( Name ))-{{ .Release.Name }}-{{ .Release.Revision }}'
    # testName allows for consistent querying across test instances via
    # kubectl commands (e.g. `kubectl logs -l testName=(( Name )) -n <namespace>)
    testName: (( Name ))
spec:
  containers:
    - name: (( Name ))
      command: (( Command ))
      # Only override this if needed for local development, otherwise it will be calculated by deployment scripts.
      image: {{ default '' .Values.repository }}/(( ImageName )):{{ default 'v1' .Values.tag }}
      {{- include 'stress-test-addons.container-env' . | nindent 6 }}
{{- end -}}
";

        public string ImageName { get; set; }

        public string? Name { get; set; }

        [ResourceProperty("Container command. If using multiple scenarios, use a template like `node dist/{{ .Scenario }}.js`")]
        public List<string>? Command { get; set; }

        [ResourceProperty("Set if job should support chaos")]
        public bool? ChaosEnabled { get; set; }

        public override void Write()
        {
            Write(Path.Join("templates", $"{Name}-job.yaml"));
        }

        public BaseJob() : base()
        {
            // Default image name to stress test directory. The deploy-stress-tests.ps1 script also defaults
            // the image name in docker build/push to this.
            ImageName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
        }
    }

    public class JobWithoutAzureResourceDeployment : BaseJob
    {
        public override string Help { get; set; } = "A stress test job that does not require an ARM deployment";

        public string TemplateInclude { get; set; } = "env-job-template";
    }

    public class JobWithAzureResourceDeployment : BaseJob
    {
        public override string Help { get; set; } = "A stress test job that requires test resources created via an ARM deployment";

        public string TemplateInclude { get; set; } = "deploy-job-template";

        private string BicepContents = @"
// Add Bicep file contents here.
// [Overview] https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview
// [Examples] https://github.com/Azure/bicep/tree/main/docs/examples
";

        private string ArmParameterContents = @"
{
  '$schema': 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#',
  'contentVersion': '1.0.0.0',
  'parameters': { }
}
";

        public override void Write()
        {
            base.Write();
            WriteAllText("test-resources.bicep", BicepContents);
            WriteAllText("parameters.json", ArmParameterContents);
        }
    }
}
