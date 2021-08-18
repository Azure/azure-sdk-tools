{{ define "stress-test-addons.init-deploy" }}
- name: azure-deployer
  image: mcr.microsoft.com/azure-cli
  command: ['bash', '-c']
  args:
    - |
      source /mnt/secrets/static/* &&
      az login --service-principal -u $AZURE_CLIENT_ID -p $AZURE_CLIENT_SECRET --tenant $AZURE_TENANT_ID &&
      az account set -s $AZURE_SUBSCRIPTION_ID &&
      groupName='{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}'
      az group create -l westus2 -g $groupName &&
      group=$(az group show -g $groupName -o tsv --query "id") &&
      az tag create --resource-id $group --tags DeleteAfter="$(date -d '+192:00:00' -Iseconds -u | sed 's/UTC/Z/')" &&
      az deployment group create \
          -g $groupName \
          -n $groupName \
          -f /mnt/testresources/test-resources.json \
          --parameters /mnt/testresources/parameters.json \
          --parameters testApplicationOid=$AZURE_CLIENT_OID > /dev/null &&
      az deployment group show \
          -g $groupName \
          -n $groupName \
          -o json \
          --query properties.outputs \
          | jq -r 'keys[] as $k | "\($k | ascii_upcase)=\(.[$k].value)"' >> /mnt/outputs/.env
  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
  volumeMounts:
    - name: "{{ .Release.Name }}-test-resources"
      mountPath: /mnt/testresources
    - name: test-env-{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
      mountPath: /mnt/outputs
    - name: "static-secrets-{{ .Release.Name }}"
      mountPath: "/mnt/secrets/static"
      readOnly: true
{{ end }}
