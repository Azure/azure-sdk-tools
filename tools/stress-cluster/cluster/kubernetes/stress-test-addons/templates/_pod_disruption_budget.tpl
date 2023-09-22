{{ define "stress-test-addons.pod-disruption-budget" }}
---
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: {{ .Release.Name }}
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
spec:
  # Jobs do not implement `scale` otherwise we could set `minAvailable: 100%` or `maxUnavailable: 0` instead.
  # Work around this by setting `minAvailable` to a number that will never be reached to simulate 100%
  # so that the disruption budget will work in parallel pod scenarios (completionMode: indexed)
  minAvailable: 10000
  selector:
    matchLabels:
      release: {{ .Release.Name }}
---
kind: ServiceAccount
apiVersion: v1
metadata:
  name: pdb-read-{{ .Release.Name }}
  namespace: {{ .Release.Namespace }}
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: pdb-read-{{ .Release.Name }}
  namespace: {{ .Release.Namespace }}
rules:
  - apiGroups: ["*"]
    resources: ["poddisruptionbudgets"]
    verbs: ["get", "list", "delete"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: pdb-read-{{ .Release.Name }}
  namespace: {{ .Release.Namespace }}
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: pdb-read-{{ .Release.Name }}
subjects:
  - kind: ServiceAccount
    name: pdb-read-{{ .Release.Name }}
---
apiVersion: batch/v1
kind: CronJob
metadata:
  name: pdb-del-{{ substr 0 39 .Release.Name }}-{{ lower (randAlphaNum 3) }}
  namespace: {{ .Release.Namespace }}
spec:
  concurrencyPolicy: Forbid
  schedule: "{{ .Values.PodDisruptionBudgetExpiryCron }}"
  jobTemplate:
    spec:
      backoffLimit: 2
      activeDeadlineSeconds: 600
      template:
        spec:
          serviceAccountName: pdb-read-{{ .Release.Name }}
          restartPolicy: OnFailure
          containers:
            - name: kubectl
              image: mcr.microsoft.com/cbl-mariner/base/core:2.0
              command: ['bash', '-c']
              args:
                - |
                    set -ex
                    curl -LOk "https://dl.k8s.io/release/$(curl -Lsk https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
                    install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl
                    kubectl version --client
                    kubectl delete poddisruptionbudgets -l release={{ .Release.Name }}
{{ end }}
