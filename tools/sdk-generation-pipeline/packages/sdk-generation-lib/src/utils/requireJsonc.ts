import * as fs from 'fs';
import { parse } from 'jsonc-parser';

export const requireJsonc = (path: string) => {
    const contentStr = fs.readFileSync(path).toString();
    const content = parse(contentStr);
    return content;
};
