{{ define "stress-test-addons.env-volumes" }}
- name: test-env-{{ lower .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
  emptyDir: {}
- name: debug-file-share-config-{{ .Release.Name }}
  csi:
    driver: secrets-store.csi.k8s.io
    readOnly: true
    volumeAttributes:
      secretProviderClass: stress-file-share-kv-{{ .Release.Name }}
- name: cluster-secrets-{{ .Release.Name }}
  csi:
    driver: secrets-store.csi.k8s.io
    readOnly: true
    volumeAttributes:
      secretProviderClass: stress-cluster-kv-{{ .Release.Name }}
{{ end }}
