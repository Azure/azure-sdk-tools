import { MessageRecord, sendSuccess, sendFailure } from '@azure/swagger-validation-common';
import * as fs from 'fs';
import * as path from 'path';
import * as prettier from 'prettier';
import * as Handlebars from 'handlebars';

import { getSDKAutomationStateString, SDKAutomationState } from '../sdkAutomationState';
import { setSdkAutoStatus } from '../utils/runScript';
import { FailureType, setFailureType, WorkflowContext } from './workflow';
import { updateBreakingChangesLabel } from './updateBreakingChangesLabels';
import { formatSuppressionLine } from '../utils/reportFormat';
import { removeAnsiEscapeCodes } from '../utils/utils';
import { CommentCaptureTransport } from './logging';

const commentLimit = 60;

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

  await updateBreakingChangesLabel(context, hasBreakingChange, isDataPlane, isBetaMgmtSdk, hasSuppressions, hasAbsentSuppressions);

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

  const outputPath = path.join(context.workingFolder, 'pipe.log');
  context.logger.info(`Writing unified pipeline message to ${outputPath}`);
  const content = JSON.stringify(pipelineResultData);

  fs.writeFileSync(outputPath, content);

  context.logger.log('endsection', 'Report status');
  context.logger.remove(captureTransport);
};

export const prettyFormatHtml = (s: string) => {
  return prettier.format(s, { parser: 'html' }).replace(/\<br\>/gi, '<br>\n');
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

  commentBody = commentBody.replace(/\<BR\>/g, '\n');

  return commentBody;
};

function unifiedRenderingMessages(message: string[], title?: string): string {
  return `<pre>${title ? `<strong>${title}</strong><BR>` : ''}${message.map(trimNewLine).join('<BR>')}</pre>`;
}
