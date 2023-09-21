{{ define "stress-test-addons.init-deploy" }}
- name: init-azure-deployer
  # Please use 'testing' for the image repo name when testing
  # e.g. azsdkengsys.azurecr.io/testing/deploy-test-resources
  image: azsdkengsys.azurecr.io/stress/deploy-test-resources
  imagePullPolicy: Always
  command:
    - 'pwsh'
    - '-NonInteractive'
    - '-NoProfile'
    - '-c'
    - '/scripts/stress-test/deploy-stress-test-resources.ps1'

  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
    - name: RESOURCE_GROUP_NAME
      value: {{ .Stress.ResourceGroupName }}
    - name: BASE_NAME
      value: {{ .Stress.BaseName }}
    - name: NAMESPACE
      value: {{ .Release.Namespace }}
    - name: JOB_NAME
      value:  "{{ lower .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}"
  volumeMounts:
    - name: "{{ .Release.Name }}-{{ .Release.Revision }}-test-resources"
      mountPath: /mnt/testresources
    - name: test-env-{{ lower .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
      mountPath: /mnt/outputs
    - name: "static-secrets-{{ .Release.Name }}-{{ .Stress.SubscriptionConfig }}"
      mountPath: "/mnt/secrets/static"
      readOnly: true
{{ end }}
