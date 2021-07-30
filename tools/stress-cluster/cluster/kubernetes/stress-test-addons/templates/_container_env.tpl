{{- define "stress-test-addons.container-env" -}}
env:
  - name: ENV_FILE
    value: /mnt/outputs/.env
volumeMounts:
  - name: test-env-{{ default "" (printf "%s-" (lower .Scenario)) }}{{ .Release.Name }}-{{ .Release.Revision }}
    mountPath: /mnt/outputs
{{- end -}}
