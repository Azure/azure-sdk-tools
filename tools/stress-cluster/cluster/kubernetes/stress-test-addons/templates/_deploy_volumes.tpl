{{ define "stress-test-addons.deploy-volumes" }}
- name: test-resources-{{ .Release.Name }}
  configMap:
    name: "test-resources-{{ .Release.Name }}"
    items:
      - key: template
        path: test-resources.json
      - key: parameters
        path: parameters.json
{{ end }}
