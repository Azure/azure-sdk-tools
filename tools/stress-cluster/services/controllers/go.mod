module github.com/azure/azure-sdk-tools/tools/stress-cluster/services/controllers

go 1.16

require (
	github.com/chaos-mesh/chaos-mesh/api/v1alpha1 v0.0.0-20210826134604-1f23fe6e5d8a
	github.com/go-logr/logr v0.4.0
	github.com/onsi/ginkgo v1.16.4
	github.com/onsi/gomega v1.14.0
	k8s.io/api v0.21.3
	k8s.io/apimachinery v0.21.3
	k8s.io/client-go v0.21.3
	sigs.k8s.io/cluster-api v0.4.2
	sigs.k8s.io/controller-runtime v0.9.6
)
