import { ApiViewSerializable } from "./apiview.js";

export enum ApiViewDiagnosticLevel {
  Default = 0,
  Info = 1,
  Warning = 2,
  Error = 3,
}

export class ApiViewDiagnostic implements ApiViewSerializable {
  static idCounter: number = 1;
  diagnosticNumber: number;
  text: string;
  targetId: string;
  level: ApiViewDiagnosticLevel;
  helpLinkUri?: string;

  constructor(message: string, targetId: string, level: ApiViewDiagnosticLevel) {
    this.diagnosticNumber = ApiViewDiagnostic.idCounter;
    ApiViewDiagnostic.idCounter++;
    this.text = message;
    this.targetId = targetId;
    this.level = level;
  }

  static fromJSON(json: any): ApiViewDiagnostic {
    return new ApiViewDiagnostic(json.Text, json.TargetId, json.Level);
  }

  toJSON(abbreviate: boolean): object {
    return {
      Text: this.text,
      TargetId: this.targetId,
      Level: this.level.valueOf(),
      HelpLinkUri: this.helpLinkUri,
    };
  }

  toText(): string {
    return this.text;
  }
}
