{{- define "azure-deployer.azure-env" -}}
- name: AZURE_TENANT_ID
  valueFrom:
    secretKeyRef:
      name: {{ .Values.subscription }}
      key: tenant
- name: AZURE_CLIENT_ID
  valueFrom:
    secretKeyRef:
      name: {{ .Values.subscription }}
      key: username
- name: AZURE_CLIENT_SECRET
  valueFrom:
    secretKeyRef:
      name: {{ .Values.subscription }}
      key: password
{{- end }}
