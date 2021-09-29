{{ define "stress-test-addons.init-deploy" }}
- name: azure-deployer
  image: stresstestregistry.azurecr.io/testing/eng-common-tools
  command: ['pwsh', '-c', './common/TestResources/deploy-stress-test.ps1']
  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
    - name: BASE_NAME
      value: '{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}'

  volumeMounts:
    - name: "{{ .Release.Name }}-test-resources"
      mountPath: /mnt/azure
    - name: test-env-{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
      mountPath: /mnt/outputs
    - name: "static-secrets-{{ .Release.Name }}"
      mountPath: "/mnt/secrets/static"
      readOnly: true
{{ end }}
