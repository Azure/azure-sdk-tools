{{ define "stress-test-addons.init-env" }}
- name: test-env-initializer
  image: k8s.gcr.io/e2e-test-images/busybox:1.29
  command: ['sh', '-c']
  args:
    # Merge all mounted keyvault secrets into env file
    - 'cat /mnt/secrets/static/* /mnt/secrets/cluster/* > $ENV_FILE'
  env:
    - name: ENV_FILE
      value: /mnt/outputs/.env
  volumeMounts:
    - name: test-env-{{ .Release.Name }}
      mountPath: /mnt/outputs
    - name: static-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/static"
      readOnly: true
    - name: cluster-secrets-{{ .Release.Name }}
      mountPath: "/mnt/secrets/cluster"
      readOnly: true
{{ end }}
