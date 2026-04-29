// Ported from https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/SemVer.ps1
export class AzureEngSemanticVersion {
    private static SEM_VAR_REGEX = /(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?/;

    // Python PEP 440 post-release extension
    // Handles all PEP 440 alternate formats: .postN, -postN, _postN, postN, .post.N, .post (implicit 0) (case-insensitive)
    private static PYTHON_SEM_VAR_REGEX = /(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?(?:(?<postsep>[.\-_]?)(?<postword>[Pp][Oo][Ss][Tt])\.?(?<postnum>\d{1,8})?)?/;

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
    // For Python PEP440 post-release support only
    isPostRelease : boolean = false;
    postReleaseNumber : number = 0;
    postReleaseSeparator : string = '';

    constructor(version: string, language: string) {
        const isPython = language?.toLowerCase() === "python";
        const regex = isPython ? AzureEngSemanticVersion.PYTHON_SEM_VAR_REGEX : AzureEngSemanticVersion.SEM_VAR_REGEX;
        const versionParts = regex.exec(version);
        
        if (versionParts) {
            this.isSemVerFormat = true;
            this.rawVersion = version;

            this.major = parseInt(versionParts.groups!['major']);
            this.minor = parseInt(versionParts.groups!['minor']);
            this.patch = parseInt(versionParts.groups!['patch']);

            let skipPrelabel = false;
            if (isPython) {
                this.setupPythonConventions();
                if (versionParts.groups!['postword']) {
                    this.isPostRelease = true;
                    this.postReleaseNumber = versionParts.groups!['postnum'] ? parseInt(versionParts.groups!['postnum']) : 0;
                    this.postReleaseSeparator = ".post";
                }
                else if (versionParts.groups!['prelabel'] && 
                         versionParts.groups!['prelabel'].toLowerCase() === 'post') {
                    // Alternate PEP 440 forms like "1.0.0-post1" or "1.0.0post1" where the regex
                    // matched "post" as a prerelease label — reinterpret as post-release.
                    this.isPostRelease = true;
                    this.postReleaseNumber = versionParts.groups!['prenumber'] ? parseInt(versionParts.groups!['prenumber']) : 0;
                    this.postReleaseSeparator = ".post";
                    skipPrelabel = true;
                }
            } else {
                this.setupDefaultConventions();
            }

            if (skipPrelabel || !versionParts.groups!['prelabel']) {
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
            } else {
                this.prereleaseLabel = versionParts.groups!['prelabel']
                this.prereleaseLabelSeparator = versionParts.groups!["presep"]
                this.prereleaseNumber = parseInt(versionParts.groups!["prenumber"])
                this.prereleaseNumberSeparator = versionParts.groups!["prenumsep"]
                this.isPrerelease = true
                this.versionType = "Beta"
                this.buildNumberSeparator = versionParts.groups!["buildnumsep"]
                this.buildNumber = versionParts.groups!["buildnumber"] ?? ''
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

        let thisBuildNum = this.buildNumber ? parseInt(this.buildNumber as string) : 0;
        let otherBuildNum = other.buildNumber ? parseInt(other.buildNumber as string) : 0;
        ret = thisBuildNum - otherBuildNum;
        if (ret !== 0) return ret;

        // Post-release versions sort after their base version
        let thisPost = this.isPostRelease ? 1 : 0;
        let otherPost = other.isPostRelease ? 1 : 0;
        ret = thisPost - otherPost;
        if (ret !== 0) return ret;

        return this.postReleaseNumber - other.postReleaseNumber;
    }
}