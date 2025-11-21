import { enumTokenGenerator } from "./enum";
import { TokenGenerator } from "./interfaces";

export const generators: TokenGenerator[] = [
    enumTokenGenerator
];