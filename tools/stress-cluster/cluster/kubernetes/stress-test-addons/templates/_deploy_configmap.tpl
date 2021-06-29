{{ define "stress-test-addons.deploy-configmap" }}
apiVersion: v1
kind: ConfigMap
metadata:
  name: "test-resources-{{ .Release.Name }}"
  namespace: {{ .Release.Namespace }}
data:
  template: |
    {{- .Files.Get "test-resources.json" | nindent 4 }}
  parameters: |
    {{- .Files.Get "parameters.json" | nindent 4 }}
{{ end }}
