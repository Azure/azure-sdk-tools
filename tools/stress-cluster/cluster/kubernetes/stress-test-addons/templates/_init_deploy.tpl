{{ define "stress-test-addons.init-deploy" }}
{{- $addons := get .Values "stress-test-addons" -}}
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
    - name: AZURE_SUBSCRIPTION_ID
      value: {{ get $addons.subscriptionId $addons.env }}
    - name: STRESS_CLUSTER_RESOURCE_GROUP
      value: {{ get $addons.clusterGroup $addons.env }}
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
{{ end }}
