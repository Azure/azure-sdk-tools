
// Mock for Monaco Editor to avoid CSS loading issues in tests and provide Types support

export namespace editor {
  export interface IStandaloneCodeEditor {
    dispose(): void;
    getValue(): string;
    setValue(v: string): void;
    onDidChangeModelContent(cb: any): void;
    getModel(): any;
    layout(): void;
    updateOptions(opts: any): void;
    focus(): void;
  }

  export function create(element: any, options: any): IStandaloneCodeEditor {
    return {
      dispose: () => {},
      getValue: () => '',
      setValue: (v: string) => {},
      onDidChangeModelContent: (cb: any) => {},
      getModel: () => ({}),
      layout: () => {},
      updateOptions: () => {},
      focus: () => {}
    };
  }

  export function setTheme(theme: string) {}
  export function setModelLanguage(model: any, language: string) {}
  export function defineTheme(themeName: string, themeData: any) {}
}

export namespace languages {
  export function register() {}
  export function setMonarchTokensProvider() {}
  export function registerCompletionItemProvider() {}
  export const json = {
    jsonDefaults: {
      setDiagnosticsOptions: () => {}
    }
  };
}

export namespace Uri {
  export function parse(url: string) { return { toString: () => url }; }
}

export enum MarkerSeverity {
  Hint = 1,
  Info = 2,
  Warning = 4,
  Error = 8
}
