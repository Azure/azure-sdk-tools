export interface Review {
    id: string
    name: string
    author: string
    language: string
    noOfRevisions: number
    status: string;
    type: string;
    state: string;
    serviceName: string
    packageDisplayName: string
    subscribers: string[]
    lastUpdated: Date
}
  