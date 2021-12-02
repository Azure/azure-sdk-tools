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

        private V1Container CreateContainer(string name, List<V1EnvVar> env = null)
        {
            return new V1Container()
            {
                Name = name,
                Image = "busybox",
                Env = env
            };
        }

        [Fact]
        public void TestShouldDeleteResources()
        {
            var handler = new PodEventHandler(null, null, null);
            var pod = CreatePod("testns", "pod-test-instance");
            pod.Spec = new V1PodSpec();

            pod.Status.Phase = "Succeeded";
            handler.ShouldDeleteResources(pod).Should().BeFalse();

            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("non-matching-container-name")
            };
            handler.ShouldDeleteResources(pod).Should().BeFalse();

            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("non-matching-container-name"),
                CreateContainer("init-azure-deployer")
            };
            handler.ShouldDeleteResources(pod).Should().BeTrue();

            var env = new List<V1EnvVar> {
                new V1EnvVar()
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(pod).Should().BeTrue();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME","")
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(pod).Should().BeTrue();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME")
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(pod).Should().BeTrue();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME", "testrg")
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(pod).Should().BeTrue();

            pod.Status.Phase = "Pending";
            handler.ShouldDeleteResources(pod).Should().BeFalse();
            pod.Status.Phase = "Running";
            handler.ShouldDeleteResources(pod).Should().BeFalse();
            pod.Status.Phase = "Failed";
            handler.ShouldDeleteResources(pod).Should().BeTrue();
            pod.Status.Phase = "Unknown";
            handler.ShouldDeleteResources(pod).Should().BeFalse();
        }

        [Fact]
        public void TestGetResourceGroupName()
        {
            var handler = new PodEventHandler(null, null, null);
            var pod = CreatePod("testns", "pod-test-instance");
            pod.Spec = new V1PodSpec();

            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("non-matching-container-name"),
                CreateContainer("init-azure-deployer")
            };
            handler.GetResourceGroupName(pod).Should().BeEmpty();

            var env = new List<V1EnvVar> {
                new V1EnvVar()
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(pod).Should().BeEmpty();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME","")
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(pod).Should().BeEmpty();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME")
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(pod).Should().BeEmpty();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME", "testrg")
            };
            pod.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(pod).Should().Be("testrg");
        }
    }
}