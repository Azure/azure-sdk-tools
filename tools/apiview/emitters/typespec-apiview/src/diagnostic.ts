export class ApiViewDiagnostic {
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
}

export enum ApiViewDiagnosticLevel {
  Default = 0,
  Info = 1,
  Warning = 2,
  Error = 3,
}
