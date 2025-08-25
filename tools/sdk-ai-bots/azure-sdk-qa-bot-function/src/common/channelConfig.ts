import * as yaml from "js-yaml";
import { BlobService } from "../services/StorageService";

const BOT_CONFIG_CONTAINER_NAME = "bot-configs";
const BOT_CHANNEL_CONFIG_BLOB_NAME = "channel.yaml";

export interface Channel {
    name: string;
    id: string;
}

export interface ChannelConfig {
    channels: Channel[];
}

export async function loadChannelMapping(
    blobService: BlobService
): Promise<Map<string, string>> {
    const channelMapping = new Map<string, string>();

    try {
        console.log("Loading channel mapping from bot-configs/channel.yaml...");

        // Download blob content
        const downloadResponse = await blobService.downloadBlob(
            BOT_CONFIG_CONTAINER_NAME,
            BOT_CHANNEL_CONFIG_BLOB_NAME
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

        // Parse YAML content
        const channelConfig = yaml.load(yamlContent) as ChannelConfig;

        if (
            !channelConfig ||
            !channelConfig.channels ||
            !Array.isArray(channelConfig.channels)
        ) {
            console.warn(
                "Warning: Invalid channel.yaml format. Expected 'channels' array property."
            );
            return channelMapping;
        }

        // Build mapping from channelId to channelName
        for (const channel of channelConfig.channels) {
            if (channel.id && channel.name) {
                channelMapping.set(channel.id, channel.name);
            }
        }

        console.log(
            `Successfully loaded ${channelMapping.size} channel mappings:"`
        );
        for (const [id, name] of channelMapping) {
            console.log(`  ${id} -> ${name}`);
        }
    } catch (error) {
        console.error("Error loading channel mapping:", error);
        console.warn(
            "Warning: Failed to load channel mapping. Channel names will be empty."
        );
    }

    return channelMapping;
}
