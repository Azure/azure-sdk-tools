{{ define "stress-test-addons.deploy-volumes" }}
- name: test-resources-{{ .Release.Name }}
  configMap:
    name: "test-resources-{{ .Release.Name }}"
    items:
      - key: template
        path: test-resources.json
      - key: parameters
        path: parameters.json
- name: test-resources-outputs-{{ .Release.Name }}
  emptyDir: {}
- name: cluster-secrets-{{ .Release.Name }}
  csi:
    driver: secrets-store.csi.k8s.io
    readOnly: true
    volumeAttributes:
      secretProviderClass: stress-cluster-kv-{{ .Release.Name }}
- name: static-secrets-{{ .Release.Name }}
  csi:
    driver: secrets-store.csi.k8s.io
    readOnly: true
    volumeAttributes:
      secretProviderClass: stress-static-kv-{{ .Release.Name }}
{{ end }}
