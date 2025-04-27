import axios from "axios";
import config from "../config";

export enum FeedbackReaction {
    good,
    bad,
}

export async function sendFeedback(
    conversation: string[],
    reaction: FeedbackReaction
) {
    const response = await axios.post(
        // TODO: make /feedback endpoint configurable
        config.azureOpenAIEndpoint + "/feedback",
        {
            tenant_id: config.azureOpenAIDeploymentName,
            messages: conversation.map((con) => ({
                role: "user",
                content: con,
            })),
            reaction: reaction.toString(),
        },
        {
            headers: {
                "X-API-Key": config.azureOpenAIKey,
                "Content-Type": "application/json; charset=utf-8",
            },
        }
    );

    if (response.status !== 200) {
        throw new Error(
            `Failed to fetch data from feedback backend. Status: ${response.status}`
        );
    }
    return response.data;
}
