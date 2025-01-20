import { MessageRecord, sendSuccess, sendFailure, sendPipelineVariable } from '../types/Message';
import * as fs from 'fs';
import * as path from 'path';
import * as prettier from 'prettier';
import * as Handlebars from 'handlebars';

import { getSDKAutomationStateString, SDKAutomationState } from './sdkAutomationState';
import { setSdkAutoStatus } from '../utils/runScript';
import { FailureType, setFailureType, WorkflowContext } from './workflow';
import { formatSuppressionLine } from '../utils/reportFormat';
import { removeAnsiEscapeCodes } from '../utils/utils';
import { CommentCaptureTransport } from './logging';
import { ExecutionReport, PackageReport } from '../types/ExecutionReport';
import { writeTmpJsonFile } from '../utils/fsUtils';
import { getGenerationBranchName } from '../types/PackageData';
import { marked } from "marked";

const commentLimit = 60;

export const generateReport = (context: WorkflowContext) => {
  context.logger.log('section', 'Generate report');
  let executionReport: ExecutionReport;
  const packageReports: PackageReport[] = [];

  let hasSuppressions = false
  let hasAbsentSuppressions = false;
  let areBreakingChangeSuppressed = false;
  let shouldLabelBreakingChange = false;
  if (context.pendingPackages.length > 0) {
    setSdkAutoStatus(context, 'failed');
    setFailureType(context, FailureType.PipelineFrameworkFailed);
    context.logger.error(`GenerationError: The following packages are still pending.`);
    for (const pkg of context.pendingPackages) {
      context.logger.error(`\t${pkg.name}`);
      context.handledPackages.push(pkg);
    }
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
    context.logger.info(`package [${pkg.name}] hasBreakingChange [${pkg.hasBreakingChange}] isBetaMgmtSdk [${pkg.isBetaMgmtSdk}] hasSuppressions [${hasSuppressions}] hasAbsentSuppressions [${hasAbsentSuppressions}]`);
  }

  executionReport = {
    packages: packageReports,
    executionResult: context.status,
    fullLogPath: context.fullLogFileName,
    filteredLogPath: context.filteredLogFileName,
    sdkArtifactFolder: context.sdkArtifactFolder,
    sdkApiViewArtifactFolder: context.sdkApiViewArtifactFolder
  };

  writeTmpJsonFile(context, 'executionReport.json', executionReport);
  context.logger.info(`Main status [${context.status}]`);
  if (context.config.runEnv === 'azureDevOps') {
    context.logger.info("Set pipeline variables.");
    setPipelineVariables(context, executionReport);
  }

  if (context.status === 'failed') {
    console.log(`##vso[task.complete result=Failed;]`);
    sendFailure();
  } else {
    sendSuccess();
  }

  context.logger.log('endsection', 'Generate report');
}

export const saveFilteredLog = async (context: WorkflowContext) => {
  context.logger.log('section', 'Save filtered log');
  let hasBreakingChange = false;
  let isBetaMgmtSdk = true;
  let isDataPlane = true;
  let showLiteInstallInstruction = false;
  let hasSuppressions = false
  let hasAbsentSuppressions = false;
  if (context.pendingPackages.length > 0) {
    setSdkAutoStatus(context, 'failed');
    setFailureType(context, FailureType.PipelineFrameworkFailed);
    context.logger.error(`GenerationError: The following packages are still pending.`);
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
  let commentBody = renderHandlebarTemplate(commentDetailView, context, extra);
  const statusMap = {
    pending: 'Error',
    inProgress: 'Error',
    failed: 'Error',
    warning: 'Warning',
    succeeded: 'Info'
  } as const;
  const type = statusMap[context.status];
  const filteredResultData = [
    {
      type: 'Markdown',
      mode: 'replace',
      level: type,
      message: commentBody,
      time: new Date()
    } as MessageRecord
  ].concat(context.extraResultRecords);

  context.logger.info(`Writing filtered log to ${context.filteredLogFileName}`);
  const content = JSON.stringify(filteredResultData);
  fs.writeFileSync(context.filteredLogFileName, content);
  context.logger.log('endsection', 'Save filtered log status');
};

export const generateHtmlFromFilteredLog = async (context: WorkflowContext) => {
    context.logger.log('section', 'Save filtered log HTML');
    const RegexMarkdownSplit = /^(.*?)(<ul>.*)$/s;
    const RegexNoteBlock = /> \[!NOTE\]\s*>\s*(.*)/;
    let messageRecord: string | undefined = undefined;
    try {
        messageRecord = fs.readFileSync(context.filteredLogFileName).toString();
    } catch (error) {
        context.logger.error(`IOError: Fails to read log in'${context.filteredLogFileName}', Details: ${error}`)
        return;
    }

    const parseMessageRecord = JSON.parse(messageRecord) as MessageRecord[];

    let totalBody = '';
    for(let commentBody of parseMessageRecord) {
        const markdown = commentBody.message || '';
        if (!markdown) continue;
        let blockString = '';
        let bodyString = '';
        
        const match = markdown.match(RegexMarkdownSplit);
        if (match !== null) { 
            bodyString = match[2].trim(); 
            const noteBlock = match[1].trim();
            const noteMatch = noteBlock.match(RegexNoteBlock);
            if (noteMatch !== null) {
                blockString = noteMatch[1].trim();
            }
        } else {
            bodyString = marked(markdown) as string;
        }
        const renderNoteBlock = blockString && htmlBlockTemp(blockString);
        totalBody += (renderNoteBlock + bodyString);
    }


    const htmlString: string = htmlTemp(totalBody, `spec-gen-sdk ${context.config.sdkName}`);
    
    context.logger.info(`Writing html to ${context.htmlLogFileName}`);
    fs.writeFileSync(context.htmlLogFileName, htmlString, "utf-8");
    context.logger.log('endsection', 'Save filtered log HTML');
}

function htmlTemp(bodyString:string, title:string):string {
    const githubStylesheet = "https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.2.0/github-markdown.min.css";
    return `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${title}</title>
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
        ${bodyString}
    </article>
</body>
</html>
`;
}

function htmlBlockTemp(blockString: string):string {
return `
<div class="markdown-alert markdown-alert-note" dir="auto">
    <p class="markdown-alert-title" dir="auto">
    <svg class="octicon octicon-info mr-2" viewBox="0 0 16 16" version="1.1" width="16" height="16" aria-hidden="true"><path d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8Zm8-6.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13ZM6.5 7.75A.75.75 0 0 1 7.25 7h1a.75.75 0 0 1 .75.75v2.75h.25a.75.75 0 0 1 0 1.5h-2a.75.75 0 0 1 0-1.5h.25v-2h-.25a.75.75 0 0 1-.75-.75ZM8 6a1 1 0 1 1 0-2 1 1 0 0 1 0 2Z"></path></svg>
        Note
    </p>
    <p dir="auto">${blockString}</p>
</div>
`
}

export const sdkAutoReportStatus = async (context: WorkflowContext) => {
  context.logger.log('section', 'Report status');

  const captureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['error', 'warn'],
    level: 'debug',
    output: context.messages
  });
  context.logger.add(captureTransport);

  let hasBreakingChange = false;
  let isBetaMgmtSdk = true;
  let isDataPlane = true;
  let showLiteInstallInstruction = false;
  let hasSuppressions = false
  let hasAbsentSuppressions = false;
  if (context.pendingPackages.length > 0) {
    setSdkAutoStatus(context, 'failed');
    setFailureType(context, FailureType.PipelineFrameworkFailed);
    context.logger.error(`GenerationError: The following packages are still pending.`);
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

  context.logger.info(`Main status [${context.status}] hasBreakingChange [${hasBreakingChange}] isBetaMgmtSdk [${isBetaMgmtSdk}] hasSuppressions [${hasSuppressions}] hasAbsentSuppressions [${hasAbsentSuppressions}]`);
  if (context.status === 'failed') {
    console.log(`##vso[task.complete result=Failed;]`);
    sendFailure();
  } else {
    sendSuccess();
  }

  const extra = { hasBreakingChange, showLiteInstallInstruction };
  let subTitle = renderHandlebarTemplate(commentSubTitleView, context, extra);
  let commentBody = renderHandlebarTemplate(commentDetailView, context, extra);

  try {
    context.logger.info(`Rendered commentSubTitle: ${prettyFormatHtml(subTitle)}`);
    context.logger.info(`Rendered commentBody: ${prettyFormatHtml(commentBody)}`);
  } catch (e) {
    context.logger.error(`RenderingError: exception is thrown while rendering the title and the body. Error details: ${e.message} ${e.stack}. This doesn't impact the SDK generation, and please click over the details link to view the full pipeine log.`);
    // To add log to PR comment
    subTitle = renderHandlebarTemplate(commentSubTitleView, context, extra);
    commentBody = renderHandlebarTemplate(commentDetailView, context, extra);
  }

  const statusMap = {
    pending: 'Error',
    inProgress: 'Error',
    failed: 'Error',
    warning: 'Warning',
    succeeded: 'Info'
  } as const;
  const type = statusMap[context.status];
  const pipelineResultData = [
    {
      type: 'Markdown',
      mode: 'replace',
      level: type,
      message: commentBody,
      time: new Date()
    } as MessageRecord
  ].concat(context.extraResultRecords);

  const encode = (str: string): string => Buffer.from(str, 'binary').toString('base64');
  console.log(`##vso[task.setVariable variable=SubTitle]${encode(subTitle)}`);

  const outputPath = path.join(context.config.workingFolder, 'pipe.log');
  context.logger.info(`Writing unified pipeline message to ${outputPath}`);
  const content = JSON.stringify(pipelineResultData);

  fs.writeFileSync(outputPath, content);

  context.logger.log('endsection', 'Report status');
  context.logger.remove(captureTransport);
};

export const prettyFormatHtml = (s: string) => {
  return prettier.format(s, { parser: 'html' }).replace(/<br>/gi, '<br>\n');
};

const commentDetailTemplate = fs.readFileSync(`${__dirname}/../templates/commentDetailNew.handlebars`).toString();
const commentDetailView = Handlebars.compile(commentDetailTemplate, { noEscape: true });
const commentSubTitleTemplate = fs.readFileSync(`${__dirname}/../templates/commentSubtitleNew.handlebars`).toString();
const commentSubTitleView = Handlebars.compile(commentSubTitleTemplate, { noEscape: true });

const htmlEscape = (s: string) => Handlebars.escapeExpression(s);

const githubStateEmoji: { [key in SDKAutomationState]: string } = {
  pending: '⌛',
  failed: '❌',
  inProgress: '🔄',
  succeeded: '️✔️',
  warning: '⚠️'
};
const trimNewLine = (line: string) => htmlEscape(line.trimEnd());
const handleBarHelpers = {
  renderStatus: (status: SDKAutomationState) => `<code>${githubStateEmoji[status]}</code>`,
  renderStatusName: (status: SDKAutomationState) => `${getSDKAutomationStateString(status)}`,
  renderMessagesUnifiedPipeline: (messages: string[] | string | undefined, status: SDKAutomationState) => {
    if (messages === undefined) {
      return '';
    }
    const messagesWithoutAnsi = removeAnsiEscapeCodes(messages);
    if (typeof messagesWithoutAnsi === 'string') {
      return messagesWithoutAnsi.replace(/\n/g, '<BR>');
    }
    // The error logs will be trimmed by the 'contentLimit'. For other messages, such as suppressions, they won't be trimmed here. Instead, their messages will be trimmed in the pipeline bot.
    if (messagesWithoutAnsi.length > commentLimit && status !== 'succeeded') {
      return `Only showing ${commentLimit} items here. Refer to log for details.<br><pre>${messagesWithoutAnsi.slice(-commentLimit).map(trimNewLine).join('<BR>')}</pre>`;
    } else {
      return `<pre>${messagesWithoutAnsi.map(trimNewLine).join('<BR>')}</pre>`;
    }
  },
  renderPresentSuppressionLines: (presentSuppressionLines: string[]) => {
    return unifiedRenderingMessages(presentSuppressionLines, 'Present SDK breaking changes suppressions');
  },
  renderAbsentSuppressionLines: (absentSuppressionLines: string[]) => {
    const _absentSuppressionLines = formatSuppressionLine(absentSuppressionLines);
    return unifiedRenderingMessages(_absentSuppressionLines, 'Absent SDK breaking changes suppressions');
  },
  renderParseSuppressionLinesErrors: (parseSuppressionLinesErrors: string[]) => {
    return `<pre><strong>Parse Suppression File Errors</strong><BR>${parseSuppressionLinesErrors.map(trimNewLine).join('<BR>')}</pre>`;
  },
  renderSDKTitleMapping: (sdkRepoName: string) => {
    switch (sdkRepoName) {
      case 'azure-cli-extensions':
        return 'Azure CLI Extension Generation';
      case 'azure-sdk-for-trenton':
        return 'Trenton Generation';
      default:
        return sdkRepoName;
    }
  },
  renderSDKNameMapping: (sdkRepoName: string) => {
    switch (sdkRepoName) {
      case 'azure-cli-extensions':
        return 'Azure CLI';
      case 'azure-sdk-for-trenton':
        return 'Trenton';
      case 'azure-resource-manager-schemas':
        return 'Schema';
      default:
        return 'SDK';
    }
  },
  shouldRender: (messages: boolean | string[] | undefined,
    isBetaMgmtSdk: boolean | undefined,
    hasBreakingChange?: boolean) => {
    if (
      ((!Array.isArray(messages) && messages) || (Array.isArray(messages) && messages.length > 0)) &&
      !isBetaMgmtSdk &&
      hasBreakingChange) {
      return true;
    }
    return false;
  }
};

Handlebars.registerHelper(handleBarHelpers);
const renderHandlebarTemplate = (
  renderFn: Handlebars.TemplateDelegate,
  context: WorkflowContext,
  extra: {
    hasBreakingChange?: boolean;
    showLiteInstallInstruction?: boolean;
  }
): string => {
  let commentBody = renderFn({
    ...context,
    ...extra
  });

  commentBody = commentBody.replace(/\n/g, '');

  commentBody = commentBody
    .replace(/(<\/?[a-z]+>)\s+(<\/?[a-z]+>)/gi, (_, p1, p2) => `${p1}${p2}`)
    .replace(/(<\/?[a-z]+>)\s+(<\/?[a-z]+>)/gi, (_, p1, p2) => `${p1}${p2}`);

  commentBody = commentBody.replace(/<BR>/g, '\n');

  return commentBody;
};

function unifiedRenderingMessages(message: string[], title?: string): string {
  return `<pre>${title ? `<strong>${title}</strong><BR>` : ''}${message.map(trimNewLine).join('<BR>')}</pre>`;
}

const setPipelineVariables = async (context: WorkflowContext, executionReport: ExecutionReport) => {
  let breakingChangeLabel = "";
  let packageName = "";
  let prBranch = "";
  let prTitle = "";
  let prBody = "";

  if (executionReport && executionReport.packages && executionReport.packages.length > 0) {
    const pkg = executionReport.packages[0];
    if (pkg.shouldLabelBreakingChange) {
      breakingChangeLabel = pkg.breakingChangeLabel ?? "";
    }
    packageName = pkg.packageName ?? "";
    if (context.config.pullNumber) {    
      prBody = `Create to sync ${context.config.specRepoHttpsUrl}/pull/${context.config.pullNumber}\n\n`;
    }
    prBranch = getGenerationBranchName(context, packageName);
    prBody = `${prBody}${pkg.installInstructions ?? ''}`;
  }
  
  prBody = `${prBody}\n This pull request has been automatically generated for preview purposes.`;
  prTitle = `[AutoPR ${packageName}]`;
  sendPipelineVariable("BreakingChangeLabel", breakingChangeLabel);
  sendPipelineVariable("PrBranch", prBranch);
  sendPipelineVariable("PrTitle", prTitle);
  sendPipelineVariable("PrBody", prBody);
  context.logger.info(`BreakingChangeLabel: ${breakingChangeLabel}, PrBranch: ${prBranch}, PrTitle: ${prTitle}, PrBody: ${prBody}`);
}
