using System;
using System.Collections.Generic;
using System.IO;

namespace Stress.Generator
{
    public abstract class BaseJob : Resource
    {
        public const string JobTemplate = @"
{{- include 'stress-test-addons.(( TemplateInclude )).from-pod' (list . 'stress.(( Name ))') -}}
{{- define 'stress.(( Name ))' -}}
metadata:
  labels:
    testInstance: '(( Name ))-{{ .Release.Name }}-{{ .Release.Revision }}'
    testName: (( Name ))
    chaos: (( ChaosEnabled ))
spec:
  containers:
    - name: (( Name ))
      command: (( Command ))
      image: {{ default '' .Values.repository }}/(( Name )):{{ default 'v1' .Values.tag }}
      {{- include 'stress-test-addons.container-env' . | nindent 6 }}
{{- end -}}
";

        [ResourceProperty("Test name")]
        public string Name { get; set; }

        [ResourceProperty("Test image")]
        public string Image { get; set; }

        [ResourceProperty("Container command. If using multiple scenarios, use a template like `node dist/{{ .Scenario }}.js`")]
        public List<string> Command { get; set; }

        [ResourceProperty("Set if job should support chaos")]
        public bool ChaosEnabled { get; set; }

        public override void Write()
        {
            Write(Path.Join("templates", $"{Name}-job.yaml"));
        }

        public BaseJob() : base(JobTemplate)
        {
        }
    }

    public class Job : BaseJob
    {
        public string TemplateInclude { get; set; } = "env-job-template";
    }

    public class AzureDeploymentJob : BaseJob
    {
        public string TemplateInclude { get; set; } = "deploy-job-template";
    }
}
