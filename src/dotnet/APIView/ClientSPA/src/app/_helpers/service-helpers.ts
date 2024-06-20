export function mapLanguageAliases(languages: Iterable<string>): string[] {
  const result: Set<string> = new Set<string>();

  for (const language of languages) {
    if (language === "TypeSpec" || language === "Cadl") {
      result.add("Cadl");
      result.add("TypeSpec");
    }
    result.add(language);
  }
  return Array.from(result);
}

