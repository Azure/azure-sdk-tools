import { getVisibleComments } from './comment-visibility.helper';
import { CommentItemModel, CommentSource } from '../_models/commentItemModel';

describe('getVisibleComments', () => {
  it('should include all user comments regardless of revision', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments, 'rev-1');

    expect(result.userComments).toHaveLength(2);
    expect(result.allVisibleComments).toHaveLength(2);
  });

  it('should include all AI-generated comments regardless of revision', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.AIGenerated, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.AIGenerated, apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments, 'rev-1');

    expect(result.aiGeneratedComments).toHaveLength(2);
    expect(result.allVisibleComments).toHaveLength(2);
  });

  it('should include diagnostic comments only for active revision', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.Diagnostic, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.Diagnostic, apiRevisionId: 'rev-2' },
      { id: 'c3', commentSource: CommentSource.Diagnostic, apiRevisionId: 'rev-1' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments, 'rev-1');

    expect(result.diagnosticCommentsForRevision).toHaveLength(2);
    expect(result.diagnosticCommentsForRevision.map(c => c.id)).toEqual(['c1', 'c3']);
    expect(result.allVisibleComments).toHaveLength(2);
  });

  it('should return empty diagnostics when activeApiRevisionId is null', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.Diagnostic, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-1' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments, null);

    expect(result.diagnosticCommentsForRevision).toHaveLength(0);
    expect(result.userComments).toHaveLength(1);
    expect(result.allVisibleComments).toHaveLength(1);
  });

  it('should categorize mixed comment sources correctly', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.AIGenerated, apiRevisionId: 'rev-1' },
      { id: 'c3', commentSource: CommentSource.Diagnostic, apiRevisionId: 'rev-1' },
      { id: 'c4', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-2' },
      { id: 'c5', commentSource: CommentSource.Diagnostic, apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments, 'rev-1');

    expect(result.userComments).toHaveLength(2);       // c1, c4
    expect(result.aiGeneratedComments).toHaveLength(1); // c2
    expect(result.diagnosticCommentsForRevision).toHaveLength(1); // c3 (not c5)
    expect(result.allVisibleComments).toHaveLength(4);  // c1, c4, c2, c3
  });

  it('should treat comments with undefined commentSource as user comments', () => {
    const comments = [
      { id: 'c1', apiRevisionId: 'rev-1' },
      { id: 'c2', apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments, 'rev-1');

    expect(result.userComments).toHaveLength(2);
    expect(result.allVisibleComments).toHaveLength(2);
  });

  it('should not apply any display cap on diagnostics', () => {
    const comments = Array.from({ length: 300 }, (_, i) => ({
      id: `c${i}`,
      commentSource: CommentSource.Diagnostic,
      apiRevisionId: 'rev-1',
    })) as CommentItemModel[];

    const result = getVisibleComments(comments, 'rev-1');

    expect(result.diagnosticCommentsForRevision).toHaveLength(300);
    expect(result.allVisibleComments).toHaveLength(300);
  });
});
