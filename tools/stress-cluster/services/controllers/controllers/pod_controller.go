/*
Copyright 2021 Microsoft.com.

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
	"fmt"

	"github.com/go-logr/logr"
	corev1 "k8s.io/api/core/v1"
	"k8s.io/apimachinery/pkg/runtime"
	"sigs.k8s.io/cluster-api/util/patch"
	ctrl "sigs.k8s.io/controller-runtime"
	"sigs.k8s.io/controller-runtime/pkg/client"
	"sigs.k8s.io/controller-runtime/pkg/log"

	chaosMesh "github.com/chaos-mesh/chaos-mesh/api/v1alpha1"
)

// PodReconciler reconciles a Pod object
type PodReconciler struct {
	client.Client
	Scheme *runtime.Scheme
	log    logr.Logger
}

const chaosStartedAnnotation = "stress/chaos.started"
const chaosLabelSelector = "testInstance"

//+kubebuilder:rbac:groups=core,resources=pods,verbs=get;list;watch;create;update;patch;delete
//+kubebuilder:rbac:groups=core,resources=pods/status,verbs=get;update;patch
//+kubebuilder:rbac:groups=core,resources=pods/finalizers,verbs=update

// Reconcile is part of the main kubernetes reconciliation loop which aims to
// move the current state of the cluster closer to the desired state.
func (r *PodReconciler) Reconcile(ctx context.Context, req ctrl.Request) (ctrl.Result, error) {
	r.log = log.FromContext(ctx).WithName("stress_watcher")

	pod := corev1.Pod{}
	if err := r.Client.Get(ctx, req.NamespacedName, &pod); err != nil {
		r.log.Error(err, "Failed to get pod resource.")
		return ctrl.Result{}, nil
	}

	if pod.Namespace == "kube-system" || pod.Namespace == "stress-infra" {
		return ctrl.Result{}, nil
	}

	if pod.Status.Phase != corev1.PodRunning {
		r.log.Info(
			fmt.Sprintf("Pod is in '%s', not '%s' phase, ignoring.",
				pod.Status.Phase, corev1.PodRunning))
		return ctrl.Result{}, nil
	}

	requeue, err := r.handleChaos(ctx, &pod)
	if err != nil {
		r.log.Error(err, "Error handling chaos for pod.")
		if requeue {
			return ctrl.Result{Requeue: true}, nil
		}
		return ctrl.Result{}, nil
	}

	return ctrl.Result{}, nil
}

func (r *PodReconciler) handleChaos(ctx context.Context, pod *corev1.Pod) (requeue bool, err error) {
	chaosResources, err := r.listAllChaosResources(ctx, pod.Namespace)
	if err != nil {
		return true, err
	}

	labels := pod.GetLabels()
	annotations := pod.GetAnnotations()
	if annotations == nil {
		annotations = map[string]string{}
	}

	if chaosStarted, ok := annotations[chaosStartedAnnotation]; ok && chaosStarted == "true" {
		r.log.Info("Pod has already been enabled for chaos.")
		return false, nil
	} else if chaos, ok := labels["chaos"]; ok && chaos == "true" {
		r.log.Info("Enabling chaos for pod.")
		testInstance, ok := labels[chaosLabelSelector]
		if !ok {
			return false, fmt.Errorf("Chaos enabled pod is missing %s label", chaosLabelSelector)
		}
		for _, chaosResource := range chaosResources {
			if chaosInstance, ok := chaosResource.Labels[chaosLabelSelector]; ok {
				if chaosInstance != testInstance {
					continue
				}
				r.log.Info("Found matching pod for chaos.", "TestInstance", chaosInstance)
				if err := r.resumeChaosResource(ctx, chaosResource.Resource); err != nil {
					return true, err
				}
				if err := r.annotatePodChaosHandled(ctx, pod, annotations); err != nil {
					return true, err
				}
			}
		}
	} else {
		r.log.Info("Ignoring pod for chaos.")
		return false, nil
	}

	r.log.Info("Failed to find matching chaos resources for pod.")
	return false, nil
}

func (r *PodReconciler) annotatePodChaosHandled(
	ctx context.Context,
	pod *corev1.Pod,
	annotations map[string]string,
) error {
	annotations[chaosStartedAnnotation] = "true"
	patcher, err := patch.NewHelper(pod.DeepCopy(), r.Client)
	if err != nil {
		return err
	}
	pod.SetAnnotations(annotations)
	if err := patcher.Patch(ctx, pod.DeepCopy()); err != nil {
		return err
	}

	return nil
}

func (r *PodReconciler) resumeChaosResource(
	ctx context.Context,
	chaosResource client.Object,
) error {
	log := r.log.WithValues("ChaosResource", chaosResource.GetName())
	annotations := chaosResource.GetAnnotations()
	if annotations == nil {
		log.Info("Chaos resource has no annotations and is likely running.")
		return nil
	}
	log.Info("Starting chaos.")

	patcher, err := patch.NewHelper(chaosResource.DeepCopyObject().(client.Object), r.Client)
	if err != nil {
		return err
	}
	delete(annotations, chaosMesh.PauseAnnotationKey)
	return patcher.Patch(ctx, chaosResource)
}

func (r *PodReconciler) listAllChaosResources(ctx context.Context, namespace string) ([]chaosResource, error) {
	networkChaosList := chaosMesh.NetworkChaosList{}
	stressChaosList := chaosMesh.StressChaosList{}
	httpChaosList := chaosMesh.HTTPChaosList{}
	ioChaosList := chaosMesh.IOChaosList{}
	kernelChaosList := chaosMesh.KernelChaosList{}
	timeChaosList := chaosMesh.TimeChaosList{}
	jvmChaosList := chaosMesh.JVMChaosList{}

	listOpt := client.ListOptions{Namespace: namespace}
	chaosItems := []chaosResource{}

	if err := r.Client.List(ctx, &networkChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get network chaos resources.")
	}
	for _, c := range networkChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}
	if err := r.Client.List(ctx, &stressChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get stress chaos resources.")
	}
	for _, c := range stressChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}
	if err := r.Client.List(ctx, &httpChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get http chaos resources.")
	}
	for _, c := range httpChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}
	if err := r.Client.List(ctx, &ioChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get io chaos resources.")
	}
	for _, c := range ioChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}
	if err := r.Client.List(ctx, &kernelChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get kernel chaos resources.")
	}
	for _, c := range kernelChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}
	if err := r.Client.List(ctx, &timeChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get time chaos resources.")
	}
	for _, c := range timeChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}

	if err := r.Client.List(ctx, &jvmChaosList, &listOpt); err != nil {
		r.log.Error(err, "Failed to get jvm chaos resources.")
	}
	for _, c := range jvmChaosList.Items {
		chaosItems = append(chaosItems, chaosResource{Resource: c.DeepCopy(), Labels: c.Spec.Selector.LabelSelectors})
	}

	return chaosItems, nil
}

type chaosResource struct {
	Resource client.Object
	Labels   map[string]string
}

// SetupWithManager sets up the controller with the Manager.
func (r *PodReconciler) SetupWithManager(mgr ctrl.Manager) error {
	return ctrl.NewControllerManagedBy(mgr).
		For(&corev1.Pod{}).
		Complete(r)
}
