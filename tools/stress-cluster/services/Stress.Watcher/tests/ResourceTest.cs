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
        public void GetTestInstanceTest()
        {
            var chaosResource = new GenericChaosResource();
            var networkChaos = new ChaosResourceSpec();
            
            // Null selector
            chaosResource.Spec = new ChaosResourceSpec();
            chaosResource.Spec.NetworkChaos = networkChaos;
            chaosResource.Spec.GetTestInstance().Should().Be(null);

            // Null labelSelector
            networkChaos.Selector = new ChaosSelector();
            chaosResource.Spec.NetworkChaos = networkChaos;
            chaosResource.Spec.GetTestInstance().Should().Be(null);

            // Null testInstance
            networkChaos.Selector.LabelSelectors = new ChaosLabelSelectors() {
                TestInstance = null
            };
            chaosResource.Spec.NetworkChaos = networkChaos;
            chaosResource.Spec.GetTestInstance().Should().Be(null);

            // Empty string testInstance
            networkChaos.Selector.LabelSelectors.TestInstance = "";
            chaosResource.Spec.NetworkChaos = networkChaos;
            chaosResource.Spec?.GetTestInstance().Should().Be("");

            // Non-empty nested testInstance
            networkChaos.Selector.LabelSelectors.TestInstance = "ResourceTestTestInstance";
            networkChaos.GetTestInstance().Should().Be("ResourceTestTestInstance");
            chaosResource.Spec.NetworkChaos = networkChaos;
            chaosResource.Spec.GetTestInstance().Should().Be("ResourceTestTestInstance");

            // Non-empty non-nested testInstance
            chaosResource.Spec.NetworkChaos = null;
            chaosResource.Spec.Selector = new ChaosSelector() {
                LabelSelectors = new ChaosLabelSelectors() {
                    TestInstance = "ResourceTestTestInstance"
                }
            };
            chaosResource.Spec.GetTestInstance().Should().Be("ResourceTestTestInstance");
        }
    }
}