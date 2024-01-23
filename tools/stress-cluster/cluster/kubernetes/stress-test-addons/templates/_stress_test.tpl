{{- define "stress-test-addons.job-wrapper.tpl" -}}
{{- $global := index . 0 -}}
{{- $definition := index . 1 -}}
spec:
  template:
    {{- include $definition $global | nindent 4 -}}
{{- end -}}

{{- define "stress-test-addons.deploy-job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ lower .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
    scenario: {{ .Stress.Scenario }}
    resourceGroupName: {{ .Stress.ResourceGroupName }}
    baseName: {{ .Stress.BaseName }}
    gitCommit: {{ .Values.GitCommit | default "" }}
spec:
  {{- if .Stress.parallel }}
  completions: {{ .Stress.parallel }}
  parallelism: {{ .Stress.parallel }}
  completionMode: Indexed
  {{- end }}
  backoffLimit: 0
  template:
    metadata:
      labels:
        release: {{ .Release.Name }}
        scenario: {{ .Stress.Scenario }}
        gitCommit: {{ .Values.GitCommit | default "" }}
      {{- if .Values.PodDisruptionBudgetExpiry }}
      annotations:
        deletionLockExpiry: {{ .Values.PodDisruptionBudgetExpiry }}
      {{- end }}
    spec:
      # In cases where a stress test has higher resource requirements or needs a dedicated node,
      # a new nodepool can be provisioned and labeled to allow custom scheduling.
      nodeSelector:
        sku: 'default'
      restartPolicy: Never
      volumes:
        # Volume template for mounting secrets
        {{- include "stress-test-addons.env-volumes" . | nindent 8 }}
        # Volume template for mounting ARM templates
        {{- include "stress-test-addons.deploy-volumes" . | nindent 8 }}
        # Volume template for mounting azure file share for debugging
        {{- include "stress-test-addons.debug-file-volumes" . | nindent 8 }}
      initContainers:
        # Init container template for injecting secrets
        # (e.g. app insights instrumentation key, azure client credentials)
        {{- include "stress-test-addons.init-env" . | nindent 8 }}
        # Init container template for deploying azure resources on startup and adding deployment outputs to the env
        {{- include "stress-test-addons.init-deploy" . | nindent 8 }}
{{- end -}}

{{- define "stress-test-addons.deploy-job-template.from-pod" -}}
{{- $global := index . 0 -}}
{{- $podDefinition := index . 1 -}}
# Configmap template that adds the stress test ARM template for mounting
{{- include "stress-test-addons.deploy-configmap" $global }}
{{- range (default (list "stress") $global.Values.scenarios) }}
---
{{ $jobCtx := fromYaml (include "stress-test-addons.util.mergeStressContext" (list $global . )) }}
{{- $jobOverride := fromYaml (include "stress-test-addons.job-wrapper.tpl" (list $jobCtx $podDefinition)) -}}
{{- $tpl := fromYaml (include "stress-test-addons.deploy-job-template.tpl" $jobCtx) -}}
{{- toYaml (merge $jobOverride $tpl) -}}
{{- end }}
{{- include "stress-test-addons.static-secrets" $global }}
{{- if $global.Values.PodDisruptionBudgetExpiry }}
{{- include "stress-test-addons.pod-disruption-budget" $global }}
{{- end }}
{{- end -}}

{{- define "stress-test-addons.env-job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
    scenario: {{ .Stress.Scenario }}
    resourceGroupName: {{ .Stress.ResourceGroupName }}
    baseName: {{ .Stress.BaseName }}
    gitCommit: {{ .Values.GitCommit | default "" }}
spec:
  {{- if .Stress.parallel }}
  completions: {{ .Stress.parallel }}
  parallelism: {{ .Stress.parallel }}
  completionMode: Indexed
  {{- end }}
  backoffLimit: 0
  template:
    metadata:
      labels:
        release: {{ .Release.Name }}
        scenario: {{ .Stress.Scenario }}
        gitCommit: {{ .Values.GitCommit | default "" }}
      {{- if .Values.PodDisruptionBudgetExpiry }}
      annotations:
        deletionLockExpiry: {{ .Values.PodDisruptionBudgetExpiry }}
      {{- end }}
    spec:
      nodeSelector:
        sku: 'default'
      restartPolicy: Never
      volumes:
        # Volume template for mounting secrets
        {{- include "stress-test-addons.env-volumes" . | nindent 8 }}
        {{- include "stress-test-addons.debug-file-volumes" . | nindent 8 }}
      initContainers:
        # Init container template for injecting secrets
        # (e.g. app insights instrumentation key, azure client credentials)
        {{- include "stress-test-addons.init-env" . | nindent 8 }}
{{- end -}}


{{- define "stress-test-addons.env-job-template.from-pod" -}}
{{- $global := index . 0 -}}
{{- $podDefinition := index . 1 -}}
{{- range (default (list "stress") $global.Values.scenarios) }}
---
{{- /* Copy scenario name into top level key of global context */}}
{{ $jobCtx := fromYaml (include "stress-test-addons.util.mergeStressContext" (list $global . )) }}
{{- $jobOverride := fromYaml (include "stress-test-addons.job-wrapper.tpl" (list $jobCtx $podDefinition)) -}}
{{- $tpl := fromYaml (include "stress-test-addons.env-job-template.tpl" $jobCtx) -}}
{{- toYaml (merge $jobOverride $tpl) -}}
{{- end }}
{{- include "stress-test-addons.static-secrets" $global }}
{{- if $global.Values.PodDisruptionBudgetExpiry }}
{{- include "stress-test-addons.pod-disruption-budget" $global }}
{{- end }}
{{- end -}}
