import { UserPreferenceModel } from "./userPreferenceModel";
import { EffectivePermissions } from "./permissions";

export class UserProfile {
    userName: string
    email: string
    languages: string[]
    preferences : UserPreferenceModel
    permissions: EffectivePermissions | null

    constructor() {
        this.userName = '';
        this.email = '';
        this.languages = [];
        this.preferences = new UserPreferenceModel();
        this.permissions = null;
    }
}