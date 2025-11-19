import { enumTokenGenerator } from "./enum";
import { ITokenGenerator } from "./interfaces";

export const generators: ITokenGenerator[] = [
    enumTokenGenerator
];