export interface ReviewList {
    TotalNumberOfReviews: number
    Reviews: Review[]
}

export interface Review {
    id: string
    name: string
    author: string
    language: string
    noOfRevisions: number
    isClosed: boolean
    isAutomatic: boolean
    filterType: number
    serviceName: string
    packageDisplayName: string
    subscribers: string[]
    lastUpdated: Date
}
  