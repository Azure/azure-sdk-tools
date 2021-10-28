{{ define "stress-test-addons.init-deploy" }}
- name: init-azure-deployer
  # Please use 'testing' for the image repo name when testing
  # e.g. azsdkengsys.azurecr.io/testing/eng-common-tools
  image: azsdkengsys.azurecr.io/engsys/eng-common-tools
  command: 
    - 'pwsh'
    - '-NonInteractive'
    - '-NoProfile'
    - '-c'
    - './common/TestResources/deploy-stress-test-resources.ps1'
    - '/mnt/testresources/parameters.json'

  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
    - name: BASE_NAME
      value: '{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}'

  volumeMounts:
    - name: "{{ .Release.Name }}-{{ .Release.Revision }}-test-resources"
      mountPath: /mnt/testresources
    - name: test-env-{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
      mountPath: /mnt/outputs
    - name: "static-secrets-{{ .Release.Name }}"
      mountPath: "/mnt/secrets/static"
      readOnly: true
{{ end }}
