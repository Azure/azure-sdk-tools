{{- define "stress-test-addons.container-env" -}}
env:
  - name: ENV_FILE
    value: /mnt/outputs/.env
volumeMounts:
  - name: test-env-{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
    mountPath: /mnt/outputs
{{- end -}}
