{{- /*
stress-test-addons.util.merge will merge two YAML templates and output the result.
This takes an array of three values:
- the top context
- the template name of the overrides (destination)
- the template name of the base (source)

See https://github.com/Masterminds/sprig/tree/master/docs for template function reference
*/}}
{{- define "stress-test-addons.util.merge" -}}
{{- $top := first . -}}
{{- $overrides := fromYaml (include (index . 1) $top) | default (dict ) -}}
{{- $tpl := fromYaml (include (index . 2) $top) | default (dict ) -}}
{{- toYaml (merge $overrides $tpl) -}}
{{- end -}}

{{- /*
stress-test-addons.util.mergeStressContext will copy/add/default stress related context
values into an object containing both the $global context and any local or range loop context values.

This takes an array of two values:
- the top global context
- A string (Scenario context item from a range loop)

Fields added to global context and returned:

.Stress.Scenario - A `.` value passed down from a range loop over a scenarios list
            from values.yaml or a default value "stress".
.Stress.ResourceGroupName - A pre-calculated resource group name value that can be passed down to various configurations that require it.
.Stress.BaseName - A random, six character, lowercase alpha string that can be used for naming and is valid for most azure resources.
.Stress.SubscriptionConfig - Secret name from which to load subscription target and credentials

See https://github.com/Masterminds/sprig/tree/master/docs for template function reference
*/}}
{{- define "stress-test-addons.util.mergeStressContext" -}}
{{- /* Copy scenario name into top level keys of global context */}}
{{- $_global := index . 0 -}}
{{- /* Load values.yaml of stress-test-addons sub chart to get subscription config defaults */}}
{{- $_subChart := get $_global.Values "stress-test-addons" }}
{{- $_scenario := index . 1 -}}
{{- $resourceGroupName := lower (print $_global.Release.Namespace "-" $_scenario.Scenario "-" $_global.Release.Name "-" $_global.Release.Revision) -}}
{{- /* Use lowercase alphanumeric characters beginning with a letter for maximum azure resource naming compatibility */ -}}
{{- $uniqueTestId := lower (print "s" (trunc 5 (sha1sum $resourceGroupName) ) ) -}}
{{- $subConfig := lower (coalesce $_scenario.subscriptionConfig (get $_subChart.subscription $_subChart.env)) }}
{{- /* Create add Stress context to top level keys of global context */}}
{{- $_stress := dict "ResourceGroupName" $resourceGroupName "BaseName" $uniqueTestId "SubscriptionConfig" $subConfig -}}
{{- $_stress := merge $_stress $_scenario -}}
{{- $_instance := deepCopy ($_global | merge (dict "Stress" $_stress )) -}}
{{ toYaml ($_instance) }}
{{- end -}}
