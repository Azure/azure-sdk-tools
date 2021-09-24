{{ define "stress-test-addons.deploy-volumes" }}
- name: {{ .Release.Name }}-test-resources
  configMap:
    name: "{{ .Release.Name }}-test-resources"
    items:
      - key: template
        path: test-resources.json
      - key: parameters
        path: parameters.json
{{ end }}
