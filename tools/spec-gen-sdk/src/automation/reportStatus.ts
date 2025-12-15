import { MessageRecord } from '../types/Message';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { setSdkAutoStatus } from '../utils/runScript';
import { extractPathFromSpecConfig, mapToObject } from '../utils/utils';
import { vsoAddAttachment, vsoLogError, vsoLogWarning } from './logging';
import { ExecutionReport, PackageReport } from '../types/ExecutionReport';
import { deleteTmpJsonFile, writeTmpJsonFile } from '../utils/fsUtils';
import { marked } from "marked";
import { toolError, toolWarning } from '../utils/messageUtils';
import { FailureType, WorkflowContext } from '../types/Workflow';
import { setFailureType } from '../utils/workflowUtils';
import { commentDetailView, renderHandlebarTemplate } from '../utils/reportStatusUtils';

export const generateReport = (context: WorkflowContext) => {
  context.logger.log('section', 'Generate report');
  let executionReport: ExecutionReport;
  const packageReports: PackageReport[] = [];
  const specConfigPath = (context.specConfigPath)?.replace(/\//g, '-');

  let hasSuppressions = false
  let hasAbsentSuppressions = false;
  let areBreakingChangeSuppressed = false;
  let shouldLabelBreakingChange = false;
  let markdownContent = '';
  let message = "";
  let isTypeSpec = false;
  let hasPkgFromTypeSpec = false;
  let generateFromTypeSpec = false;

  if (!context.config.sdkName.includes('net') && context.specConfigPath && context.specConfigPath.endsWith('tspconfig.yaml')) {
    generateFromTypeSpec = true;
  }
  for (const pkg of context.handledPackages) {
    setSdkAutoStatus(context, pkg.status);
    hasSuppressions = Boolean(pkg.presentSuppressionLines.length > 0);
    hasAbsentSuppressions = Boolean(pkg.absentSuppressionLines.length > 0);
    if(pkg.hasBreakingChange && hasSuppressions && !hasAbsentSuppressions) {
      areBreakingChangeSuppressed = true;
    }
    if(pkg.hasBreakingChange && !pkg.isBetaMgmtSdk && !pkg.isDataPlane && !areBreakingChangeSuppressed) {
      shouldLabelBreakingChange = true;
    }
    const packageReport: PackageReport = {
      serviceName: pkg.serviceName,
      packageName: pkg.name,
      result: pkg.status,
      artifactPaths: pkg.artifactPaths,
      readmeMd: pkg.readmeMd,
      typespecProject: pkg.typespecProject,
      version: pkg.version,
      apiViewArtifact: pkg.apiViewArtifactPath,
      language: pkg.language,
      hasBreakingChange: pkg.hasBreakingChange,
      breakingChangeLabel: context.swaggerToSdkConfig.packageOptions.breakingChangesLabel,
      shouldLabelBreakingChange,
      areBreakingChangeSuppressed,
      presentBreakingChangeSuppressions: pkg.presentSuppressionLines,
      absentBreakingChangeSuppressions: pkg.absentSuppressionLines,
      installInstructions: pkg.installationInstructions
    }
    packageReports.push(packageReport);
    markdownContent += `## Package Name\n${pkg.name}\n`;
    markdownContent += `## Version\n${pkg.version}\n`;
    markdownContent += `## Result\n${pkg.status}\n`;
    markdownContent += `## Spec Configuration\n${pkg.typespecProject ?? pkg.readmeMd}\n`;
    markdownContent += `## Has Breaking Change\n${pkg.hasBreakingChange}\n`;
    markdownContent += `## Is Beta Management SDK\n${pkg.isBetaMgmtSdk}\n`;
    markdownContent += `## Has Suppressions\n${hasSuppressions}\n`;
    markdownContent += `## Has Absent Suppressions\n${hasAbsentSuppressions}\n\n`;
    isTypeSpec = pkg.typespecProject !== undefined;
    context.logger.info(
      `package [${pkg.name}] ` +
      `result [${pkg.status}] ` +
      `language [${pkg.language}] ` +
      `isTypeSpec [${isTypeSpec}] ` +
      `hasBreakingChange [${pkg.hasBreakingChange}] ` +
      `isDataPlane [${pkg.isDataPlane}] ` +
      `isBetaMgmtSdk [${pkg.isBetaMgmtSdk}] ` +
      `hasSuppressions [${hasSuppressions}] ` +
      `hasAbsentSuppressions [${hasAbsentSuppressions}]`
    );
    hasPkgFromTypeSpec = hasPkgFromTypeSpec || isTypeSpec;
  }

  // only for .NET SDK, use the flag returned from the .NET automation to mark the whole generationFromTypeSpec
  if (context.config.sdkName.includes('net') && hasPkgFromTypeSpec) {
    generateFromTypeSpec = true;
  }
 
  if (context.config.pullNumber && markdownContent) {
    try {
      // Write a markdown file to be rendered by the Azure DevOps pipeline
      const fileNamePrefix = extractPathFromSpecConfig(context.config.tspConfigPath, context.config.readmePath);
      const markdownFilePath = path.join(context.config.workingFolder, `out/logs/${fileNamePrefix}-package-report.md`);
      context.logger.info(`Writing markdown to ${markdownFilePath}`);
      if (fs.existsSync(markdownFilePath)) {
        fs.rmSync(markdownFilePath);
      }
      fs.writeFileSync(markdownFilePath, markdownContent);
      vsoAddAttachment(`Generation Summary for ${specConfigPath}`, markdownFilePath);
    } catch (e) {
      message = toolError(`Fails to write markdown file. Details: ${e}`);
      context.logger.error(message);
      vsoLogError(context, message);
    }
  }

  executionReport = {
    packages: packageReports,
    executionResult: context.status,
    isSdkConfigDuplicated: context.isSdkConfigDuplicated,
    fullLogPath: context.fullLogFileName,
    filteredLogPath: context.filteredLogFileName,
    stagedArtifactsFolder: context.stagedArtifactsFolder,
    sdkArtifactFolder: context.sdkArtifactFolder,
    generateFromTypeSpec,
    ...(context.config.runEnv === 'azureDevOps' ? {vsoLogPath: context.vsoLogFileName} : {})
  };

  deleteTmpJsonFile(context, 'execution-report.json');
  writeTmpJsonFile(context, 'execution-report.json', executionReport);
  if (context.status === 'failed') {
    message = `The generation process failed for ${specConfigPath}. Refer to the full log for details.`;
    context.logger.error(message);
    vsoLogError(context, message);
  } 
  else if (context.status === 'notEnabled') {
    message = toolWarning(`SDK configuration is not enabled for ${specConfigPath}. Refer to the full log for details.`);
    context.logger.warn(message);
    vsoLogWarning(context, message);
  } else {
    context.logger.info(`Main status [${context.status}]`);
  }

  context.logger.log('endsection', 'Generate report');
}

export const saveVsoLog = (context: WorkflowContext) => {
  const vsoLogFileName = context.vsoLogFileName;
  context.logger.log('section', `Save log to ${vsoLogFileName}`);
  try {
    const content = JSON.stringify(mapToObject(context.vsoLogs), null, 2);
    fs.writeFileSync(context.vsoLogFileName, content);
  } catch (error) {
    const message = toolError(`Fails to write log to ${vsoLogFileName}. Details: ${error}`);
    context.logger.error(message);
    return
  }
  context.logger.log('endsection', `Save log to ${vsoLogFileName}`);
}

export const saveFilteredLog = (context: WorkflowContext) => {
  context.logger.log('section', 'Save filtered log');
  let hasBreakingChange = false;
  let isBetaMgmtSdk = true;
  let isDataPlane = true;
  let showLiteInstallInstruction = false;
  let hasSuppressions = false
  let hasAbsentSuppressions = false;
  let message = "";
  if (context.pendingPackages.length > 0) {
    setSdkAutoStatus(context, 'failed');
    setFailureType(context, FailureType.SpecGenSdkFailed);
    message = toolError(`The following packages are still pending in code generation.`);
    context.logger.error(message);
    for (const pkg of context.pendingPackages) {
      context.logger.error(`\t${pkg.name}`);
      context.handledPackages.push(pkg);
    }
  }
  
  for (const pkg of context.handledPackages) {
    setSdkAutoStatus(context, pkg.status);
    hasBreakingChange = hasBreakingChange || Boolean(pkg.hasBreakingChange);
    isBetaMgmtSdk = isBetaMgmtSdk && Boolean(pkg.isBetaMgmtSdk);
    isDataPlane = isDataPlane && Boolean(pkg.isDataPlane);
    hasSuppressions = hasSuppressions || Boolean(pkg.presentSuppressionLines.length > 0);
    hasAbsentSuppressions = hasAbsentSuppressions || Boolean(pkg.absentSuppressionLines.length > 0);
    showLiteInstallInstruction = showLiteInstallInstruction || !!pkg.liteInstallationInstruction;
  }

  const extra = { hasBreakingChange, showLiteInstallInstruction };

  try {
    let commentBody = renderHandlebarTemplate(commentDetailView, context, extra);
    const statusMap = {
      pending: 'Error',
      inProgress: 'Error',
      failed: 'Error',
      warning: 'Warning',
      succeeded: 'Info',
      notEnabled: 'Warning'
    } as const;
    const type = statusMap[context.status];
    const filteredResultData = {
      type: 'Markdown',
      mode: 'replace',
      level: type,
      message: commentBody,
      time: new Date(),
    } as MessageRecord;

    context.logger.info(`Writing filtered log to ${context.filteredLogFileName}`);
    const content = JSON.stringify(filteredResultData);
    fs.writeFileSync(context.filteredLogFileName, content);
  } catch (error) {
    message = toolError(`Fails to write log to ${context.filteredLogFileName}. Details: ${error}`);
    context.logger.error(message);
    vsoLogError(context, message);
  }

  context.logger.log('endsection', 'Save filtered log status');
};

export const generateHtmlFromFilteredLog = (context: WorkflowContext) => {
    context.logger.log('section', 'Generate HTML from filtered log');
    const RegexMarkdownSplit = /^(.*?)(<ul>.*)$/s;
    const RegexNoteBlock = /> \[!NOTE\]\s*>\s*(.*)/;
    let messageRecord: string | undefined = undefined;
    try {
      messageRecord = fs.readFileSync(context.filteredLogFileName).toString();
      const parseMessageRecord = JSON.parse(messageRecord) as MessageRecord;
      let pageBody = '';
      const markdown = parseMessageRecord.message || '';
      let noteBlockInfo = '';
      let mainContent = '';
      const match = markdown.match(RegexMarkdownSplit);
      if (match !== null) {
          mainContent = match[2].trim();
          const noteBlock = match[1].trim();
          const noteBlockMatch = noteBlock.match(RegexNoteBlock);
          if (noteBlockMatch !== null) {
              noteBlockInfo = noteBlockMatch[1].trim();
          }
      } else {
          mainContent = marked(markdown) as string;
      }
      const noteBlockHtml = noteBlockInfo && generateNoteBlockTemplate(noteBlockInfo);
      pageBody += (noteBlockHtml  + mainContent);

      // eg: spec-gen-sdk-net result
      const pageTitle = `spec-gen-sdk-${context.config.sdkName.substring("azure-sdk-for-".length)} result`;
      const generatedHtml: string = generateHtmlTemplate(pageBody, pageTitle );
      context.logger.info(`Writing html to ${context.htmlLogFileName}`);
      fs.writeFileSync(context.htmlLogFileName, generatedHtml , "utf-8");
    } catch (error) {
      const message = toolError(`Fails to generate html log '${context.htmlLogFileName}'. Details: ${error}`);
      context.logger.error(message);
      vsoLogError(context, message);
    }

    context.logger.log('endsection', 'Generate HTML from filtered log');
}

function generateHtmlTemplate(pageBody:string, pageTitle:string):string {
    const githubStylesheet = "https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.2.0/github-markdown.min.css";
    return `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${pageTitle}</title>
    <link rel="stylesheet" href="${githubStylesheet}">
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif, "Apple Color Emoji", "Segoe UI Emoji";
            line-height: 1.6;
            padding: 40px;
        }
        .markdown-body {
            box-sizing: border-box;
            min-width: 200px;
            max-width: 980px;
            margin: 0 auto;
        }
        table {
            border-collapse: collapse;
            border-spacing: 0;
        }
        a {
            text-decoration: underline!important;
            text-underline-offset: .2rem!important;
        }
         /* GitHub Special prompt block */
        .markdown-alert.markdown-alert-note {
            border-left-color: #0969da;
        }
        .markdown-alert.markdown-alert-note .markdown-alert-title {
            color: #0969da;
        }
        .markdown-body>*:first-child {
            margin-top: 0 !important;
        }
        .markdown-alert {
           padding: 0.5rem 1rem;
           margin-bottom: 1rem;
           color: inherit;
           border-left: .25em solid #d1d9e0;
        }
        .markdown-alert .markdown-alert-title {
            display: flex;
            font-weight: 500;
            align-items: center;
            line-height: 1;
        }
        .markdown-alert>:first-child {
            margin-top: 0;
        }
        .markdown-body p, .markdown-body blockquote, .markdown-body ul, .markdown-body ol, .markdown-body dl, .markdown-body table, .markdown-body pre, .markdown-body details {
            margin-top: 0;
            margin-bottom: 1rem;
        }
        .mr-2 {
            margin-right: 0.5rem !important;
        }
        .octicon {
            display: inline-block;
            overflow: visible !important;
            vertical-align: text-bottom;
            fill: currentColor;
        }
    </style>
</head>
<body>
    <article class="markdown-body">
        ${pageBody}
    </article>
</body>
</html>
`;
}

function generateNoteBlockTemplate(noteBlockInfo: string):string {
return `
<div class="markdown-alert markdown-alert-note" dir="auto">
    <p class="markdown-alert-title" dir="auto">
    <svg class="octicon octicon-info mr-2" viewBox="0 0 16 16" version="1.1" width="16" height="16" aria-hidden="true"><path d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8Zm8-6.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13ZM6.5 7.75A.75.75 0 0 1 7.25 7h1a.75.75 0 0 1 .75.75v2.75h.25a.75.75 0 0 1 0 1.5h-2a.75.75 0 0 1 0-1.5h.25v-2h-.25a.75.75 0 0 1-.75-.75ZM8 6a1 1 0 1 1 0-2 1 1 0 0 1 0 2Z"></path></svg>
        Note
    </p>
    <p dir="auto">${noteBlockInfo}</p>
</div>
`
}
