{{ define "stress-test-addons.deploy-volumes" }}
- name: {{ .Release.Name }}-{{ .Release.Revision }}-test-resources
  configMap:
    name: "{{ .Release.Name }}-{{ .Release.Revision }}-test-resources"
    items:
      - key: template
        path: test-resources.json
{{ end }}
