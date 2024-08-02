export class CodeDiagnostic {
    diagnosticId: string = '';
    text: string = '';
    helpLinkUri: string = '';
    targetId: string = '';
    level: string = '';

    constructor() {
        this.diagnosticId = '';
        this.text = '';
        this.helpLinkUri = '';
        this.targetId = '';
        this.level = '';
    }
}