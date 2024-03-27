import { Revision } from "./revision"

export enum DiffLineKind {
    Added,
    Removed,
    Unchanged
}

export enum FirstReleaseApproval {
  Approved,
  Pending,
  All
}

export interface Review {
  id: string
  packageName: string
  language: string
  lastUpdatedOn: string,
  isDeleted: boolean,
  isApproved: boolean
}

export interface ChangeHistory {
  changeAction: string
  user: string
  changeDateTime: string
  notes: any
}

export interface SelectItemModel {
  label: string,
  data: string
}
  