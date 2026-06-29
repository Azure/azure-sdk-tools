// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * Fixture: three declare module blocks — two third-party modules and one owned module.
 *
 * Derived from real usage patterns in @azure/ai-projects which depends on both
 * `openai` (for getOpenAIClient) and `@azure/core-paging` (for paged responses).
 *
 * Exercises:
 *  • Parser includes ALL three modules (openai, @azure/core-paging, @azure/ai-projects)
 *  • Cross-module NavigateToId: @azure/ai-projects types link to openai + core-paging types
 *  • Correct per-module LineId prefix for all three modules
 *  • "Declared Modules" list in generateApiViewFromDts output
 *  • Third-party modules appear as full Subpath-export sections alongside owned modules
 */

// ---------------------------------------------------------------------------
// Third-party module: openai
// ---------------------------------------------------------------------------

declare module "openai" {
    export default class OpenAI {
        constructor(opts?: OpenAIOptions);
        /** Access to the Assistants API */
        readonly assistants: AssistantsAPI;
        /** Access to the Chat Completions API */
        readonly chat: ChatAPI;
    }

    export interface OpenAIOptions {
        apiKey?: string;
        baseURL?: string;
        maxRetries?: number;
        defaultHeaders?: Record<string, string>;
    }

    export interface AssistantsAPI {
        create(body: AssistantCreateParams): Promise<Assistant>;
        retrieve(assistantId: string): Promise<Assistant>;
        list(): AssistantsPage;
    }

    export interface ChatAPI {
        completions: ChatCompletionsAPI;
    }

    export interface ChatCompletionsAPI {
        create(body: ChatCompletionCreateParams): Promise<ChatCompletion>;
    }

    export interface Assistant {
        id: string;
        name: string | null;
        model: string;
        instructions: string | null;
    }

    export interface AssistantCreateParams {
        model: string;
        name?: string;
        instructions?: string;
    }

    export interface ChatCompletion {
        id: string;
        choices: ChatCompletionChoice[];
        model: string;
    }

    export interface ChatCompletionCreateParams {
        model: string;
        messages: ChatCompletionMessageParam[];
    }

    export interface ChatCompletionChoice {
        message: ChatCompletionMessage;
        finish_reason: "stop" | "length" | "tool_calls" | "content_filter";
        index: number;
    }

    export interface ChatCompletionMessage {
        role: "assistant";
        content: string | null;
    }

    export interface ChatCompletionMessageParam {
        role: "user" | "system" | "assistant";
        content: string;
    }

    export interface AssistantsPage {
        data: Assistant[];
        has_more: boolean;
        first_id: string | null;
        last_id: string | null;
    }
}

// ---------------------------------------------------------------------------
// Third-party module: @azure/core-paging
// ---------------------------------------------------------------------------

declare module "@azure/core-paging" {
    export interface PagedAsyncIterableIterator<
        TElement,
        TPage = TElement[],
        TPageSettings = PageSettings
    > {
        next(): Promise<IteratorResult<TElement>>;
        [Symbol.asyncIterator](): PagedAsyncIterableIterator<TElement, TPage, TPageSettings>;
        byPage(settings?: TPageSettings): AsyncIterableIterator<TPage>;
    }

    export interface PageSettings {
        continuationToken?: string;
        maxPageSize?: number;
    }
}

// ---------------------------------------------------------------------------
// Owned module: @azure/ai-projects
// ---------------------------------------------------------------------------

declare module "@azure/ai-projects" {
    import type OpenAI from "openai";
    import type { PagedAsyncIterableIterator } from "@azure/core-paging";

    /**
     * Client for Azure AI Projects.
     * @beta
     */
    export class AIProjectsClient {
        constructor(endpoint: string, credential: TokenCredential);
        /** Returns an authenticated OpenAI client scoped to this project */
        getOpenAIClient(options?: GetOpenAIClientOptions): OpenAI;
        /** Access to the Agents sub-client */
        readonly agents: AgentsOperations;
    }

    export interface GetOpenAIClientOptions {
        /** Name of the AI Services connection to use */
        connectionName?: string;
        /** API version override */
        apiVersion?: string;
    }

    export interface AgentsOperations {
        /** Lists all agents in the project */
        listAgents(options?: AgentListOptions): PagedAsyncIterableIterator<Agent>;
        /** Creates a new agent */
        createAgent(body: AgentCreateParams): Promise<Agent>;
        /** Retrieves an agent by ID */
        getAgent(agentId: string): Promise<Agent>;
        /** Deletes an agent */
        deleteAgent(agentId: string): Promise<void>;
    }

    export interface Agent {
        id: string;
        name: string;
        model: string;
        instructions?: string;
        /** @deprecated Use `instructions` instead */
        systemPrompt?: string;
    }

    export interface AgentCreateParams {
        model: string;
        name: string;
        instructions?: string;
    }

    export interface AgentListOptions {
        maxPageSize?: number;
        continuationToken?: string;
    }

    export interface TokenCredential {
        getToken(scopes: string | string[], options?: GetTokenOptions): Promise<AccessToken | null>;
    }

    export interface GetTokenOptions {
        abortSignal?: AbortSignal;
    }

    export interface AccessToken {
        token: string;
        expiresOnTimestamp: number;
    }
}
