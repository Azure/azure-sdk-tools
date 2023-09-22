{{ define "stress-test-addons.init-env" }}
- name: init-test-env
  image: mcr.microsoft.com/oss/busybox/busybox:1.33.1
  # Merge all mounted keyvault secrets into env file.
  # Secret values are expected to be in format <key>=<value>
  command: ['sh', '-c']
  args:
    - "cat /mnt/secrets/static/* /mnt/secrets/cluster/* > $ENV_FILE"
  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
  volumeMounts:
    - name: test-env-{{ lower .Stress.Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
      mountPath: /mnt/outputs
    - name: static-secrets-{{ .Release.Name }}-{{ .Stress.SubscriptionConfig }}
      mountPath: "/mnt/secrets/static"
      readOnly: true
    - name: cluster-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/cluster"
      readOnly: true
    # Force secret initialization from the secret provider CSI
    - name: debug-file-share-config-{{ .Release.Name }}
      mountPath: "/mnt/secrets/fileshare"
{{ end }}
