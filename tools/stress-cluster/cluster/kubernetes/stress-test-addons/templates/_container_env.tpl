{{- define "stress-test-addons.container-env" -}}
env:
  - name: ENV_FILE
    value: /mnt/outputs/.env
volumeMounts:
  - name: test-env-{{ .Release.Name }}
    mountPath: /mnt/outputs
{{- end -}}
