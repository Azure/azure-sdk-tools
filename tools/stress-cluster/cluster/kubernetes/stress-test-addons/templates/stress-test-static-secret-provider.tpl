{{- define "stress-test-addons.static-secrets" -}}
{{- /* The subscriptionConfig key allows a user to use a custom set of subscription+creds uploaded manually to the static keyvault */}}
{{- /* This template finds all unique subscription configs in scenarios and creates a single secret provider for each */}}
{{- $global := . }}
{{- $subChart := get $global.Values "stress-test-addons" }}
{{- $subConfigs := list (get $subChart.subscription $subChart.env) }}
{{- range $global.Values.scenarios }}
{{- $subConfigs = append $subConfigs (coalesce .subscriptionConfig "") }}
{{- end }}
{{- $subConfigs = compact $subConfigs | uniq }}
{{- range $subConfigs }}
---
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: stress-static-kv-{{ $global.Release.Name }}-{{ lower . }}
  namespace: {{ $global.Release.Namespace }}
spec:
  provider: azure
  secretObjects:
    - secretName: {{ . }}
      type: Opaque
      data:
        - objectName: {{ . }}
          key: value
  parameters:
    useVMManagedIdentity: "true"
    userAssignedIdentityID: {{ get $subChart.secretProviderIdentity $subChart.env }}  # az vmss identity show ...
    keyvaultName: {{ get $subChart.staticTestSecretsKeyvaultName $subChart.env }}
    objects:  |
      array:
        - |
          objectName: {{ . }}
          objectType: secret
    tenantId: {{ get $subChart.tenantId $subChart.env }}
{{- end }}
{{- end -}}
