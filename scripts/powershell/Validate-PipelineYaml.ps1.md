---
external help file: -help.xml
Module Name:
online version:
schema: 2.0.0
---

# Validate-PipelineYaml.ps1

## SYNOPSIS
Validates a Azure Pipelines yaml file via the Azure Pipelines API.

## SYNTAX

```
Validate-PipelineYaml.ps1 [-Path] <String> [-PipelineId] <String> [[-Branch] <String>] [[-Project] <String>]
 [[-OutputPath] <String>] [<CommonParameters>]
```

## DESCRIPTION
This script submits pipelines yaml to the server-side API for advanced validation which includes parameter expansion.
It writes the generated yaml to a file.
For basic pipelines yaml schema validation, see the VSCode extension: https://github.com/Microsoft/azure-pipelines-vscode

## EXAMPLES

### EXAMPLE 1
```
Test changes to a pipeline file against master:
```

$env:PATVAR = "\<personal access token\>"
./scripts/powershell/Validate-PipelineYaml.ps1 \`
    -Path ./eng/pipelines/pipeline-generation.yml \`
    -Pipeline 773

### EXAMPLE 2
```
Test changes to a pipeline file against a branch with extra template changes:
```

$env:PATVAR = "\<personal access token\>"
./scripts/powershell/Validate-PipelineYaml.ps1 \`
    -Path ./eng/pipelines/pipeline-generation.yml \`
    -Pipeline 773 \`
    -Branch \<your branch\>

## PARAMETERS

### -Path
Path to the pipelines yaml file

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PipelineId
The Pipeline ID (number) that the pipeline yaml is defined for.
You can retrieve the numeric ID by navigating
to the pipeline in the UI, and grabbing it from the url: https://dev.azure.com/azure-sdk/internal/_build?definitionId=\<PIPELINE_ID\>&_a=summary

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 2
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Branch
The Pipelines API supports validating any additional yaml templates included by the pipeline file.
In order to do so, these files must be checked in to the repository.
In this case -Branch must be specified to reference those changes.
Referencing external repositories for validation is not supported by the Pipelines API.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 3
Default value: Master
Accept pipeline input: False
Accept wildcard characters: False
```

### -Project
Optional Azure DevOps project the pipeline is defined under.
Defaults to 'internal'.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 4
Default value: Internal
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Optional output path for the generated pipeline yaml.
Defaults to 'output.yml'.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 5
Default value: Output.yml
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

## RELATED LINKS
