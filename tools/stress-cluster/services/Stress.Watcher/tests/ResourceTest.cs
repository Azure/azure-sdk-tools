using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Stress.Watcher.Extensions;
using k8s;
using k8s.Models;

namespace Stress.Watcher.Tests
{
    public class ResourceTest
    {
        [Fact]
        public void GetInstanceTest()
        {
            var schedule = new GenericChaosResource();
            var networkChaos = new ChaosResourceSpec();
            schedule.Spec = new ChaosResourceSpec();
            networkChaos.Selector = new ChaosSelector() {
                LabelSelectors = new ChaosLabelSelectors() {
                    TestInstance = "ResourceTestTestInstance"
                }
            };
            schedule.Spec.NetworkChaos = networkChaos;
            networkChaos.GetTestInstance().Should().Be("ResourceTestTestInstance");
            schedule.Spec?.GetTestInstance().Should().Be("ResourceTestTestInstance");
        }
    }
}