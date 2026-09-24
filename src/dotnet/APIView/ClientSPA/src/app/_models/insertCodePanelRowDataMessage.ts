export enum ReviewPageWorkerMessageDirective {
  CreatePageNavigation,
  UpdateCodePanelData,
  UpdateCodePanelRowData,
  SetHasHiddenAPIFlag
}

export interface InsertCodePanelRowDataMessage {
  directive: ReviewPageWorkerMessageDirective
  payload : any
}