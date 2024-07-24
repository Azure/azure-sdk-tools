export class UserPreferenceModel {
    userName : string
    language : string []
    theme : string
    hideReviewPageOptions : boolean
    hideLineNumbers : boolean
    hideLeftNavigation: boolean
    showHiddenApis: boolean
    showDocumentation: boolean
    showComments: boolean
    showSystemComments: boolean
    useBetaIndexPage: boolean

    constructor() {
        this.userName = '';
        this.language = []
        this.theme = '';
        this.hideReviewPageOptions  = false;
        this.hideLineNumbers = false;
        this.hideLeftNavigation = false;
        this.showHiddenApis = false
        this.showDocumentation = false;
        this.showComments = false;
        this.showSystemComments = false;
        this.useBetaIndexPage = false;
    }
}