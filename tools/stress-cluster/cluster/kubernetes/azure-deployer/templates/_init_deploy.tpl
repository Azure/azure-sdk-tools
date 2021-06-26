{{- define "azure-deployer.deploy-init" -}}
- name: azure-deployer
  image: mcr.microsoft.com/azure-cli
  command: ['bash', '-c']
  args:
    - |
      az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET --tenant $AZURE_TENANT_ID &&
      az account set -s "{{ .Values.subscription }}" &&
      az deployment sub create \
          -n {{ .Release.Name }} \
          -l westus2 \
          -f /testresources/test-resources.json \
          --parameters /testresources/parameters.json &&
      az deployment sub show \
          -n {{ .Release.Name }} \
          -o json \
          --query properties.outputs \
          | jq -r 'keys[] as $k | "\($k | ascii_upcase)=\(.[$k].value)"' > /outputs/.env
  volumeMounts:
    - name: test-resources
      mountPath: /testresources
    - name: test-resources-outputs
      mountPath: /outputs
  env:
    {{- include "azure-deployer.azure-env" . | nindent 4 }}
{{- end -}}
