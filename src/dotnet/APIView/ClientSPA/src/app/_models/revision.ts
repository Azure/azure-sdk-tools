import { ChangeHistory } from "./review"

export interface Revision {
  id: string
  reviewId: string
  packageName: string
  language: string
  files: File[]
  label: any
  changeHistory: ChangeHistory[]
  reviewRevisionType: string
  status: string
  isDeleted: boolean
}
  
  