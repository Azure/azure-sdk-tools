using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using k8s.Models;

namespace Stress.Watcher
{
    public class GenericChaosResource : CustomResource<ChaosResourceSpec, ChaosResourceStatus>
    {
        public static string PauseAnnotationKey = "experiment.chaos-mesh.org/pause";

        public static string TestInstanceLabelKey = "testInstance";

        public bool IsPaused()
        {
            var paused = "";
            Metadata.Annotations?.TryGetValue(GenericChaosResource.PauseAnnotationKey, out paused);
            return paused == "true";
        }

        public override string ToString()
        {
            var labels = "";
            foreach (var kvp in Metadata.Labels)
            {
                labels += kvp.Key + " : " + kvp.Value + ", ";
            }
            labels = labels.TrimEnd(',', ' ');
            return $"{Metadata.Name} Status: { CStatus.Experiment.phase }, Labels: {{ {labels} }}, Spec: {{ {Spec.Selector} }}";
        }
    }

    public class ChaosResourceSpec
    {
        [JsonProperty(PropertyName = "selector")]
        public ChaosSelector Selector { get; set; }

        [JsonProperty(PropertyName = "networkChaos")]
        public ChaosResourceSpec NetworkChaos { get; set; }
        
        [JsonProperty(PropertyName = "stressChaos")]
        public ChaosResourceSpec StressChaos { get; set; }

        [JsonProperty(PropertyName = "httpChaos")]
        public ChaosResourceSpec HttpChaos { get; set; }

        [JsonProperty(PropertyName = "ioChaos")]
        public ChaosResourceSpec IoChaos { get; set; }

        [JsonProperty(PropertyName = "kernelChaos")]
        public ChaosResourceSpec KernelChaos { get; set; }

        [JsonProperty(PropertyName = "timeChaos")]
        public ChaosResourceSpec TimeChaos { get; set; }

        [JsonProperty(PropertyName = "jvmChaos")]
        public ChaosResourceSpec JvmChaos { get; set; }

        public string GetTestInstance()
        {
            bool isInvalidSpec = true;
            foreach (PropertyInfo property in this.GetType().GetProperties()) {
                if (property.GetValue(this, null) != null) {
                    isInvalidSpec = false;
                    break;
                }
            }
            if (isInvalidSpec) return null;

            string testInstance = null;
            foreach (PropertyInfo property in this.GetType().GetProperties()) {
                if (property.PropertyType == typeof(ChaosSelector)) {
                    string ti = Selector?.LabelSelectors?.TestInstance;
                    testInstance = (ti == null) ? testInstance : ti;
                } else if (property.PropertyType == typeof(ChaosResourceSpec)) {
                    ChaosResourceSpec chaosSpec = (ChaosResourceSpec)property.GetValue(this, null);
                    string ti = chaosSpec?.GetTestInstance();
                    testInstance = (ti == null) ? testInstance : ti;
                }
            }
            return testInstance;
        }
    }

    public class ChaosSelector
    {
        public ChaosLabelSelectors LabelSelectors { get; set; }
        public List<string> Namespaces { get; set; }

        public override string ToString()
        {
            return $"Namespaces: {String.Join(',', Namespaces)}, {LabelSelectors}";
        }
    }

    public class ChaosLabelSelectors
    {
        public string TestInstance { get; set; }

        public override string ToString()
        {
            return $"TestInstance: {TestInstance}";
        }
    }

    public class ChaosResourceStatus : V1Status
    {
        [JsonProperty(PropertyName = "experiment", NullValueHandling = NullValueHandling.Ignore)]
        public ChaosExperimentStatus Experiment { get; set; }
    }

    public class ChaosExperimentStatus
    {
        public string duration { get; set; }
        public DateTime endTime { get; set; }
        public string phase { get; set; }
        public DateTime startTime { get; set; }
    }
}
