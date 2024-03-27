export interface AppVersion {
    hash: string
}

export interface LoginStatus {
    isLoggedIn: boolean
}

export interface UserProfile {
    userName: string
    email: string
    languages: string[]
    preferences : UserPreferenceModel
}

export interface UserPreferenceModel {
    userName : string
    language : string []
    theme : string
}