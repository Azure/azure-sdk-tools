import { UserPreferenceModel } from "./userPreferenceModel";

export class UserProfile {
    userName: string
    email: string
    languages: string[]
    preferences : UserPreferenceModel

    constructor() {
        this.userName = '';
        this.email = '';
        this.languages = [];
        this.preferences = new UserPreferenceModel();
    }
}