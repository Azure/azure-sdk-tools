import { Revision } from "./revision"

export enum DiffLineKind {
    Added,
    Removed,
    Unchanged
}

export interface Review {
  id: string
  packageName: string
  packageDisplayName: any
  serviceName: any
  language: string
  reviewRevisions: string[]
  subscribers: any[]
  changeHistory: ChangeHistory[]
  state: string
  status: string
  isDeleted: boolean
}

export interface ChangeHistory {
  changeAction: string
  user: string
  changeDateTime: string
  notes: any
}

export interface ReviewContent {
  review: Review
  navigation: NavigationItem[]
  codeLines: ReviewLine[]
  reviewRevisions: Map<string, Revision[]>
  activeRevision: Revision
}
  
export interface NavigationItem {
  text: string
  navigationId: string
  childItems: NavigationItem[]
  tags: Map<string, string>
  isHiddenApi: boolean
}

export interface CodeLine {
  displayString: string
  elementId?: string
  lineClass: string
  lineNumber: number
  sectionKey: any
  indent: number
  isDocumentation: boolean
  nodeRef: any
  isHiddenApi: boolean
}

export interface ReviewLine {
  codeLine: CodeLine
  diagnostics: any[]
  commentThread: any
  kind: DiffLineKind
  lineNumber: number
  documentedByLines: any[]
  isDiffView: boolean
  diffSectionId: any
  otherLineSectionKey: any
  headingsOfSectionsWithDiff: any[]
  isSubHeadingWithDiffInSection: boolean
}
  
  