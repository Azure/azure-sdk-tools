{{- define "azure-deployer.deploy-volumes" -}}
- name: test-resources
  configMap:
    name: "{{ .Release.Name }}-test-resources"
    items:
      - key: template
        path: test-resources.json
      - key: parameters
        path: parameters.json
- name: test-resources-outputs
  emptyDir: {}
{{- end -}}
