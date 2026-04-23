export enum NamespaceDecisionStatus {
  Proposed = 'Proposed',
  Approved = 'Approved',
  Rejected = 'Rejected',
  Withdrawn = 'Withdrawn'
}

export interface NamespaceDecisionEntry {
  language: string;
  packageName?: string | null;
  namespace: string;
  status: NamespaceDecisionStatus;
  notes?: string | null;
  proposedBy: string;
  proposedOn: string | null;
  decidedBy?: string | null;
  decidedOn: string | null;
}

export interface ProjectNamespaceInfo {
  approvedNamespaces: NamespaceDecisionEntry[];
  namespaceHistory: { [language: string]: NamespaceDecisionEntry[] };
  currentNamespaceStatus: { [language: string]: NamespaceDecisionEntry[] };
}
