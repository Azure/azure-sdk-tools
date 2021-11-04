{{ define "stress-test-addons.debug-file-volumes" }}
- name: debug-file-share-{{ .Release.Name }}
  azureFile:
    secretName: debugstorageaccountconfig
    shareName: {{ get .Values.debugFileShareName .Values.env }}
    readOnly: false
{{ end }}
