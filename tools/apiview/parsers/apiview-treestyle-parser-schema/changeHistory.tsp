namespace ApiView;

enum AICommentChangeAction {
    Created,
    Deleted,
    Modified
}

enum ReviewChangeAction {
    Created,
    Closed,
    ReOpened,
    Approved,
    ApprovalReverted,
    Deleted,
    UnDeleted
}

/** C#: APIRevisionChangeAction */
enum ApiRevisionChangeAction {
    Created,
    Approved,
    ApprovalReverted,
    Deleted,
    UnDeleted
}

enum CommentChangeAction {
    Created,
    Edited,
    Resolved,
    UnResolved,
    Deleted,
    UnDeleted
}

model ChangeHistoryModel {
    ChangedBy: string;
    ChangedOn?: utcDateTime;
    Notes: string;
}

/** C#: AICommentChangeHistoryModel */
model AICommentChangeHistory extends ChangeHistoryModel {
    ChangeAction: AICommentChangeAction;
}

/** C#: ReviewChangeHistoryModel */
model ReviewChangeHistory extends ChangeHistoryModel {
    ChangeAction: ReviewChangeAction;
}

/** C#: APIRevisionChangeHistoryModel */
model ApiRevisionChangeHistory extends ChangeHistoryModel {
    ChangeAction: ApiRevisionChangeAction;
}

/** C#: CommentChangeHistoryModel */
model CommentChangeHistory extends ChangeHistoryModel {
    ChangeAction: CommentChangeAction;
}
