import { readFileSync } from 'node:fs';
import * as Handlebars from 'handlebars';
import { getSDKAutomationStateString, SDKAutomationState } from '../automation/sdkAutomationState';
import { formatSuppressionLine } from '../utils/reportFormat';
import { removeAnsiEscapeCodes } from '../utils/utils';
import { WorkflowContext } from '../types/Workflow';

const commentLimit = 60;
const commentDetailTemplate = readFileSync(`${__dirname}/../templates/commentDetailNew.handlebars`).toString();
export const commentDetailView = Handlebars.compile(commentDetailTemplate, { noEscape: true });

const htmlEscape = (s: string) => Handlebars.escapeExpression(s);

const githubStateEmoji: { [key in SDKAutomationState]: string } = {
  pending: '⌛',
  failed: '❌',
  inProgress: '🔄',
  succeeded: '️✔️',
  warning: '⚠️',
  notEnabled: '🚫'
};

export const trimNewLine = (line: string) => htmlEscape(line.trimEnd());

export const handleBarHelpers = {
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
  renderPullRequestLink: (specRepoHttpsUrl: string, prNumber: string) => {
    const url = `${specRepoHttpsUrl}/pull/${prNumber}`;
    return `<a target="_blank" class="issue-link js-issue-link" href="${url}" data-hovercard-type="pull_request" data-hovercard-url="${url}/hovercard">#${prNumber}</a>`
  },
  renderCommitLink: (specRepoHttpsUrl: string, commitSha: string) => {
    const shortSha = commitSha.substring(0, 7);
    const url = `${specRepoHttpsUrl}/commit/${commitSha}`;
    return `<a target="_blank" class="commit-link" href="${url}" data-hovercard-type="commit" data-hovercard-url="${url}/hovercard"><tt>${shortSha}</tt></a>`;
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
export const renderHandlebarTemplate = (
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

export function unifiedRenderingMessages(message: string[], title?: string): string {
  return `<pre>${title ? `<strong>${title}</strong><BR>` : ''}${message.map(trimNewLine).join('<BR>')}</pre>`;
}
