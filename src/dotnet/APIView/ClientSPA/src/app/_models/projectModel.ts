import { ProjectNamespaceInfo } from './namespaceModel';

export interface ProjectChangeHistory {
  changedBy: string;
  changedOn: string;
  notes: string;
  changeAction: ProjectChangeAction;
}

export enum ProjectChangeAction {
  Created = 'Created',
  Edited = 'Edited',
  Deleted = 'Deleted',
  UnDeleted = 'UnDeleted',
  ReviewLinked = 'ReviewLinked',
  ReviewUnlinked = 'ReviewUnlinked',
  NamespaceStatusChanged = 'NamespaceStatusChanged'
}

export interface PackageInfo {
  packageName: string;
  namespace: string;
}

export interface Project {
  id: string;
  crossLanguagePackageId: string;
  displayName: string;
  description: string;
  owners: string[];
  namespace: string;
  expectedPackages: { [language: string]: PackageInfo };
  namespaceInfo: ProjectNamespaceInfo;
  changeHistory: ProjectChangeHistory[];
  reviews: { [language: string]: string };
  historicalReviewIds: string[];
  createdOn: string;
  lastUpdatedOn: string;
  isDeleted: boolean;
}

export interface RelatedReviewItem {
  id: string;
  packageName: string;
  language: string;
  subscribers: string[];
  isClosed: boolean;
  isApproved: boolean;
  packageType: string | null;
  namespaceReviewStatus: string;
  createdBy: string;
  createdOn: string;
  lastUpdatedOn: string;
  isDeleted: boolean;
  projectId: string;
  crossLanguagePackageId: string;
}

export interface RelatedReviewsResponse {
  currentReviewId: string;
  projectId: string;
  projectName: string;
  crossLanguagePackageId: string;
  reviews: RelatedReviewItem[];
}
