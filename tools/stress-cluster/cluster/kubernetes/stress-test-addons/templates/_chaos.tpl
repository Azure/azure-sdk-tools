{{- define "stress-test-addons.chaos-wrapper.tpl" -}}
{{- $global := index . 0 -}}
{{- $chaosTemplate := index . 1 -}}
{{- range (default (list "stress") $global.Values.scenarios) }}
---
{{ $chaosCtx := fromYaml (include "stress-test-addons.util.mergeStressContext" (list $global . )) }}
metadata:
  name: "{{ lower $chaosCtx.Stress.Scenario }}-{{ $chaosCtx.Release.Name }}-{{ $chaosCtx.Release.Revision }}"
  namespace: {{ $chaosCtx.Release.Namespace }}
  annotations:
    'experiment.chaos-mesh.org/pause': 'true'
{{ include $chaosTemplate $chaosCtx }}
{{- end -}}
{{- end -}}
