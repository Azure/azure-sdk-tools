import type { Language, TypeChecker } from "./types.ts";
import { typecheckTypeScript } from "./typescript/typecheck.ts";
import { typecheckPython } from "./python/typecheck.ts";
import { typecheckGo } from "./go/typecheck.ts";

export const languages = [
    "TypeScript",
    "Python",
    "Java",
    "C++",
    "C#",
    "Go",
    "Rust",
    "curl",
] as const;

export function getToolNames(language: Language): string[] {
    switch (language.toLowerCase()) {
        case "typescript":
            return ["typescript", "typescript_typecheck"];
        case "python":
            return ["python", "python_typecheck"];
        case "java":
            return ["java", "java_typecheck"];
        case "c++":
            return ["cpp", "cpp_typecheck"];
        case "c#":
            return ["csharp", "csharp_typecheck"];
        case "go":
            return ["go", "go_typecheck"];
        case "rust":
            return ["rust", "rust_typecheck"];
        case "curl":
            return [];
        default:
            throw new Error(`Unsupported language: ${language}`);
    }
}

export function getFileExtension(language: Language): string {
    switch (language.toLowerCase()) {
        case "typescript":
            return "ts";
        case "python":
            return "py";
        case "java":
            return "java";
        case "c++":
            return "cpp";
        case "c#":
            return "cs";
        case "go":
            return "go";
        case "rust":
            return "rs";
        case "curl":
            return "sh";
        default:
            throw new Error(`Unsupported language: ${language}`);
    }
}

export function getTypechecker(language: Language): TypeChecker {
    switch (language.toLowerCase()) {
        case "typescript":
            return typecheckTypeScript;
        case "python":
            return typecheckPython;
        case "go":
            return typecheckGo;
        case "curl":
            return async () => {
                return { succeeded: true, output: "" };
            };
        default:
            throw new Error(`Unsupported language: ${language}`);
    }
}
