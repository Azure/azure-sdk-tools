{{- define "stress-test-addons.job-wrapper.tpl" -}}
spec:
  template:
    {{- include (index . 1) (index . 0) | nindent 4 -}}
{{- end -}}

{{- define "stress-test-addons.deploy-job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
    scenario: {{ .Scenario }}
spec:
  backoffLimit: 0
  template:
    metadata:
      labels:
        release: {{ .Release.Name }}
        scenario: {{ .Scenario }}
    spec:
      restartPolicy: Never
      volumes:
        # Volume template for mounting secrets
        {{- include "stress-test-addons.env-volumes" . | nindent 8 }}
        # Volume template for mounting ARM templates
        {{- include "stress-test-addons.deploy-volumes" . | nindent 8 }}
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
{{- /* Copy scenario name into top level key of global context */}}
{{ $instance := deepCopy $global | merge (dict "Scenario" . ) -}}
{{- $jobOverride := fromYaml (include "stress-test-addons.job-wrapper.tpl" (list $instance $podDefinition)) -}}
{{- $tpl := fromYaml (include "stress-test-addons.deploy-job-template.tpl" $instance) -}}
{{- toYaml (merge $jobOverride $tpl) -}}
{{- end }}
{{- end -}}

{{- define "stress-test-addons.env-job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
    scenario: {{ .Scenario }}
spec:
  backoffLimit: 0
  template:
    metadata:
      labels:
        release: {{ .Release.Name }}
        scenario: {{ .Scenario }}
    spec:
      restartPolicy: Never
      volumes:
        # Volume template for mounting secrets
        {{- include "stress-test-addons.env-volumes" . | nindent 8 }}
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
{{ $instance := deepCopy $global | merge (dict "Scenario" . ) -}}
{{- $jobOverride := fromYaml (include "stress-test-addons.job-wrapper.tpl" (list $instance $podDefinition)) -}}
{{- $tpl := fromYaml (include "stress-test-addons.env-job-template.tpl" $instance) -}}
{{- toYaml (merge $jobOverride $tpl) -}}
{{- end }}
{{- end -}}
