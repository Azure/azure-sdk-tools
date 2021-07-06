{{- define "stress-test-addons.job-wrapper.tpl" -}}
spec:
  template:
    {{- include (index . 1) (index . 0) | nindent 4 -}}
{{- end -}}

{{- define "stress-test-addons.deploy-job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
spec:
  backoffLimit: 0
  template:
    metadata:
      labels:
        release: {{ .Release.Name }}
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

{{- define "stress-test-addons.deploy-job-template" -}}
# Configmap template that adds the stress test ARM template for mounting
{{- include "stress-test-addons.deploy-configmap" (first .) }}
---
{{- include "stress-test-addons.util.merge" (append . "stress-test-addons.deploy-job-template.tpl") -}}
{{- end -}}

{{- define "stress-test-addons.deploy-job-template.from-pod" -}}
# Configmap template that adds the stress test ARM template for mounting
{{- include "stress-test-addons.deploy-configmap" (first .) }}
---
{{- $jobOverride := fromYaml (include "stress-test-addons.job-wrapper.tpl" .) -}}
{{- $tpl := fromYaml (include "stress-test-addons.deploy-job-template.tpl" (first .)) -}}
{{- toYaml (merge $jobOverride $tpl) -}}
{{- end -}}

{{- define "stress-test-addons.env-job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
  labels:
    release: {{ .Release.Name }}
spec:
  backoffLimit: 0
  template:
    metadata:
      labels:
        release: {{ .Release.Name }}
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

{{- define "stress-test-addons.env-job-template" -}}
{{- include "stress-test-addons.util.merge" (append . "stress-test-addons.env-job-template.tpl") -}}
{{- end -}}

{{- define "stress-test-addons.env-job-template.from-pod" -}}
{{- $jobOverride := fromYaml (include "stress-test-addons.job-wrapper.tpl" .) -}}
{{- $tpl := fromYaml (include "stress-test-addons.env-job-template.tpl" (first .)) -}}
{{- toYaml (merge $jobOverride $tpl) -}}
{{- end -}}
