{{ define "stress-test-addons.init-env" }}
- name: init-test-env
  image: k8s.gcr.io/e2e-test-images/busybox:1.29
  command: ['sh', '-c']
  args:
    # Merge all mounted keyvault secrets into env file.
    # Secret values are expected to be in format <key>=<value>
    - 'cat /mnt/secrets/static/* /mnt/secrets/cluster/* > $ENV_FILE'
  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
  volumeMounts:
    - name: test-env-{{ lower .Scenario }}-{{ .Release.Name }}-{{ .Release.Revision }}
      mountPath: /mnt/outputs
    - name: static-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/static"
      readOnly: true
    - name: cluster-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/cluster"
      readOnly: true
{{ end }}
