{{- define "stress-test-addons.container-env" -}}
env:
  - name: ENV_FILE
    value: /mnt/outputs/.env
  - name: POD_NAME
    valueFrom:
      fieldRef:
        fieldPath: metadata.name
  - name: POD_NAMESPACE
    valueFrom:
      fieldRef:
        fieldPath: metadata.namespace
  - name: DEBUG_SHARE
    value: /mnt/share/$(POD_NAMESPACE)/$(POD_NAME)/
  - name: DEBUG_SHARE_ROOT
    value: /mnt/share/
  - name: SCENARIO_NAME
    value: {{ .Stress.Scenario }}
volumeMounts:
  - name: test-env-{{ lower .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
    mountPath: /mnt/outputs
  - name: debug-file-share-{{ .Release.Name }}
    mountPath: /mnt/share
{{- end -}}
