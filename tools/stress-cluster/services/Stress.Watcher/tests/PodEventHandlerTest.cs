using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Stress.Watcher.Extensions;
using k8s;
using k8s.Models;

namespace Stress.Watcher.Tests
{
    public class PodEventHandlerTest
    {
        private GenericChaosResource CreateChaosResource(string ns, string testInstance, bool paused)
        {
            var chaos = new GenericChaosResource
            {
                Metadata = new V1ObjectMeta(),
                Spec = new ChaosResourceSpec
                {
                    Selector = new ChaosSelector
                    {
                        Namespaces = new List<string> { ns },
                        LabelSelectors = new ChaosLabelSelectors
                        {
                            TestInstance = testInstance
                        }
                    }
                }
            };

            if (paused)
            {
                chaos.Metadata.Annotations = new Dictionary<string, string>
                {
                    { GenericChaosResource.PauseAnnotationKey, "true" }
                };
            }

            return chaos;
        }

        private V1Pod CreatePod(string ns, string testInstance)
        {
            return new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    NamespaceProperty = ns,
                    Name = "testpod",
                    Labels = new Dictionary<string, string>
                    {
                        { GenericChaosResource.TestInstanceLabelKey, testInstance },
                        { "chaos", "true" }
                    },
                    Annotations = new Dictionary<string, string>()
                },
                Status = new V1PodStatus()
            };
        }

        [Fact]
        public void TestShouldStartChaos()
        {
            var handler = new PodEventHandler(null, null, null);
            var pod = CreatePod("testns", "pod-test-instance");

            var noStart1 = CreateChaosResource("testnostart1", "test-no-start-1", false);
            handler.ShouldStartChaos(noStart1, pod).Should().BeFalse();

            var noStart2 = CreateChaosResource(pod.Namespace(), pod.TestInstance(), false);
            handler.ShouldStartChaos(noStart2, pod).Should().BeFalse();

            var start1 = CreateChaosResource(pod.Namespace(), pod.TestInstance(), true);
            handler.ShouldStartChaos(start1, pod).Should().BeTrue();
        }

        [Fact]
        public void TestShouldStartPodChaos()
        {
            var handler = new PodEventHandler(null, null, null);
            var pod = CreatePod("testns", "pod-test-instance");

            pod.Status.Phase = "Pending";
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeFalse();
            pod.Status.Phase = "Deleting";
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeFalse();

            pod.Status.Phase = "Running";
            handler.ShouldStartPodChaos(WatchEventType.Added, pod).Should().BeTrue();
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeTrue();
            handler.ShouldStartPodChaos(WatchEventType.Deleted, pod).Should().BeFalse();
            handler.ShouldStartPodChaos(WatchEventType.Error, pod).Should().BeFalse();

            pod.Metadata.Annotations["stress/chaos.started"] = "true";
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeFalse();

            pod.Metadata.Annotations.Remove("stress/chaos.started");
            pod.Metadata.Annotations["stress/chaos.autoStart"] = "false";
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeFalse();

            pod = CreatePod("testns", "");
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeFalse();

            pod = CreatePod("testns", "pod-test-instance");
            pod.Metadata.Labels.Remove("chaos");
            handler.ShouldStartPodChaos(WatchEventType.Modified, pod).Should().BeFalse();
        }
    }
}