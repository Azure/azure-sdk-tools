{{ define "stress-test-addons.debug-file-volumes" }}
- name: debug-file-share-{{ .Release.Name }}
  azureFile:
    secretName: debugstorageaccountconfig
    {{ $addonvalues := index . "Values" "stress-test-addons" -}}
    shareName: {{ get $addonvalues.debugFileShareName $addonvalues.env }}
    readOnly: false
{{ end }}
