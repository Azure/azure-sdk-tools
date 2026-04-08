export enum ProposalDecision {
  Accept = 'accept',
  AcceptWithModification = 'acceptWithModification',
  Reject = 'reject'
}

export interface ProposalVote {
  language: string;
  decision: ProposalDecision;
  modificationText: string;
  votedBy: string;
  voterRole: string;
  votedOn: string;
}

export interface ProposalComment {
  id: string;
  commentText: string;
  createdBy: string;
  createdOn: string;
  roleOfCreator: string;
}

export interface CrossLanguageProposalModel {
  id: string;
  reviewId: string;
  elementId: string;
  crossLanguageId: string;
  threadId: string;
  proposalText: string;
  description: string;
  createdBy: string;
  createdOn: string;
  roleOfCreator: string;
  isDeleted: boolean;
  isSuperseded: boolean;
  supersededByProposalId: string;
  supersedeReason: string;
  supersededByUser: string;
  supersededOn: string;
  votes: ProposalVote[];
  comments: ProposalComment[];
  type: string;
}
