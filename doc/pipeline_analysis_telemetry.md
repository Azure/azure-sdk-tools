# Pipeline Analysis Telemetry

The [telemetry dashboard](https://azuresdkartifacts.z5.web.core.windows.net/pipeline-evaluation/index.html) tracks [Pipeline Analysis Next Steps](https://github.com/Azure/azure-sdk-for-python/blob/main/.github/workflows/pipeline-analysis-next-steps.md) and [Pipeline Auto Fix](https://github.com/Azure/azure-sdk-for-python/blob/main/.github/workflows/pipeline-analysis-auto-fix.md).

It is populated by the [pipeline fix evaluation pipeline](https://github.com/Azure/azure-sdk-tools/blob/main/eng/pipelines/pipeline-fix-eval.yml), using [`azsdk eng evaluate`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/EngSys/PipelineFixEvaluatorTool.cs).