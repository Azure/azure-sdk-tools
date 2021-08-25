/*
Copyright 2021 microsoft.com.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

package controllers

import (
	"context"
	"time"

	. "github.com/onsi/ginkgo"
	. "github.com/onsi/gomega"
	corev1 "k8s.io/api/core/v1"
	metav1 "k8s.io/apimachinery/pkg/apis/meta/v1"
	"k8s.io/apimachinery/pkg/types"

	chaosMesh "github.com/chaos-mesh/chaos-mesh/api/v1alpha1"
)

// These tests use Ginkgo (BDD-style Go testing framework). Refer to
// http://onsi.github.io/ginkgo/ to learn more about Ginkgo.

var _ = Describe("Pod Controller", func() {
	ctx := context.Background()

	It("Should resume chaos for matching pods", func() {
		testInstance := "pod-reconciler-network-chaos-test"
		labels := map[string]string{
			"chaos":         "true",
			"test-instance": testInstance,
		}
		networkChaos := chaosMesh.NetworkChaos{
			ObjectMeta: metav1.ObjectMeta{
				Name: "test-network-chaos",
				Annotations: map[string]string{
					chaosMesh.PauseAnnotationKey: "true",
				},
			},
			Spec: chaosMesh.NetworkChaosSpec{
				PodSelector: chaosMesh.PodSelector{
					Selector: chaosMesh.PodSelectorSpec{
						LabelSelectors: labels,
					},
				},
			},
		}
		chaosPod := corev1.Pod{
			ObjectMeta: metav1.ObjectMeta{
				Name:   "test-network-chaos-pod",
				Labels: labels,
			},
		}

		Expect(k8sClient.Create(ctx, networkChaos.DeepCopy())).To(Succeed())
		Expect(k8sClient.Create(ctx, chaosPod.DeepCopy())).To(Succeed())

		podReconciler.log.Info("SLEEPING")
		time.Sleep(5 * time.Second)
		podReconciler.log.Info("SLEEPING")

		result := chaosMesh.NetworkChaos{}
		Expect(k8sClient.Get(ctx, types.NamespacedName{Name: networkChaos.Name}, &result)).To(Succeed())

		annotations := result.GetAnnotations()
		if _, ok := annotations[chaosMesh.PauseAnnotationKey]; ok {
			Fail("Expected chaos pause annotation to have been removed.")
		}
	})
})
