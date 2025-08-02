import type { languages } from "./languages.ts";

export interface Sample {
    fileName: string;
    content: string;
    language: Language;
    executable?: boolean;
}

export interface SampleIdea {
    name: string;
    description: string;
    fileName: string;
    requests: Array<{
        path: string;
        description: string;
        method: string;
        queryParams?: Array<{
            name: string;
            value: string;
        }>;
        headers?: Array<{
            name: string;
            value: string;
        }>;
        body?: string;
    }>;
    prerequisites?: {
        setup?: string;
        additionalResources?: Array<{
            type: string;
        }>;
    };
}

export type Language = (typeof languages)[number];

export interface TypeCheckParameters {
    code: string;
    clientDist?: string;
    pkgName?: string;
}

export interface TypeCheckResult {
    output: string;
    succeeded: boolean;
}

export type TypeChecker = (
    inputs: TypeCheckParameters,
) => Promise<TypeCheckResult>;
