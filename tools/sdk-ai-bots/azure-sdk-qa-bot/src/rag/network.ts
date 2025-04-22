import axios from "axios";
const MY_DEBUG = false;

export interface RAGOptions {
    endpoint: string;
    apiKey: string;
    tenantId: string;
}

interface RAGReference {
    title: string;
    source: string;
    link: string;
    content: string;
}

export interface RAGReply {
    answer: string;
    has_result: boolean;
    references: RAGReference[];
}

export async function getRAGReply(
    question: string,
    options: RAGOptions
): Promise<RAGReply> {
    if (MY_DEBUG)
        return {
            answer: `[DEBUG1] Echo: ${question}`,
            has_result: true,
            references: [],
        };

    const response = await axios.post(
        options.endpoint,
        {
            // TODO: move to config
            tenant_id: options.tenantId,
            message: {
                // TODO: move to config
                role: "user",
                content: question,
            },
        },
        {
            headers: {
                "X-API-Key": options.apiKey,
                "Content-Type": "application/json; charset=utf-8",
            },
        }
    );

    if (response.status !== 200) {
        throw new Error(
            `Failed to fetch data from RAG backend. Status: ${response.status}`
        );
    }
    return response.data;
}
