export class APICodeFileModel {
    fileId: string;
    name: string;
    versionString: string;
    parserStyle: string;
    languageVariant: string;
    hasOriginal: boolean;
    creationDate: Date;
    runAnalysis: boolean;
    packageName: string;
    fileName: string;
    packageVersion: string;
    crossLanguagePackageId: string;

    constructor() {
        this.fileId = '';
        this.name = '';
        this.versionString = '';
        this.parserStyle = '';
        this.languageVariant = '';
        this.hasOriginal = false;
        this.creationDate = new Date();
        this.runAnalysis = false;
        this.packageName = '';
        this.fileName  = '';
        this.packageVersion = '';
        this.crossLanguagePackageId = '';
    }
}