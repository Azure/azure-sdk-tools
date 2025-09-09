import * as yaml from "js-yaml";
import { BlobService } from "../StorageService";
import { AnalyticsServiceBase } from "./AnalyticsServiceBase";

interface Channel {
    name: string;
    id: string;
    tenant: string;
    endpoint: string;
}

interface ChannelConfigData {
    channels: Channel[];
}

export class ChannelConfigService extends AnalyticsServiceBase<
    string,
    Channel
> {
    private readonly botConfigContainerName = "bot-configs";
    private readonly channelConfigBlobName = "channel.yaml";
    private blobService = new BlobService();

    protected async getEntities(): Promise<string[]> {
        // Download blob content
        const downloadResponse = await this.blobService.downloadBlob(
            this.botConfigContainerName,
            this.channelConfigBlobName
        );
        if (!downloadResponse.readableStreamBody) {
            throw new Error("Failed to download channel.yaml content");
        }

        // Convert stream to string
        const chunks: Buffer[] = [];
        for await (const chunk of downloadResponse.readableStreamBody) {
            chunks.push(Buffer.from(chunk));
        }
        const yamlContent = Buffer.concat(chunks).toString("utf-8");
        return [yamlContent];
    }

    protected deserializeData(entities: string[]): Channel[] {
        const channels = entities.flatMap((entity) => {
            const channelConfig = yaml.load(entity) as ChannelConfigData;
            return channelConfig.channels;
        });
        return channels;
    }
}
