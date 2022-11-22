using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using k8s;
using k8s.Models;

namespace Stress.Watcher.Tests
{
    public class JobEventHandlerTest
    {
        private V1Job CreateJob(string ns)
        {

            return new V1Job
            {
                Status = new V1JobStatus {},
                Metadata = new V1ObjectMeta
                {
                    Name = "testjob",
                    NamespaceProperty = ns,
                    Labels = new Dictionary<string, string> {},
                    Annotations = new Dictionary<string, string>()
                },
                Spec = new V1JobSpec
                {
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            NamespaceProperty = ns,
                            Name = "testpod",
                            Labels = new Dictionary<string, string> {},
                            Annotations = new Dictionary<string, string>()
                        },
                        Spec = new V1PodSpec {}
                    }
                }
            };
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
        public void TestShouldDeleteResourcesWithInitContainer()
        {
            var handler = new JobEventHandler(null, null, null);
            var job = CreateJob("testns");

            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();

            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition("True", "Complete") };

            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("non-matching-container-name")
            };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            handler.ShouldDeleteResources(job, WatchEventType.Deleted).Should().BeFalse();

            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("non-matching-container-name"),
                CreateContainer("init-azure-deployer")
            };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeTrue();
            handler.ShouldDeleteResources(job, WatchEventType.Deleted).Should().BeTrue();

            var env = new List<V1EnvVar> {
                new V1EnvVar()
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeTrue();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME","")
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeTrue();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME")
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeTrue();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME", "testrg")
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeTrue();
        }


        [Fact]
        public void TestShouldDeleteResourcesWithCondition()
        {
            var handler = new JobEventHandler(null, null, null);
            var job = CreateJob("testns");

            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            var env = new List<V1EnvVar> { new V1EnvVar("RESOURCE_GROUP_NAME", "testrg") };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>() { CreateContainer("init-azure-deployer", env) };

            job.Status.Conditions = new List<V1JobCondition> { };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            handler.ShouldDeleteResources(job, WatchEventType.Deleted).Should().BeTrue();
            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition(null, null) };
            handler.ShouldDeleteResources(job, WatchEventType.Deleted).Should().BeTrue();
            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition("False", "Failed") };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition("Unknown", "") };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition("False", "Complete") };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition("True", "Complete") };
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeTrue();
        }

        [Fact]
        public void TestShouldDeleteResourcesWithLabel()
        {
            var handler = new JobEventHandler(null, null, null);
            var job = CreateJob("testns");

            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();

            job.Status.Conditions = new List<V1JobCondition> { new V1JobCondition("True", "Failed") };
            job.Metadata.Labels.Add("Skip.RemoveTestResources", "true");
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
            job.Metadata.Labels.Remove("Skip.RemoveTestResources");
            job.Spec.Template.Metadata.Labels.Add("Skip.RemoveTestResources", "true");
            handler.ShouldDeleteResources(job, WatchEventType.Modified).Should().BeFalse();
        }


        [Fact]
        public void TestGetResourceGroupName()
        {
            var handler = new JobEventHandler(null, null, null);
            var job = CreateJob("testns");

            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("non-matching-container-name"),
                CreateContainer("init-azure-deployer")
            };
            handler.GetResourceGroupName(job).Should().BeEmpty();

            var env = new List<V1EnvVar> {
                new V1EnvVar()
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(job).Should().BeEmpty();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME","")
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(job).Should().BeEmpty();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME")
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(job).Should().BeEmpty();

            env = new List<V1EnvVar> {
                new V1EnvVar("RESOURCE_GROUP_NAME", "testrg")
            };
            job.Spec.Template.Spec.InitContainers = new List<V1Container>()
            {
                CreateContainer("other-init-container"),
                CreateContainer("init-azure-deployer", env)
            };
            handler.GetResourceGroupName(job).Should().Be("testrg");
        }
    }
}