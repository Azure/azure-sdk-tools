export function getLanguageCssSafeName(language: string): string {
  switch (language.toLowerCase()) {
      case "c#":
          return "csharp";
      case "c++":
          return "cplusplus";
      default:
          return language.toLowerCase();
  }
}