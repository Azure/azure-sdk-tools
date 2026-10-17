export class SamplesRevision {
    id: string
    reviewId: string
    fileId: string
    originalFileId: string
    originalFileName?: string
    createdBy: string
    createdOn: Date
    title: string
    isDeleted: boolean
    apiVersionId?: string | null

    constructor() {
        this.id = ''
        this.reviewId = ''
        this.fileId = ''
        this.originalFileId = ''
        this.originalFileName = ''
        this.createdBy = ''
        this.createdOn = new Date()
        this.title = ''
        this.isDeleted = false
    }
}