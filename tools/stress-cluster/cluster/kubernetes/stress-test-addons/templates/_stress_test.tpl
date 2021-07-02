{{- define "stress-test-addons.job-template.tpl" -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: "{{ .Release.Name }}-{{ .Release.Revision }}"
  namespace: {{ .Release.Namespace }}
spec:
  backoffLimit: 0
  template:
    metadata:
      labels:
        chaos: "true"
        owners: "{{ .Values.owners }}"
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
{{- define "stress-test-addons.job-template" -}}
# Configmap template that adds the stress test ARM template for mounting
{{- include "stress-test-addons.deploy-configmap" (first .) }}
---
{{- include "stress-test-addons.util.merge" (append . "stress-test-addons.job-template.tpl") -}}
{{- end -}}
