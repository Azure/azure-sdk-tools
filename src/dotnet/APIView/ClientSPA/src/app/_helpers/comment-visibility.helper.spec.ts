import { getVisibleComments } from './comment-visibility.helper';
import { CommentItemModel, CommentSource } from '../_models/commentItemModel';

describe('getVisibleComments', () => {
  it('should include all user comments regardless of revision', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments);

    expect(result.userComments).toHaveLength(2);
    expect(result.allVisibleComments).toHaveLength(2);
  });

  it('should include all AI-generated comments regardless of revision', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.AIGenerated, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.AIGenerated, apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments);

    expect(result.aiGeneratedComments).toHaveLength(2);
    expect(result.allVisibleComments).toHaveLength(2);
  });

  it('should categorize mixed comment sources correctly', () => {
    const comments = [
      { id: 'c1', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-1' },
      { id: 'c2', commentSource: CommentSource.AIGenerated, apiRevisionId: 'rev-1' },
      { id: 'c3', commentSource: CommentSource.UserGenerated, apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments);

    expect(result.userComments).toHaveLength(2);        // c1, c3
    expect(result.aiGeneratedComments).toHaveLength(1); // c2
    expect(result.allVisibleComments).toHaveLength(3);  // c1, c2, c3
  });

  it('should treat comments with undefined commentSource as user comments', () => {
    const comments = [
      { id: 'c1', apiRevisionId: 'rev-1' },
      { id: 'c2', apiRevisionId: 'rev-2' },
    ] as CommentItemModel[];

    const result = getVisibleComments(comments);

    expect(result.userComments).toHaveLength(2);
    expect(result.allVisibleComments).toHaveLength(2);
  });
});
