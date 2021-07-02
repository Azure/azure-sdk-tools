{{ define "stress-test-addons.init-deploy" }}
- name: azure-deployer
  image: mcr.microsoft.com/azure-cli
  command: ['bash', '-c']
  args:
    - |
      source /mnt/secrets/static/* &&
      az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET --tenant $AZURE_TENANT_ID &&
      az account set -s $AZURE_SUBSCRIPTION_ID &&
      az deployment sub create \
          -n {{ .Release.Name }} \
          -l westus2 \
          -f /mnt/testresources/test-resources.json \
          --parameters /mnt/testresources/parameters.json &&
      az deployment sub show \
          -n {{ .Release.Name }} \
          -o json \
          --query properties.outputs \
          | jq -r 'keys[] as $k | "\($k | ascii_upcase)=\(.[$k].value)"' >> /mnt/outputs/.env
  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
  volumeMounts:
    - name: "{{ .Release.Name }}-test-resources"
      mountPath: /mnt/testresources
    - name: "test-env-{{ .Release.Name }}"
      mountPath: /mnt/outputs
    - name: "static-secrets-{{ .Release.Name }}"
      mountPath: "/mnt/secrets/static"
      readOnly: true
{{ end }}
