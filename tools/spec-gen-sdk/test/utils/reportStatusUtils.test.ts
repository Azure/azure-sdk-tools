import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as Handlebars from 'handlebars';

// Mock dependencies
vi.mock('node:fs', () => ({
    readFileSync: vi.fn(() => 'Hello {{name}}')
}));

vi.mock('handlebars', async () => {
    const actual: any = await vi.importActual('handlebars');
    return {
        ...actual,
        compile: vi.fn((tpl: string) => (ctx: any) => tpl.replace('{{name}}', ctx.name || '')),
        registerHelper: vi.fn(),
        escapeExpression: vi.fn((str: string) => str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'))
    };
});

vi.mock('../../src/automation/sdkAutomationState', () => ({
    getSDKAutomationStateString: (status: string) => `State: ${status}`,
    SDKAutomationState: {
        pending: 'pending',
        failed: 'failed',
        inProgress: 'inProgress',
        succeeded: 'succeeded',
        warning: 'warning',
        notEnabled: 'notEnabled'
    }
}));

vi.mock('../../src/utils/reportFormat', () => ({
    formatSuppressionLine: (lines: string[]) => lines.map(l => `formatted:${l}`)
}));

vi.mock('../../src/utils/utils', () => ({
    removeAnsiEscapeCodes: (msg: any) => msg
}));

import {
    commentDetailView,
    renderHandlebarTemplate,
    trimNewLine,
    handleBarHelpers,
    unifiedRenderingMessages
} from '../../src/utils/reportStatusUtils';

describe('reportStatusUtils', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    describe('commentDetailView', () => {
        it('should compile commentDetailView template', () => {
            const result = commentDetailView({ name: 'World' });
            expect(result).toContain('Hello World');
        });

        it('should handle empty context', () => {
            const result = commentDetailView({});
            expect(result).toBe('Hello ');
        });
    });

    describe('trimNewLine', () => {
        it('should trim end of line and escape HTML', () => {
            const result = trimNewLine('  hello world  ');
            expect(result).toBe('  hello world');
        });

        it('should escape HTML characters', () => {
            const result = trimNewLine('<script>alert("test")</script>');
            expect(result).toBe('&lt;script&gt;alert("test")&lt;/script&gt;');
        });

        it('should handle empty string', () => {
            const result = trimNewLine('');
            expect(result).toBe('');
        });
    });

    describe('unifiedRenderingMessages', () => {
        it('should render messages with title', () => {
            const result = unifiedRenderingMessages(['msg1', 'msg2'], 'Test Title');
            expect(result).toContain('<strong>Test Title</strong>');
            expect(result).toContain('msg1<BR>msg2');
        });

        it('should render messages without title', () => {
            const result = unifiedRenderingMessages(['msg1', 'msg2']);
            expect(result).not.toContain('<strong>');
            expect(result).toContain('msg1<BR>msg2');
        });

        it('should handle empty messages array', () => {
            const result = unifiedRenderingMessages([]);
            expect(result).toBe('<pre></pre>');
        });
    });

    describe('renderHandlebarTemplate', () => {
        it('should renderHandlebarTemplate and clean up html', () => {
            const renderFn = (ctx: any) => `<div>Hi\n</div> <span>Test</span>`;
            const context = { foo: 'bar' } as any;
            const extra = { hasBreakingChange: true };
            const result = renderHandlebarTemplate(renderFn, context, extra);
            expect(result).toContain('<div>Hi</div><span>Test</span>');
            expect(result).not.toContain('\n');
        });

        it('should replace <BR> with newline in renderHandlebarTemplate', () => {
            const renderFn = (ctx: any) => `<div>Hi<BR></div>`;
            const result = renderHandlebarTemplate(renderFn, {} as any, {});
            expect(result).toContain('Hi\n');
        });

        it('should merge context and extra properties', () => {
            const renderFn = vi.fn((ctx: any) => `${ctx.foo}-${ctx.hasBreakingChange}`);
            const context = { foo: 'bar' } as any;
            const extra = { hasBreakingChange: true };
            renderHandlebarTemplate(renderFn, context, extra);
            expect(renderFn).toHaveBeenCalledWith({ foo: 'bar', hasBreakingChange: true });
        });

        it('should remove newlines from template output', () => {
            const renderFn = (ctx: any) => `<div>Line1\nLine2\nLine3</div>`;
            const result = renderHandlebarTemplate(renderFn, {} as any, {});
            expect(result).toBe('<div>Line1Line2Line3</div>');
        });

        it('should clean up adjacent HTML tags', () => {
            const renderFn = (ctx: any) => `<div> </div> <span> </span>`;
            const result = renderHandlebarTemplate(renderFn, {} as any, {});
            expect(result).toBe('<div></div><span></span>');
        });
    });

    describe('handleBarHelpers', () => {
        describe('renderStatus', () => {
            it('should return emoji code for each status', () => {
                expect(handleBarHelpers.renderStatus('pending')).toBe('<code>⌛</code>');
                expect(handleBarHelpers.renderStatus('failed')).toBe('<code>❌</code>');
                expect(handleBarHelpers.renderStatus('inProgress')).toBe('<code>🔄</code>');
                expect(handleBarHelpers.renderStatus('succeeded')).toBe('<code>️✔️</code>');
                expect(handleBarHelpers.renderStatus('warning')).toBe('<code>⚠️</code>');
                expect(handleBarHelpers.renderStatus('notEnabled')).toBe('<code>🚫</code>');
            });
        });

        describe('renderStatusName', () => {
            it('should return state string', () => {
                expect(handleBarHelpers.renderStatusName('pending')).toBe('State: pending');
                expect(handleBarHelpers.renderStatusName('failed')).toBe('State: failed');
                expect(handleBarHelpers.renderStatusName('succeeded')).toBe('State: succeeded');
            });
        });

        describe('renderMessagesUnifiedPipeline', () => {
            it('should return empty string for undefined messages', () => {
                const result = handleBarHelpers.renderMessagesUnifiedPipeline(undefined, 'failed');
                expect(result).toBe('');
            });

            it('should handle string messages', () => {
                const msg = 'Error\nLine2';
                const html = handleBarHelpers.renderMessagesUnifiedPipeline(msg, 'failed');
                expect(html).toBe('Error<BR>Line2');
            });

            it('should handle array messages within limit', () => {
                const arr = ['msg1', 'msg2'];
                const html = handleBarHelpers.renderMessagesUnifiedPipeline(arr, 'failed');
                expect(html).toContain('<pre>msg1<BR>msg2</pre>');
            });

            it('should handle array messages exceeding limit for failed status', () => {
                const arr = Array(100).fill('msg');
                const html = handleBarHelpers.renderMessagesUnifiedPipeline(arr, 'failed');
                expect(html).toContain('Only showing 60 items here');
                expect(html).toContain('<pre>');
            });

            it('should not trim messages for succeeded status', () => {
                const arr = Array(100).fill('msg');
                const html = handleBarHelpers.renderMessagesUnifiedPipeline(arr, 'succeeded');
                expect(html).not.toContain('Only showing');
                expect(html).toContain('<pre>');
            });

            it('should handle empty array', () => {
                const html = handleBarHelpers.renderMessagesUnifiedPipeline([], 'failed');
                expect(html).toBe('<pre></pre>');
            });
        });

        describe('renderPresentSuppressionLines', () => {
            it('should render present suppression lines', () => {
                const html = handleBarHelpers.renderPresentSuppressionLines(['line1', 'line2']);
                expect(html).toContain('Present SDK breaking changes suppressions');
                expect(html).toContain('<pre>');
                expect(html).toContain('line1<BR>line2');
            });

            it('should handle empty array', () => {
                const html = handleBarHelpers.renderPresentSuppressionLines([]);
                expect(html).toContain('Present SDK breaking changes suppressions');
                expect(html).toContain('<pre>');
            });
        });

        describe('renderAbsentSuppressionLines', () => {
            it('should render absent suppression lines with formatting', () => {
                const html = handleBarHelpers.renderAbsentSuppressionLines(['line1', 'line2']);
                expect(html).toContain('Absent SDK breaking changes suppressions');
                expect(html).toContain('formatted:line1');
                expect(html).toContain('formatted:line2');
            });

            it('should handle empty array', () => {
                const html = handleBarHelpers.renderAbsentSuppressionLines([]);
                expect(html).toContain('Absent SDK breaking changes suppressions');
            });
        });

        describe('renderParseSuppressionLinesErrors', () => {
            it('should render parse suppression errors', () => {
                const html = handleBarHelpers.renderParseSuppressionLinesErrors(['error1', 'error2']);
                expect(html).toContain('Parse Suppression File Errors');
                expect(html).toContain('<pre>');
                expect(html).toContain('error1<BR>error2');
            });

            it('should handle empty array', () => {
                const html = handleBarHelpers.renderParseSuppressionLinesErrors([]);
                expect(html).toContain('Parse Suppression File Errors');
                expect(html).toBe('<pre><strong>Parse Suppression File Errors</strong><BR></pre>');
            });
        });

        describe('renderPullRequestLink', () => {
            it('should render pull request link', () => {
                const html = handleBarHelpers.renderPullRequestLink('https://github.com/foo', '123');
                expect(html).toContain('href="https://github.com/foo/pull/123"');
                expect(html).toContain('#123');
                expect(html).toContain('data-hovercard-type="pull_request"');
                expect(html).toContain('issue-link js-issue-link');
            });

            it('should handle different repo URLs', () => {
                const html = handleBarHelpers.renderPullRequestLink('https://github.com/azure/azure-rest-api-specs', '456');
                expect(html).toContain('href="https://github.com/azure/azure-rest-api-specs/pull/456"');
                expect(html).toContain('#456');
            });
        });

        describe('renderCommitLink', () => {
            it('should render commit link with short SHA', () => {
                const html = handleBarHelpers.renderCommitLink('https://github.com/foo', 'abcdef123456789');
                expect(html).toContain('href="https://github.com/foo/commit/abcdef123456789"');
                expect(html).toContain('<tt>abcdef1</tt>');
                expect(html).toContain('data-hovercard-type="commit"');
            });

            it('should handle short commit SHA', () => {
                const html = handleBarHelpers.renderCommitLink('https://github.com/foo', 'abc123');
                expect(html).toContain('<tt>abc123</tt>');
            });
        });

        describe('shouldRender', () => {
            it('should return true for valid conditions', () => {
                expect(handleBarHelpers.shouldRender(['msg'], false, true)).toBe(true);
                expect(handleBarHelpers.shouldRender(true, false, true)).toBe(true);
            });

            it('should return false for empty array', () => {
                expect(handleBarHelpers.shouldRender([], false, true)).toBe(false);
            });

            it('should return false for falsy non-array values', () => {
                expect(handleBarHelpers.shouldRender(false, false, true)).toBe(false);
                expect(handleBarHelpers.shouldRender(undefined, false, true)).toBe(false);
            });

            it('should return false when isBetaMgmtSdk is true', () => {
                expect(handleBarHelpers.shouldRender(['msg'], true, true)).toBe(false);
            });

            it('should return false when hasBreakingChange is false', () => {
                expect(handleBarHelpers.shouldRender(['msg'], false, false)).toBe(false);
            });

            it('should return false when hasBreakingChange is undefined', () => {
                expect(handleBarHelpers.shouldRender(['msg'], false, undefined)).toBe(false);
            });

            it('should handle edge cases', () => {
                expect(handleBarHelpers.shouldRender(['msg'], undefined, true)).toBe(true);
                expect(handleBarHelpers.shouldRender(['0'], false, true)).toBe(true);
            });
        });
    });

});
