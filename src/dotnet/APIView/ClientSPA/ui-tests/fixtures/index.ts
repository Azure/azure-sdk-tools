const defaultNodeMetaData = {
  documentation: [],
  diagnostics: [],
  codeLines: [],
  commentThread: {},
  isNodeWithDiff: false,
  isNodeWithDiffInDescendants: false,
  isNodeWithNoneDocDiffInDescendants: false,
  bottomTokenNodeIdHash: '',
  isProcessed: false,
  relatedNodeIdHash: '',
};

const defaultCodeLineProps = {
  type: 'codeLine' as const,
  rowPositionInGroup: 0,
  associatedRowPositionInGroup: 0,
  rowClasses: [],
  diffKind: '',
  toggleDocumentationClasses: '',
  toggleCommentsClasses: '',
  diagnostics: null,
  comments: [],
  isHiddenAPI: false,
  crossLanguageId: '',
};

const defaultCommentProps = {
  crossLanguageId: '',
  correlationId: '',
  isResolved: false,
  upvotes: [],
  downvotes: [],
  taggedUsers: [],
  commentType: 1,
  resolutionLocked: false,
  lastEditedOn: null,
  isDeleted: false,
  isInEditMode: false,
  hasRelatedComments: false,
  relatedCommentsCount: 0,
  commentSource: null,
  guidelineIds: [],
  memoryIds: [],
  confidenceScore: 0.0,
};

const defaultCommentThreadProps = {
  type: 'commentThread' as const,
  rowClasses: ['user-comment-thread'],
  diffKind: 'noneDiff',
  isResolvedCommentThread: false,
  commentThreadIsResolvedBy: '',
  showReplyTextBox: false,
};

function createCodeLine(overrides: {
  lineNumber: number;
  rowOfTokens: Array<{ value: string; renderClasses: string[] }>;
  nodeId: string;
  nodeIdHashed: string;
  indent?: number;
  rowPositionInGroup?: number;
  toggleCommentsClasses?: string;
  diffKind?: string;
  rowClasses?: string[];
}) {
  return {
    ...defaultCodeLineProps,
    ...overrides,
    indent: overrides.indent ?? 0,
  };
}

function createNodeMeta(overrides: {
  nodeIdHashed: string;
  label: string;
  parentNodeIdHashed: string;
  codeLines?: any[];
  commentThread?: any;
  childrenNodeIdsInOrder?: Record<number, string>;
  navigationTreeNode?: any;
}) {
  return {
    ...defaultNodeMetaData,
    codeLines: overrides.codeLines ?? [],
    commentThread: overrides.commentThread ?? {},
    navigationTreeNode: overrides.navigationTreeNode ?? {
      label: overrides.label,
      data: { nodeIdHashed: overrides.nodeIdHashed },
      children: [],
    },
    parentNodeIdHashed: overrides.parentNodeIdHashed,
    childrenNodeIdsInOrder: overrides.childrenNodeIdsInOrder ?? {},
  };
}

function createComment(overrides: {
  id: string;
  reviewId?: string;
  apiRevisionId?: string;
  elementId: string;
  commentText: string;
  createdBy: string;
  createdOn: string;
  severity?: number;
  sectionClass?: string;
  correlationId?: string;
  hasRelatedComments?: boolean;
  relatedCommentsCount?: number;
  commentSource?: string | null;
  confidenceScore?: number;
}) {
  return {
    ...defaultCommentProps,
    id: overrides.id,
    reviewId: overrides.reviewId ?? 'test-review-id',
    apiRevisionId: overrides.apiRevisionId ?? 'revision-1',
    elementId: overrides.elementId,
    sectionClass: overrides.sectionClass ?? '',
    commentText: overrides.commentText,
    createdBy: overrides.createdBy,
    createdOn: overrides.createdOn,
    severity: overrides.severity ?? 2,
    correlationId: overrides.correlationId ?? '',
    hasRelatedComments: overrides.hasRelatedComments ?? false,
    relatedCommentsCount: overrides.relatedCommentsCount ?? 0,
    commentSource: overrides.commentSource ?? null,
    confidenceScore: overrides.confidenceScore ?? 0.0,
    changeHistory: [
      {
        changeAction: 'created',
        changedBy: overrides.createdBy,
        changedOn: overrides.createdOn,
      },
    ],
  };
}

function createCommentThread(overrides: {
  nodeId: string;
  nodeIdHashed: string;
  rowPositionInGroup?: number;
  associatedRowPositionInGroup?: number;
  comments: any[];
}) {
  return {
    ...defaultCommentThreadProps,
    nodeId: overrides.nodeId,
    nodeIdHashed: overrides.nodeIdHashed,
    rowPositionInGroup: overrides.rowPositionInGroup ?? 0,
    associatedRowPositionInGroup: overrides.associatedRowPositionInGroup ?? 0,
    comments: overrides.comments,
  };
}

export const mockUserProfile = {
  userName: 'testuser',
  email: 'testuser@microsoft.com',
  preferences: {
    theme: 'light',
    hideLeftNavigation: false,
    hideReviewPageOptions: false,
    hideLineNumbers: false,
    showHiddenApis: false,
    showDocumentation: true,
    showComments: true,
    showSystemComments: false,
  },
  languages: ['C#', 'Python', 'JavaScript', 'Java'],
};

export const mockReview = {
  id: 'test-review-id',
  packageName: 'Azure.Storage.Blobs',
  language: 'C#',
  isDeleted: false,
  isApproved: false,
  createdOn: '2025-01-15T10:30:00Z',
  lastUpdatedOn: '2025-01-16T14:22:00Z',
  createdBy: 'developer@microsoft.com',
  packageVersion: '12.0.0',
  isClosed: false,
  filterType: 0,
  changeHistory: [],
};

export const mockApiRevisions = [
  {
    id: 'revision-1',
    reviewId: 'test-review-id',
    packageVersion: '12.0.0',
    apiRevisionType: 'Manual',
    label: 'Initial Upload',
    createdOn: '2025-01-15T10:30:00Z',
    createdBy: 'developer@microsoft.com',
    isApproved: false,
    isReleased: false,
    files: [
      {
        fileId: 'file-1',
        fileName: 'Azure.Storage.Blobs.dll',
        language: 'C#',
        languageVariant: null,
        crossLanguagePackageId: null,
      },
    ],
    viewedBy: [],
    assignedReviewers: [],
  },
  {
    id: 'revision-2',
    reviewId: 'test-review-id',
    packageVersion: '12.0.0-beta.1',
    apiRevisionType: 'Automatic',
    label: 'CI Build #1234',
    createdOn: '2025-01-16T08:15:00Z',
    createdBy: 'azure-pipelines@microsoft.com',
    isApproved: true,
    isReleased: false,
    files: [
      {
        fileId: 'file-2',
        fileName: 'Azure.Storage.Blobs.dll',
        language: 'C#',
        languageVariant: null,
        crossLanguagePackageId: null,
      },
    ],
    viewedBy: ['reviewer@microsoft.com'],
    assignedReviewers: ['approver1', 'approver2'],
  },
];

export const mockComments = [
  {
    id: 'comment-1',
    reviewId: 'test-review-id',
    apiRevisionId: 'revision-1',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    sectionClass: 'section-1',
    commentText: 'This class should follow the naming convention.',
    createdOn: '2025-01-15T11:00:00Z',
    createdBy: 'reviewer@microsoft.com',
    lastEditedOn: null,
    isResolved: false,
    upvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
    crossLanguageId: null,
  },
];

export const mockCommentOwnedByUser = [
  {
    id: 'user-comment-1',
    reviewId: 'test-review-id',
    apiRevisionId: 'revision-1',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    sectionClass: 'section-1',
    commentText: 'My comment that I can edit and delete',
    createdOn: '2025-01-15T11:00:00Z',
    createdBy: 'testuser',
    lastEditedOn: null,
    isResolved: false,
    upvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
    crossLanguageId: null,
  },
];

const tokens = {
  namespace: [
    { value: 'namespace ', renderClasses: ['keyword'] },
    { value: 'Azure.Storage.Blobs', renderClasses: ['text'] },
  ],
  openBrace: [{ value: '{', renderClasses: ['punctuation'] }],
  publicClass: (name: string) => [
    { value: 'public ', renderClasses: ['keyword'] },
    { value: 'class ', renderClasses: ['keyword'] },
    { value: name, renderClasses: ['class-name'] },
  ],
  publicMethod: (name: string, params = '()') => [
    { value: 'public ', renderClasses: ['keyword'] },
    { value: 'Task ', renderClasses: ['type'] },
    { value: name, renderClasses: ['method-name'] },
    {
      value: params.startsWith('(') ? params : `(${params})`,
      renderClasses: ['punctuation'],
    },
  ],
};

const baseNodeMetaData = {
  root: {
    ...defaultNodeMetaData,
    navigationTreeNode: null,
    parentNodeIdHashed: '',
    childrenNodeIdsInOrder: { 0: 'node-namespace' },
  },
  'node-namespace': createNodeMeta({
    nodeIdHashed: 'node-namespace',
    label: 'Azure.Storage.Blobs',
    parentNodeIdHashed: 'root',
    codeLines: [
      createCodeLine({
        lineNumber: 1,
        rowOfTokens: tokens.namespace,
        nodeId: 'Azure.Storage.Blobs',
        nodeIdHashed: 'node-namespace',
      }),
      createCodeLine({
        lineNumber: 2,
        rowOfTokens: tokens.openBrace,
        nodeId: 'Azure.Storage.Blobs',
        nodeIdHashed: 'node-namespace',
        rowPositionInGroup: 1,
      }),
    ],
    childrenNodeIdsInOrder: { 0: 'node-blobclient' },
  }),
  'node-blobclient': createNodeMeta({
    nodeIdHashed: 'node-blobclient',
    label: 'BlobClient',
    parentNodeIdHashed: 'node-namespace',
    codeLines: [
      createCodeLine({
        lineNumber: 3,
        rowOfTokens: tokens.publicClass('BlobClient'),
        nodeId: 'Azure.Storage.Blobs.BlobClient',
        nodeIdHashed: 'node-blobclient',
        indent: 1,
        diffKind: 'noneDiff',
        toggleCommentsClasses: 'bi bi-chat-right-text show',
      }),
    ],
    childrenNodeIdsInOrder: { 0: 'node-upload', 1: 'node-download' },
  }),
  'node-upload': createNodeMeta({
    nodeIdHashed: 'node-upload',
    label: 'Upload',
    parentNodeIdHashed: 'node-blobclient',
    codeLines: [
      createCodeLine({
        lineNumber: 4,
        rowOfTokens: [
          { value: 'public ', renderClasses: ['keyword'] },
          { value: 'Task ', renderClasses: ['type'] },
          { value: 'Upload', renderClasses: ['method-name'] },
          { value: '(', renderClasses: ['punctuation'] },
          { value: 'string ', renderClasses: ['keyword'] },
          { value: 'path', renderClasses: ['parameter'] },
          { value: ')', renderClasses: ['punctuation'] },
        ],
        nodeId: 'Azure.Storage.Blobs.BlobClient.Upload',
        nodeIdHashed: 'node-upload',
        indent: 2,
      }),
    ],
  }),
  'node-download': createNodeMeta({
    nodeIdHashed: 'node-download',
    label: 'Download',
    parentNodeIdHashed: 'node-blobclient',
    codeLines: [
      createCodeLine({
        lineNumber: 5,
        rowOfTokens: [
          { value: 'public ', renderClasses: ['keyword'] },
          { value: 'Task ', renderClasses: ['type'] },
          { value: 'Download', renderClasses: ['method-name'] },
          { value: '(', renderClasses: ['punctuation'] },
          { value: 'string ', renderClasses: ['keyword'] },
          { value: 'path', renderClasses: ['parameter'] },
          { value: ')', renderClasses: ['punctuation'] },
        ],
        nodeId: 'Azure.Storage.Blobs.BlobClient.Download',
        nodeIdHashed: 'node-download',
        indent: 2,
      }),
    ],
  }),
};

const baseNavigationTreeNodes = [
  {
    label: 'Azure.Storage.Blobs',
    data: { nodeIdHashed: 'node-namespace' },
    expanded: true,
    visible: true,
    children: [
      {
        label: 'BlobClient',
        data: { nodeIdHashed: 'node-blobclient' },
        expanded: false,
        visible: true,
        children: [
          {
            label: 'Upload',
            data: { nodeIdHashed: 'node-upload' },
            expanded: false,
            visible: true,
            children: [
              {
                label: 'UploadAsync',
                data: { nodeIdHashed: 'node-upload-async' },
                visible: true,
              },
            ],
          },
          {
            label: 'Download',
            data: { nodeIdHashed: 'node-download' },
            visible: true,
          },
        ],
      },
    ],
  },
];

export const mockCodePanelData = {
  hasDiff: false,
  hasHiddenAPIThatIsDiff: false,
  navigationTreeNodes: baseNavigationTreeNodes,
  nodeMetaData: {
    ...baseNodeMetaData,
    'node-blobclient': {
      ...baseNodeMetaData['node-blobclient'],
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          comments: [
            createComment({
              id: 'comment-1',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText: 'Should we consider making this class sealed?',
              createdBy: 'reviewer',
              createdOn: '2025-01-15T11:00:00Z',
            }),
          ],
        })],
      },
    },
  },
};

export const mockCodePanelDataNoComments = {
  ...mockCodePanelData,
  nodeMetaData: { ...baseNodeMetaData },
};

export const mockCodePanelDataOwnedByUser = {
  ...mockCodePanelData,
  nodeMetaData: {
    ...baseNodeMetaData,
    'node-blobclient': {
      ...baseNodeMetaData['node-blobclient'],
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          comments: [
            createComment({
              id: 'user-comment-1',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText: 'My comment that I can edit and delete',
              createdBy: 'testuser',
              createdOn: '2025-01-15T11:00:00Z',
            }),
          ],
        })],
      },
    },
  },
};

export const mockDeletedReview = { ...mockReview, isDeleted: true };

export const mockApprovedRevision = {
  ...mockApiRevisions[0],
  isApproved: true,
};

const sharedCorrelationId = 'correlation-group-1';

export const mockCodePanelDataWithRelatedComments = {
  hasDiff: false,
  hasHiddenAPIThatIsDiff: false,
  navigationTreeNodes: [
    {
      ...baseNavigationTreeNodes[0],
      children: [
        {
          label: 'BlobClient',
          data: { nodeIdHashed: 'node-blobclient' },
          expanded: true,
          visible: true,
          children: [
            {
              label: 'Upload',
              data: { nodeIdHashed: 'node-upload' },
              children: [],
              visible: true,
            },
            {
              label: 'Download',
              data: { nodeIdHashed: 'node-download' },
              children: [],
              visible: true,
            },
          ],
        },
      ],
    },
  ],
  nodeMetaData: {
    ...baseNodeMetaData,
    'node-blobclient': {
      ...baseNodeMetaData['node-blobclient'],
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          comments: [
            createComment({
              id: 'related-comment-1',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText: 'This class should follow the naming convention.',
              createdBy: 'azure-sdk',
              createdOn: '2025-01-15T10:00:00Z',
              correlationId: sharedCorrelationId,
              hasRelatedComments: true,
              relatedCommentsCount: 1,
              commentSource: 'aiGenerated',
              confidenceScore: 0.95,
            }),
          ],
        })],
      },
    },
    'node-upload': {
      ...baseNodeMetaData['node-upload'],
      codeLines: [
        createCodeLine({
          lineNumber: 4,
          rowOfTokens: tokens.publicMethod('Upload', '(string path)'),
          nodeId: 'Azure.Storage.Blobs.BlobClient.Upload',
          nodeIdHashed: 'node-upload',
          indent: 2,
          toggleCommentsClasses: 'bi bi-chat-right-text show',
        }),
      ],
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient.Upload',
          nodeIdHashed: 'node-upload',
          comments: [
            createComment({
              id: 'related-comment-2',
              elementId: 'Azure.Storage.Blobs.BlobClient.Upload',
              commentText:
                'Upload method should also follow naming conventions.',
              createdBy: 'azure-sdk',
              createdOn: '2025-01-15T10:01:00Z',
              correlationId: sharedCorrelationId,
              hasRelatedComments: true,
              relatedCommentsCount: 1,
              commentSource: 'aiGenerated',
              confidenceScore: 0.9,
            }),
          ],
        })],
      },
    },
  },
};

export const mockCommentsWithRelatedComments = [
  {
    id: 'related-comment-1',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    commentText: 'This class should follow the naming convention.',
    correlationId: sharedCorrelationId,
    createdBy: 'azure-sdk',
    createdOn: '2025-01-15T10:00:00Z',
    isResolved: false,
    hasRelatedComments: true,
    relatedCommentsCount: 1,
    severity: 2,
  },
  {
    id: 'related-comment-2',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient.Upload',
    commentText: 'Upload method should also follow naming conventions.',
    correlationId: sharedCorrelationId,
    createdBy: 'azure-sdk',
    createdOn: '2025-01-15T10:01:00Z',
    isResolved: false,
    hasRelatedComments: true,
    relatedCommentsCount: 1,
    severity: 2,
  },
];

export const mockAIGeneratedComments = [
  {
    id: 'ai-comment-1',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    sectionClass: 'section-1',
    commentText:
      'This class name should follow Azure SDK naming conventions. Consider renaming to align with the style guide.',
    createdOn: '2025-01-15T10:00:00Z',
    createdBy: 'azure-sdk',
    lastEditedOn: null,
    isResolved: false,
    upvotes: [],
    downvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
    crossLanguageId: null,
    commentSource: 'aiGenerated',
    severity: 2,
  },
];

export const mockCodePanelDataWithAIComments = {
  hasDiff: false,
  nodeMetaData: {
    root: {
      ...defaultNodeMetaData,
      navigationTreeNode: {
        label: 'root',
        data: { nodeIdHashed: 'root' },
        children: [],
      },
      parentNodeIdHashed: null,
      childrenNodeIdsInOrder: { 0: 'node-namespace' },
    },
    'node-namespace': createNodeMeta({
      nodeIdHashed: 'node-namespace',
      label: 'Azure.Storage.Blobs',
      parentNodeIdHashed: 'root',
      codeLines: [
        {
          ...createCodeLine({
            lineNumber: 1,
            rowOfTokens: tokens.namespace,
            nodeId: 'Azure.Storage.Blobs',
            nodeIdHashed: 'node-namespace',
          }),
          diffKind: 'noneDiff',
          rowClasses: '',
          isDocumentation: false,
        },
      ],
      childrenNodeIdsInOrder: { 0: 'node-blobclient' },
    }),
    'node-blobclient': {
      ...createNodeMeta({
        nodeIdHashed: 'node-blobclient',
        label: 'BlobClient',
        parentNodeIdHashed: 'node-namespace',
        codeLines: [
          {
            ...createCodeLine({
              lineNumber: 3,
              rowOfTokens: [
                { value: 'public class ', renderClasses: ['keyword'] },
                { value: 'BlobClient', renderClasses: ['tname'] },
              ],
              nodeId: 'Azure.Storage.Blobs.BlobClient',
              nodeIdHashed: 'node-blobclient',
              indent: 1,
            }),
            diffKind: 'noneDiff',
            toggleCommentsClasses: 'show can-show',
            rowClasses: '',
            isDocumentation: false,
          },
        ],
      }),
      commentThread: {
        0: [{
          ...defaultCommentThreadProps,
          lineNumber: 3,
          rowOfTokens: [],
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          rowPositionInGroup: 0,
          associatedRowPositionInGroup: 0,
          indent: 1,
          diffKind: 'noneDiff',
          toggleDocumentationClasses: '',
          toggleCommentsClasses: 'show',
          rowClasses: '',
          isDocumentation: false,
          comments: [
            createComment({
              id: 'ai-comment-1',
              apiRevisionId: 'test-revision-id',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText:
                'This class name should follow Azure SDK naming conventions. Consider renaming to align with the style guide.',
              createdBy: 'azure-sdk',
              createdOn: '2025-01-15T10:00:00Z',
              commentSource: 'aiGenerated',
              confidenceScore: 0.92,
            }),
          ],
        }],
      },
    },
  },
};

export const mockCodePanelDataWithMultipleThreads = {
  hasDiff: false,
  nodeMetaData: {
    root: {
      ...defaultNodeMetaData,
      navigationTreeNode: {
        label: 'root',
        data: { nodeIdHashed: 'root' },
        children: [],
      },
      parentNodeIdHashed: null,
      childrenNodeIdsInOrder: { 0: 'node-namespace' },
    },
    'node-namespace': createNodeMeta({
      nodeIdHashed: 'node-namespace',
      label: 'Azure.Storage.Blobs',
      parentNodeIdHashed: 'root',
      codeLines: [
        {
          ...createCodeLine({
            lineNumber: 1,
            rowOfTokens: tokens.namespace,
            nodeId: 'Azure.Storage.Blobs',
            nodeIdHashed: 'node-namespace',
          }),
          diffKind: 'noneDiff',
          rowClasses: '',
          isDocumentation: false,
        },
      ],
      childrenNodeIdsInOrder: { 0: 'node-blobclient' },
    }),
    'node-blobclient': {
      ...createNodeMeta({
        nodeIdHashed: 'node-blobclient',
        label: 'BlobClient',
        parentNodeIdHashed: 'node-namespace',
        codeLines: [
          {
            ...createCodeLine({
              lineNumber: 3,
              rowOfTokens: [
                { value: 'public class ', renderClasses: ['keyword'] },
                { value: 'BlobClient', renderClasses: ['tname'] },
              ],
              nodeId: 'Azure.Storage.Blobs.BlobClient',
              nodeIdHashed: 'node-blobclient',
              indent: 1,
            }),
            diffKind: 'noneDiff',
            toggleCommentsClasses: 'show can-show',
            rowClasses: '',
            isDocumentation: false,
          },
        ],
      }),
      commentThread: {
        0: [{
          ...defaultCommentThreadProps,
          lineNumber: 3,
          rowOfTokens: [],
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          rowPositionInGroup: 0,
          associatedRowPositionInGroup: 0,
          indent: 1,
          diffKind: 'noneDiff',
          toggleDocumentationClasses: '',
          toggleCommentsClasses: 'show',
          rowClasses: '',
          isDocumentation: false,
          comments: [
            createComment({
              id: 'thread1-comment1',
              apiRevisionId: 'test-revision-id',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText: 'First thread: Should this class be sealed?',
              createdBy: 'reviewer1',
              createdOn: '2025-01-15T10:00:00Z',
            }),
          ],
        }],
        1: [{
          ...defaultCommentThreadProps,
          lineNumber: 3,
          rowOfTokens: [],
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          rowPositionInGroup: 1,
          associatedRowPositionInGroup: 0,
          indent: 1,
          diffKind: 'noneDiff',
          toggleDocumentationClasses: '',
          toggleCommentsClasses: 'show',
          rowClasses: '',
          isDocumentation: false,
          comments: [
            createComment({
              id: 'thread2-comment1',
              apiRevisionId: 'test-revision-id',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText:
                'Second thread: Consider adding documentation for this class.',
              createdBy: 'reviewer2',
              createdOn: '2025-01-15T11:00:00Z',
              severity: 1,
            }),
          ],
        }],
      },
    },
  },
};

export const mockCommentsMultipleThreads = [
  {
    id: 'thread1-comment1',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    commentText: 'First thread: Should this class be sealed?',
    createdBy: 'reviewer1',
    createdOn: '2025-01-15T10:00:00Z',
    isResolved: false,
    severity: 2,
  },
  {
    id: 'thread2-comment1',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    commentText: 'Second thread: Consider adding documentation for this class.',
    createdBy: 'reviewer2',
    createdOn: '2025-01-15T11:00:00Z',
    isResolved: false,
    severity: 1,
  },
];

export const mockCodePanelDataWithDiff = {
  diffStyle: 'nodes',
  hasDiff: true,
  nodeMetaData: {
    root: {
      ...defaultNodeMetaData,
      navigationTreeNode: null,
      parentNodeIdHashed: '',
      childrenNodeIdsInOrder: { 0: 'node-namespace-diff' },
      isNodeWithDiff: true,
      isNodeWithDiffInDescendants: true,
      isNodeWithNoneDocDiffInDescendants: true,
    },
    'node-namespace-diff': {
      ...createNodeMeta({
        nodeIdHashed: 'node-namespace-diff',
        label: 'Azure.Storage.Blobs',
        parentNodeIdHashed: 'root',
        codeLines: [
          createCodeLine({
            lineNumber: 1,
            rowOfTokens: tokens.namespace,
            nodeId: 'Azure.Storage.Blobs',
            nodeIdHashed: 'node-namespace-diff',
            diffKind: 'unchanged',
          }),
          createCodeLine({
            lineNumber: 2,
            rowOfTokens: [
              { value: 'public ', renderClasses: ['keyword'] },
              { value: 'void ', renderClasses: ['keyword'] },
              {
                value: 'OldMethod',
                renderClasses: ['method-name', 'diff-change'],
              },
              { value: '()', renderClasses: ['punctuation'] },
            ],
            nodeId: 'Azure.Storage.Blobs.OldMethod',
            nodeIdHashed: 'node-old-method',
            indent: 1,
            diffKind: 'removed',
            rowClasses: ['removed'],
          }),
          createCodeLine({
            lineNumber: 3,
            rowOfTokens: [
              { value: 'public ', renderClasses: ['keyword'] },
              { value: 'void ', renderClasses: ['keyword'] },
              {
                value: 'NewMethod',
                renderClasses: ['method-name', 'diff-change'],
              },
              { value: '(string param)', renderClasses: ['punctuation'] },
            ],
            nodeId: 'Azure.Storage.Blobs.NewMethod',
            nodeIdHashed: 'node-new-method',
            indent: 1,
            diffKind: 'added',
            rowClasses: ['added'],
          }),
          createCodeLine({
            lineNumber: 4,
            rowOfTokens: [
              { value: 'public ', renderClasses: ['keyword'] },
              { value: 'string ', renderClasses: ['keyword'] },
              {
                value: 'DeprecatedProperty',
                renderClasses: ['property-name', 'diff-change'],
              },
              { value: ' { get; }', renderClasses: ['punctuation'] },
            ],
            nodeId: 'Azure.Storage.Blobs.DeprecatedProperty',
            nodeIdHashed: 'node-deprecated-prop',
            indent: 1,
            diffKind: 'removed',
            rowClasses: ['removed'],
          }),
          createCodeLine({
            lineNumber: 5,
            rowOfTokens: [
              { value: 'public ', renderClasses: ['keyword'] },
              { value: 'string ', renderClasses: ['keyword'] },
              {
                value: 'NewProperty',
                renderClasses: ['property-name', 'diff-change'],
              },
              { value: ' { get; set; }', renderClasses: ['punctuation'] },
            ],
            nodeId: 'Azure.Storage.Blobs.NewProperty',
            nodeIdHashed: 'node-new-prop',
            indent: 1,
            diffKind: 'added',
            rowClasses: ['added'],
          }),
          createCodeLine({
            lineNumber: 6,
            rowOfTokens: [
              { value: 'public ', renderClasses: ['keyword'] },
              { value: 'void ', renderClasses: ['keyword'] },
              { value: 'ExistingMethod', renderClasses: ['method-name'] },
              { value: '()', renderClasses: ['punctuation'] },
            ],
            nodeId: 'Azure.Storage.Blobs.ExistingMethod',
            nodeIdHashed: 'node-existing-method',
            indent: 1,
            diffKind: 'unchanged',
          }),
        ],
      }),
      isNodeWithDiff: true,
      isNodeWithDiffInDescendants: true,
      isNodeWithNoneDocDiffInDescendants: true,
    },
  },
  navigation: [
    {
      label: 'Azure.Storage.Blobs',
      data: { nodeIdHashed: 'node-namespace-diff' },
      children: [],
      visible: true,
    },
  ],
};

export function generateLargeCodePanelData(lineCount: number = 100) {
  const codeLines = Array.from({ length: lineCount }, (_, i) =>
    createCodeLine({
      lineNumber: i + 1,
      rowOfTokens: [
        { value: 'public ', renderClasses: ['keyword'] },
        { value: 'void ', renderClasses: ['keyword'] },
        { value: `Method${i}`, renderClasses: ['method-name'] },
        { value: '() { }', renderClasses: ['punctuation'] },
      ],
      nodeId: 'LargeClass',
      nodeIdHashed: 'node-large-class',
      rowPositionInGroup: i,
      indent: 2,
      diffKind: 'noneDiff',
      toggleCommentsClasses: 'bi bi-chat-right-text can-show',
    })
  );

  return {
    diffStyle: 'nodes',
    hasDiff: false,
    nodeMetaData: {
      root: {
        ...defaultNodeMetaData,
        navigationTreeNode: null,
        parentNodeIdHashed: '',
        childrenNodeIdsInOrder: { 0: 'node-large-class' },
      },
      'node-large-class': {
        ...createNodeMeta({
          nodeIdHashed: 'node-large-class',
          label: 'LargeClass',
          parentNodeIdHashed: 'root',
          codeLines,
        }),
      },
    },
    navigation: [
      {
        label: 'LargeClass',
        data: { nodeIdHashed: 'node-large-class' },
        children: [],
        visible: true,
      },
    ],
  };
}

export const mockCommentsForNavigation = [
  {
    id: 'nav-comment-1',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient',
    commentText: 'First comment on BlobClient class - navigation test',
    createdOn: '2025-01-15T10:00:00Z',
    createdBy: 'reviewer1@microsoft.com',
    isResolved: false,
    upvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
    severity: 2,
  },
  {
    id: 'nav-comment-2',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient.Upload',
    commentText: 'Second comment on Upload method - navigation test',
    createdOn: '2025-01-15T11:00:00Z',
    createdBy: 'reviewer2@microsoft.com',
    isResolved: false,
    upvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
    severity: 1,
  },
  {
    id: 'nav-comment-3',
    reviewId: 'test-review-id',
    elementId: 'Azure.Storage.Blobs.BlobClient.Download',
    commentText: 'Third comment on Download method - navigation test',
    createdOn: '2025-01-15T12:00:00Z',
    createdBy: 'reviewer3@microsoft.com',
    isResolved: false,
    upvotes: [],
    taggedUsers: [],
    commentType: 1,
    resolutionLocked: false,
    severity: 3,
  },
];

export const mockCodePanelDataForNavigation = {
  hasDiff: false,
  hasHiddenAPIThatIsDiff: false,
  navigationTreeNodes: [
    {
      label: 'Azure.Storage.Blobs',
      data: { nodeIdHashed: 'node-namespace' },
      expanded: true,
      visible: true,
      children: [
        {
          label: 'BlobClient',
          data: { nodeIdHashed: 'node-blobclient' },
          expanded: true,
          visible: true,
          children: [
            {
              label: 'Upload',
              data: { nodeIdHashed: 'node-upload' },
              children: [],
              visible: true,
            },
            {
              label: 'Download',
              data: { nodeIdHashed: 'node-download' },
              children: [],
              visible: true,
            },
          ],
        },
      ],
    },
  ],
  nodeMetaData: {
    root: {
      ...defaultNodeMetaData,
      navigationTreeNode: null,
      parentNodeIdHashed: '',
      childrenNodeIdsInOrder: { 0: 'node-namespace' },
    },
    'node-namespace': createNodeMeta({
      nodeIdHashed: 'node-namespace',
      label: 'Azure.Storage.Blobs',
      parentNodeIdHashed: 'root',
      codeLines: [
        createCodeLine({
          lineNumber: 1,
          rowOfTokens: tokens.namespace,
          nodeId: 'Azure.Storage.Blobs',
          nodeIdHashed: 'node-namespace',
        }),
      ],
      childrenNodeIdsInOrder: { 0: 'node-blobclient' },
    }),
    'node-blobclient': {
      ...createNodeMeta({
        nodeIdHashed: 'node-blobclient',
        label: 'BlobClient',
        parentNodeIdHashed: 'node-namespace',
        codeLines: [
          createCodeLine({
            lineNumber: 2,
            rowOfTokens: [
              { value: 'public class ', renderClasses: ['keyword'] },
              { value: 'BlobClient', renderClasses: ['class-name'] },
            ],
            nodeId: 'Azure.Storage.Blobs.BlobClient',
            nodeIdHashed: 'node-blobclient',
            indent: 1,
            toggleCommentsClasses: 'toggle-row-comments-icon',
          }),
        ],
        childrenNodeIdsInOrder: { 0: 'node-upload', 1: 'node-download' },
      }),
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient',
          nodeIdHashed: 'node-blobclient',
          comments: [
            createComment({
              id: 'nav-comment-1',
              elementId: 'Azure.Storage.Blobs.BlobClient',
              commentText:
                'First comment on BlobClient class - navigation test',
              createdBy: 'reviewer1',
              createdOn: '2025-01-15T10:00:00Z',
            }),
          ],
        })],
      },
    },
    'node-upload': {
      ...createNodeMeta({
        nodeIdHashed: 'node-upload',
        label: 'Upload',
        parentNodeIdHashed: 'node-blobclient',
        codeLines: [
          createCodeLine({
            lineNumber: 3,
            rowOfTokens: tokens.publicMethod('Upload', '(string path)'),
            nodeId: 'Azure.Storage.Blobs.BlobClient.Upload',
            nodeIdHashed: 'node-upload',
            indent: 2,
            toggleCommentsClasses: 'toggle-row-comments-icon',
          }),
        ],
      }),
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient.Upload',
          nodeIdHashed: 'node-upload',
          comments: [
            createComment({
              id: 'nav-comment-2',
              elementId: 'Azure.Storage.Blobs.BlobClient.Upload',
              commentText: 'Second comment on Upload method - navigation test',
              createdBy: 'reviewer2',
              createdOn: '2025-01-15T11:00:00Z',
              severity: 1,
            }),
          ],
        })],
      },
    },
    'node-download': {
      ...createNodeMeta({
        nodeIdHashed: 'node-download',
        label: 'Download',
        parentNodeIdHashed: 'node-blobclient',
        codeLines: [
          createCodeLine({
            lineNumber: 4,
            rowOfTokens: tokens.publicMethod('Download', '(string path)'),
            nodeId: 'Azure.Storage.Blobs.BlobClient.Download',
            nodeIdHashed: 'node-download',
            indent: 2,
            toggleCommentsClasses: 'toggle-row-comments-icon',
          }),
        ],
      }),
      commentThread: {
        0: [createCommentThread({
          nodeId: 'Azure.Storage.Blobs.BlobClient.Download',
          nodeIdHashed: 'node-download',
          comments: [
            createComment({
              id: 'nav-comment-3',
              elementId: 'Azure.Storage.Blobs.BlobClient.Download',
              commentText: 'Third comment on Download method - navigation test',
              createdBy: 'reviewer3',
              createdOn: '2025-01-15T12:00:00Z',
              severity: 3,
            }),
          ],
        })],
      },
    },
  },
};
