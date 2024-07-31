// Ported from https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/SemVer.ps1
export class AzureEngSemanticVersion {
    private static SEM_VAR_REGEX = /(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?/;

    major : number = 0;
    minor : number = 0;
    patch : number = 0
    prereleaseLabelSeparator : string = '';
    prereleaseLabel : string = ''
    prereleaseNumberSeparator : string = '';
    buildNumberSeparator : string = '';
    buildNumber : string = '';
    prereleaseNumber : number = 0;
    isPrerelease : boolean = false;
    versionType : string = '';
    rawVersion : string;
    isSemVerFormat : boolean;
    defaultPrereleaseLabel : string = '';
    defaultAlphaReleaseLabel : string = '';

    constructor(version: string, language: string) {
        const versionParts = AzureEngSemanticVersion.SEM_VAR_REGEX.exec(version);
        
        if (versionParts) {
            this.isSemVerFormat = true;
            this.rawVersion = version;

            this.major = parseInt(versionParts.groups!['major']);
            this.minor = parseInt(versionParts.groups!['minor']);
            this.patch = parseInt(versionParts.groups!['patch']);

            if (language === "Python") {
                this.setupPythonConventions();

            } else {
                this.setupDefaultConventions();
            }

            if (versionParts.groups!['prelabel']) {
                this.prereleaseLabel = versionParts.groups!['prelabel']
                this.prereleaseLabelSeparator = versionParts.groups!["presep"]
                this.prereleaseNumber = versionParts.groups!["prenumber"] as unknown as number
                this.prereleaseNumberSeparator = versionParts.groups!["prenumsep"]
                this.isPrerelease = true
                this.versionType = "Beta"
                this.buildNumberSeparator = versionParts.groups!["buildnumsep"]
                this.buildNumber = versionParts.groups!["buildnumber"] ?? ''
            } else {
                // artifically provide these values for non-prereleases to enable easy sorting of them later than prereleases.
                this.prereleaseLabel = "zzz"
                this.prereleaseNumber = 99999999
                this.isPrerelease = false
                this.versionType = "GA"
                if (this.major == 0) {
                   // Treat initial 0 versions as a prerelease beta's
                  this.versionType = "Beta"
                  this.isPrerelease = true
                }
                else if (this.patch !== 0) {
                  this.versionType = "Patch"
                }
            }
        } else {
            this.rawVersion = version
            this.isSemVerFormat = false
        }
    }

    private setupPythonConventions() : void {
        this.prereleaseLabelSeparator = this.prereleaseNumberSeparator = this.buildNumberSeparator = ""
        this.defaultPrereleaseLabel = "b"
        this.defaultAlphaReleaseLabel = "a"
    }

    private setupDefaultConventions() : void {
        this.prereleaseLabelSeparator = "-"
        this.prereleaseNumberSeparator = "."
        this.buildNumberSeparator = "."
        this.defaultPrereleaseLabel = "beta"
        this.defaultAlphaReleaseLabel = "alpha"
    }

    compareTo(other: AzureEngSemanticVersion): number {
        if (!(other instanceof AzureEngSemanticVersion)) {
            throw new Error(`Cannot compare ${other} with ${this}`);
        }

        let ret = this.major - other.major;
        if (ret !== 0) return ret;

        ret = this.minor - other.minor;
        if (ret !== 0) return ret;

        ret = this.patch - other.patch;
        if (ret !== 0) return ret;

        ret = this.prereleaseLabel.localeCompare(other.prereleaseLabel, undefined, { sensitivity: 'base' });
        if (ret !== 0) return ret;

        ret = this.prereleaseNumber - other.prereleaseNumber;
        if (ret !== 0) return ret;

        return (this.buildNumber as unknown as number) - (other.buildNumber as unknown as number);
    }
}