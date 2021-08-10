{{- define "stress-test-addons.container-image" -}}
{{- $context := (first .).Values | dict -}}
{{- $env := dig "stress-test-addons" "env" $context -}}
{{- $registry := get $context.registry $env -}}
{{- $repository := "repo" -}}
image: {{ $env }}.azurecr.io/{{ $repository }}/{{ (index . 1) }}
{{- end -}}
