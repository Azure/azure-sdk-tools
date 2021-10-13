{{- define "stress-test-addons.container-env" -}}
env:
  - name: ENV_FILE
    value: /mnt/outputs/.env
  - name: DEBUG_SHARE
    value: /mnt/share/
volumeMounts:
  - name: test-env-{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
    mountPath: /mnt/outputs
  - name: debug-file-share-{{ .Release.Name }}
    mountPath: /mnt/share
{{- end -}}
