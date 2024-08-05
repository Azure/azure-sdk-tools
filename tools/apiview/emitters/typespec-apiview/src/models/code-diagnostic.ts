import { ApiViewSerializable } from "../interface.js";

export class CodeDiagnostic implements ApiViewSerializable{
  static idCounter: number = 1;
  diagnosticId: number;
  text: string;
  targetId: string;
  level: CodeDiagnosticLevel;
  helpLinkUri?: string;

  constructor(message: string, targetId: string, level: CodeDiagnosticLevel) {
    this.diagnosticId = CodeDiagnostic.idCounter;
    CodeDiagnostic.idCounter++;
    this.text = message;
    this.targetId = targetId;
    this.level = level;
  }

  serialize(): object {
    return {
      DiagnosticId: this.diagnosticId,
      Text: this.text,
      HelpLinkUri: this.helpLinkUri,
      TargetId: this.targetId,
      Level: this.level,
    }
  }
}

export enum CodeDiagnosticLevel {
  Info = 1,
  Warning = 2,
  Error = 3,
  Fatal = 4,
}
