export interface Review {
    id: string
    author: string
    language: string
    noOfRevisions: number
    status: string;
    type: string;
    state: string;
    displayName: string
    lastUpdated: Date
}


export interface ReviewContent {
    navigation: NavigationItem[]
    codeLines: CodeLine[]
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
  
  