import * as fs from "fs";
import * as path from "path";
import { logger } from "../../utils/logger.js";

export function updateTspLocation(packageFolderPath: string) {
    logger.info('Start to update tsp-location.yaml');
    const data: string = fs.readFileSync(path.join(packageFolderPath, 'tsp-location.yaml'), 'utf8');
    const result = data.replace(`repo: ../azure-rest-api-specs`, `repo: Azure/azure-rest-api-specs`);
    fs.writeFileSync(path.join(packageFolderPath, 'tsp-location.yaml'), result, 'utf8');
}