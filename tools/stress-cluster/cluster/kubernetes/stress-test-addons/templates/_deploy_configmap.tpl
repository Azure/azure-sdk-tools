{{ define "stress-test-addons.deploy-configmap" }}
apiVersion: v1
kind: ConfigMap
metadata:
  name: "{{ .Release.Name }}-{{ .Release.Revision }}-test-resources"
  namespace: {{ .Release.Namespace }}
data:
  template: |
    {{ $template := .Files.Get "stress-test-resources.json" }}
    {{ if eq (len $template) 0 }}
      {{ fail "File `stress-test-resources.json` was empty or not found for live resource deployment configmap. Perhaps the `stress-test-resources.bicep` file is missing or the `az bicep build` command failed when running the deployment script?" }}
    {{ end }}
    {{- $template | nindent 4 }}
{{ end }}
