import { type Language } from "./types.ts";

export function getTypecheckingPrompt(lang: Language): string {
    return `After you generate the code, call the typechecker tool.  
  If the typechecker reports any errors, you must carefully review the errors,
  update the code to fix **all** type errors, and try again.

  **Repeat this process:**  
  - Generate or revise the ${lang} code.
  - Submit it for typechecking.
  - If there are any type errors, fix them and resubmit.
  - Continue until the code passes typechecking with no errors.

  **Rules:**  
  - Do not ignore or suppress type errors.
  - Do not use any, type assertions, or disable checks to bypass errors.
  - Only stop when the typechecker reports zero errors.

  Respond only with the revised ${lang} code each time, until it passes typechecking.`;
}

export function sampleGuidelines(lang: Language) {
    return `
  The sample must not use API keys unless there is no other way for authentication.
  The sample must use token credential for authentication if the service supports it.
  The sample must use the latest version of the ${lang} language.
  Don't specify the service version in the sample unless it is required.
  The sample must not use any deprecated or obsolete features of the ${lang} language.
  The sample must not use any deprecated or obsolete features of the client library.
  The sample must not use any deprecated or obsolete features of the service.
  The sample must be a complete, self-contained code that can be run as is.
  The sample must be a single file that can be run as is.
  The main function in the sample should be called "main".
  The sample doesn't contain any dead code or unused imports.
  The sample must load resource information from environment variables.
  Every function must be fully typed.

  ${getTypecheckingPrompt(lang)}`;
}
