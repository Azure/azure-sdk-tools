import { ChangeHistory } from "./changeHistory"

export enum FirstReleaseApproval {
  Approved,
  Pending,
  All
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

  constructor() {
    this.id = ''
    this.packageName = ''
    this.language = ''
    this.lastUpdatedOn = ''
    this.isDeleted = false
    this.isApproved = false
    this.changeHistory = []
    this.subscribers = []
  }
}

export interface SelectItemModel {
  label: string
  data: string
}