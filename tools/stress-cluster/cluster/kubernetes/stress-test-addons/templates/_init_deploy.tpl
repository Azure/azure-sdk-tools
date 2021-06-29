{{ define "stress-test-addons.init-deploy" }}
- name: azure-deployer
  image: mcr.microsoft.com/azure-cli
  command: ['bash', '-c']
  args:
    - |
      # Merge all mounted keyvault secrets into env file
      cat /mnt/secrets/static/* > /mnt/outputs/.env &&
      cat /mnt/secrets/cluster/* >> /mnt/outputs/.env &&
      cat /mnt/outputs/.env &&
      source /mnt/outputs/.env &&
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
  volumeMounts:
    - name: test-resources-{{ .Release.Name }}
      mountPath: /mnt/testresources
    - name: test-resources-outputs-{{ .Release.Name }}
      mountPath: /mnt/outputs
    - name: cluster-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/cluster"
      readOnly: true
    - name: static-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/static"
      readOnly: true
{{ end }}
