export class CodeDiagnostic {
    diagnosticId: string = '';
    text: string = '';
    helpLinkUri: string = '';
    targetId: string = '';
    level: string = '';

    constructor(
        diagnosticId: string = '',
        text: string = '',
        helpLinkUri: string = '',
        targetId: string = '',
        level: string = ''
    ) {
        this.diagnosticId = diagnosticId;
        this.text = text;
        this.helpLinkUri = helpLinkUri;
        this.targetId = targetId;
        this.level = level;
    }
}