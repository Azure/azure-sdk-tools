import { ChangeHistory } from "./changeHistory"

export enum FirstReleaseApproval {
  Approved,
  Pending,
  All
}

export enum PackageType {
  Data = 'Data',
  Management = 'Management',
  Unknown = 'Unknown'
}

export class Review {
  id: string
  packageName: string
  language: string
  lastUpdatedOn: string
  isDeleted: boolean
  isApproved: boolean
  changeHistory: ChangeHistory[]
  subscribers: string[]
  namespaceReviewStatus: string
  packageType?: PackageType | null  // Optional - undefined or null if not yet classified

  constructor() {
    this.id = ''
    this.packageName = ''
    this.language = ''
    this.lastUpdatedOn = ''
    this.isDeleted = false
    this.isApproved = false

    this.namespaceReviewStatus = 'NotStarted'
    this.changeHistory = []
    this.subscribers = []
    // Don't set default PackageType - let it be undefined if not provided by backend
  }
}

export interface SelectItemModel {
  label: string
  data: string
}
