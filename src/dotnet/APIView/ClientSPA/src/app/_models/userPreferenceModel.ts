export class UserPreferenceModel {
    userName : string
    language : string []
    approvedLanguages: string []
    theme : string
    hideReviewPageOptions : boolean
    hideSamplesPageOptions : boolean
    hideLineNumbers : boolean
    hideLeftNavigation: boolean
    showHiddenApis: boolean
    showDocumentation: boolean
    showComments: boolean
    showSystemComments: boolean
    disableCodeLinesLazyLoading: boolean


    constructor() {
        this.userName = '';
        this.language = [];
        this.approvedLanguages = [];
        this.theme = '';
        this.hideReviewPageOptions  = false;
        this.hideSamplesPageOptions  = false;
        this.hideLineNumbers = false;
        this.hideLeftNavigation = false;
        this.showHiddenApis = false
        this.showDocumentation = false;
        this.showComments = true;
        this.showSystemComments = true;
        this.disableCodeLinesLazyLoading = false;
    }
}