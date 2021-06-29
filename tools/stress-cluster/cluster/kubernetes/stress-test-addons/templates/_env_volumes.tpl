{{ define "stress-test-addons.env-volumes" }}
- name: test-env-{{ .Release.Name }}
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
