/// <reference lib="es2020" />
/// <reference lib="dom" />

/**
 * Representative fixture for @azure/ai-projects.
 *
 * Derived from sdk/ai/ai-projects/review/ai-projects-node.api.md in
 * Azure/azure-sdk-for-js (PR #38143). Structured as two declare module blocks
 * to simulate an extract-api-v2 output for a package with named subpath exports
 * – @azure/ai-projects (main client + operations) and @azure/ai-projects/models
 * (all data-model types).
 *
 * This fixture exercises:
 *  - Multiple declare module blocks in one file
 *  - Cross-module NavigateToId references
 *  - Class with @beta-tagged property
 *  - Overloaded interface methods
 *  - PagedAsyncIterableIterator<T> generic return types
 *  - Discriminated union types
 *  - Long string-literal union (AttackStrategy)
 *  - @deprecated type
 *  - Complex extends hierarchies (InsightRequest, InsightResult, BaseCredentials)
 *  - Interface with optional/readonly members
 */

/* ============================================================
   @azure/ai-projects  –  main entry point
   ============================================================ */

declare module "@azure/ai-projects" {
    import type { PagedAsyncIterableIterator } from "@azure/core-paging";
    import type { TokenCredential } from "@azure/core-auth";

    // ── Client class ──────────────────────────────────────────────────────

    /** Azure AI Projects client. Provides access to agents, connections, datasets,
     * indexes, deployments, evaluation rules, and (beta) advanced operations. */
    export class AIProjectClient {
        constructor(endpoint: string, credential: TokenCredential, options?: AIProjectClientOptionalParams);
        readonly agents: AgentsOperations;
        readonly connections: ConnectionsOperations;
        readonly datasets: DatasetsOperations;
        readonly indexes: IndexesOperations;
        readonly deployments: DeploymentsOperations;
        readonly evaluationRules: EvaluationRulesOperations;
        /** @beta */
        readonly beta: BetaOperations;
    }

    // ── Client optional params ────────────────────────────────────────────

    export interface AIProjectClientOptionalParams {
        apiVersion?: string;
        userAgentOptions?: UserAgentPolicyOptions;
    }

    export interface UserAgentPolicyOptions {
        userAgentPrefix?: string;
    }

    // ── Agents operations ─────────────────────────────────────────────────

    /** Operations for managing AI agents. */
    export interface AgentsOperations {
        /** Creates a new agent from a definition. */
        create(name: string, definition: AgentDefinitionUnion, options?: AgentsCreateOptionalParams): Promise<Agent>;
        /** Creates a new agent from a manifest. */
        create(name: string, manifestId: string, parameterValues: Record<string, unknown>, options?: AgentsCreateAgentFromManifestOptionalParams): Promise<Agent>;
        /** Creates a new agent version from a definition. */
        createVersion(agentName: string, definition: AgentDefinitionUnion, options?: AgentsCreateVersionOptionalParams): Promise<AgentVersion>;
        /** Creates a new agent version from a manifest. */
        createVersion(agentName: string, manifestId: string, parameterValues: Record<string, unknown>, options?: AgentsCreateVersionOptionalParams): Promise<AgentVersion>;
        delete(agentName: string, options?: AgentsDeleteOptionalParams): Promise<DeleteAgentResponse>;
        deleteVersion(agentName: string, agentVersion: string, options?: AgentsDeleteVersionOptionalParams): Promise<DeleteAgentVersionResponse>;
        get(agentName: string, options?: AgentsGetOptionalParams): Promise<Agent>;
        getVersion(agentName: string, agentVersion: string, options?: AgentsGetVersionOptionalParams): Promise<AgentVersion>;
        list(options?: AgentsListOptionalParams): PagedAsyncIterableIterator<Agent>;
        listVersions(agentName: string, options?: AgentsListVersionsOptionalParams): PagedAsyncIterableIterator<AgentVersion>;
        /** Updates an existing agent from a definition. */
        update(agentName: string, definition: AgentDefinitionUnion, options?: AgentsUpdateOptionalParams): Promise<Agent>;
        /** Updates an existing agent from a manifest. */
        update(agentName: string, manifestId: string, parameterValues: Record<string, unknown>, options?: AgentsUpdateAgentFromManifestOptionalParams): Promise<Agent>;
    }

    // ── Connections operations ────────────────────────────────────────────

    export interface ConnectionsOperations {
        get(connectionName: string, options?: ConnectionsGetOptionalParams): Promise<Connection>;
        getDefault(connectionType: ConnectionType, options?: ConnectionsGetDefaultOptionalParams): Promise<Connection>;
        getWithCredentials(connectionName: string, options?: ConnectionsGetWithCredentialsOptionalParams): Promise<Connection>;
        list(options?: ConnectionsListOptionalParams): PagedAsyncIterableIterator<Connection>;
    }

    // ── Datasets operations ───────────────────────────────────────────────

    export interface DatasetsOperations {
        createOrUpdate(name: string, version: string, dataset: DatasetVersionUnion, options?: DatasetsCreateOrUpdateOptionalParams): Promise<DatasetVersionUnion>;
        delete(name: string, version: string, options?: DatasetsDeleteOptionalParams): Promise<void>;
        get(name: string, version: string, options?: DatasetsGetOptionalParams): Promise<DatasetVersionUnion>;
        getCredentials(name: string, version: string, options?: DatasetsGetCredentialsOptionalParams): Promise<DatasetCredential>;
        list(options?: DatasetsListOptionalParams): PagedAsyncIterableIterator<DatasetVersionUnion>;
        listVersions(name: string, options?: DatasetsListVersionsOptionalParams): PagedAsyncIterableIterator<DatasetVersionUnion>;
        pendingUpload(name: string, version: string, body: PendingUploadRequest, options?: DatasetsPendingUploadOptionalParams): Promise<PendingUploadResponse>;
    }

    // ── Indexes operations ────────────────────────────────────────────────

    export interface IndexesOperations {
        createOrUpdate(name: string, version: string, index: IndexUnion, options?: IndexesCreateOrUpdateOptionalParams): Promise<IndexUnion>;
        delete(name: string, version: string, options?: IndexesDeleteOptionalParams): Promise<void>;
        get(name: string, version: string, options?: IndexesGetOptionalParams): Promise<IndexUnion>;
        list(options?: IndexesListOptionalParams): PagedAsyncIterableIterator<IndexUnion>;
        listVersions(name: string, options?: IndexesListVersionsOptionalParams): PagedAsyncIterableIterator<IndexUnion>;
    }

    // ── Deployments operations ────────────────────────────────────────────

    export interface DeploymentsOperations {
        get(deploymentName: string, options?: DeploymentsGetOptionalParams): Promise<Deployment>;
        list(options?: DeploymentsListOptionalParams): PagedAsyncIterableIterator<Deployment>;
    }

    // ── EvaluationRules operations ────────────────────────────────────────

    export interface EvaluationRulesOperations {
        createOrUpdate(name: string, rule: EvaluationRule, options?: EvaluationRulesCreateOrUpdateOptionalParams): Promise<EvaluationRule>;
        delete(name: string, options?: EvaluationRulesDeleteOptionalParams): Promise<void>;
        get(name: string, options?: EvaluationRulesGetOptionalParams): Promise<EvaluationRule>;
        list(options?: EvaluationRulesListOptionalParams): PagedAsyncIterableIterator<EvaluationRule>;
    }

    // ── Beta operations ───────────────────────────────────────────────────

    /** @beta */
    export interface BetaOperations {
        /** @beta */
        readonly evaluators: BetaEvaluatorsOperations;
        /** @beta */
        readonly insights: BetaInsightsOperations;
        /** @beta */
        readonly memoryStores: BetaMemoryStoresOperations;
        /** @beta */
        readonly redTeams: BetaRedTeamsOperations;
        /** @beta */
        readonly schedules: BetaSchedulesOperations;
    }

    /** @beta */
    export interface BetaEvaluatorsOperations {
        createVersion(evaluatorId: string, version: string, body: EvaluatorDefinitionUnion, options?: BetaEvaluatorsCreateVersionOptionalParams): Promise<EvaluatorVersion>;
        deleteVersion(evaluatorId: string, version: string, options?: BetaEvaluatorsDeleteVersionOptionalParams): Promise<void>;
        getVersion(evaluatorId: string, version: string, options?: BetaEvaluatorsGetVersionOptionalParams): Promise<EvaluatorVersion>;
        listLatestVersions(options?: BetaEvaluatorsListLatestVersionsOptionalParams): PagedAsyncIterableIterator<EvaluatorVersion>;
        listVersions(evaluatorId: string, options?: BetaEvaluatorsListVersionsOptionalParams): PagedAsyncIterableIterator<EvaluatorVersion>;
        updateVersion(evaluatorId: string, version: string, body: EvaluatorDefinitionUnion, options?: BetaEvaluatorsUpdateVersionOptionalParams): Promise<EvaluatorVersion>;
    }

    /** @beta */
    export interface BetaInsightsOperations {
        generate(insightRequest: InsightRequest, options?: BetaInsightsGenerateOptionalParams): Promise<InsightResult>;
        get(insightId: string, options?: BetaInsightsGetOptionalParams): Promise<Insight>;
        list(options?: BetaInsightsListOptionalParams): PagedAsyncIterableIterator<Insight>;
    }

    /** @beta */
    export interface BetaMemoryStoresOperations {
        create(name: string, body: MemoryStoreDefinitionUnion, options?: BetaMemoryStoresCreateOptionalParams): Promise<MemoryStore>;
        delete(name: string, options?: BetaMemoryStoresDeleteOptionalParams): Promise<void>;
        deleteScope(name: string, options?: BetaMemoryStoresDeleteScopeOptionalParams): Promise<DeleteMemoryStoreResponse>;
        get(name: string, options?: BetaMemoryStoresGetOptionalParams): Promise<MemoryStore>;
        getUpdateResult(name: string, operationId: string, options?: BetaMemoryStoresGetUpdateResultOptionalParams): Promise<MemoryStoreUpdateResponse>;
        list(options?: BetaMemoryStoresListOptionalParams): PagedAsyncIterableIterator<MemoryStore>;
        searchMemories(name: string, query: string, options?: BetaMemoryStoresSearchMemoriesOptionalParams): Promise<MemoryStoreSearchResponse>;
        update(name: string, body: MemoryStoreDefinitionUnion, options?: BetaMemoryStoresUpdateOptionalParams): Promise<MemoryStore>;
        updateMemories(name: string, items: MemoryItem[], options?: BetaMemoryStoresUpdateMemoriesOptionalParams): Promise<MemoryStoreUpdateResponse>;
    }

    /** @beta */
    export interface BetaRedTeamsOperations {
        create(name: string, body: RedTeam, options?: BetaRedTeamsCreateOptionalParams): Promise<RedTeam>;
        get(name: string, options?: BetaRedTeamsGetOptionalParams): Promise<RedTeam>;
        list(options?: BetaRedTeamsListOptionalParams): PagedAsyncIterableIterator<RedTeam>;
    }

    /** @beta */
    export interface BetaSchedulesOperations {
        createOrUpdate(name: string, schedule: Schedule, options?: BetaSchedulesCreateOrUpdateOptionalParams): Promise<Schedule>;
        delete(name: string, options?: BetaSchedulesDeleteOptionalParams): Promise<void>;
        get(name: string, options?: BetaSchedulesGetOptionalParams): Promise<Schedule>;
        getRun(name: string, runId: string, options?: BetaSchedulesGetRunOptionalParams): Promise<ScheduleRun>;
        list(options?: BetaSchedulesListOptionalParams): PagedAsyncIterableIterator<Schedule>;
        listRuns(name: string, options?: BetaSchedulesListRunsOptionalParams): PagedAsyncIterableIterator<ScheduleRun>;
    }

    // ── Optional params ───────────────────────────────────────────────────

    export interface OperationOptions {
        abortSignal?: AbortSignal;
        requestOptions?: RequestOptions;
        tracingOptions?: TracingOptions;
    }

    export interface RequestOptions {
        timeout?: number;
    }

    export interface TracingOptions {
        tracingContext?: unknown;
    }

    export interface AgentsCreateOptionalParams extends OperationOptions {
        description?: string;
        metadata?: Record<string, string>;
        foundryFeatures?: AgentDefinitionOptInKeys;
    }

    export interface AgentsCreateAgentFromManifestOptionalParams extends OperationOptions {
        description?: string;
        metadata?: Record<string, string>;
    }

    export interface AgentsCreateVersionOptionalParams extends OperationOptions {
        description?: string;
        metadata?: Record<string, string>;
    }

    export interface AgentsDeleteOptionalParams extends OperationOptions {}
    export interface AgentsDeleteVersionOptionalParams extends OperationOptions {}
    export interface AgentsGetOptionalParams extends OperationOptions {}
    export interface AgentsGetVersionOptionalParams extends OperationOptions {}

    export interface AgentsListOptionalParams extends OperationOptions {
        after?: string;
        before?: string;
        kind?: AgentKind;
        limit?: number;
        order?: PageOrder;
    }

    export interface AgentsListVersionsOptionalParams extends OperationOptions {
        after?: string;
        before?: string;
        limit?: number;
        order?: PageOrder;
    }

    export interface AgentsUpdateOptionalParams extends OperationOptions {
        description?: string;
        metadata?: Record<string, string>;
    }

    export interface AgentsUpdateAgentFromManifestOptionalParams extends OperationOptions {
        description?: string;
        metadata?: Record<string, string>;
    }

    export interface ConnectionsGetOptionalParams extends OperationOptions {}
    export interface ConnectionsGetDefaultOptionalParams extends OperationOptions {}
    export interface ConnectionsGetWithCredentialsOptionalParams extends OperationOptions {}
    export interface ConnectionsListOptionalParams extends OperationOptions {}

    export interface DatasetsCreateOrUpdateOptionalParams extends OperationOptions {}
    export interface DatasetsDeleteOptionalParams extends OperationOptions {}
    export interface DatasetsGetOptionalParams extends OperationOptions {}
    export interface DatasetsGetCredentialsOptionalParams extends OperationOptions {}
    export interface DatasetsListOptionalParams extends OperationOptions {}
    export interface DatasetsListVersionsOptionalParams extends OperationOptions {}
    export interface DatasetsPendingUploadOptionalParams extends OperationOptions {}

    export interface IndexesCreateOrUpdateOptionalParams extends OperationOptions {}
    export interface IndexesDeleteOptionalParams extends OperationOptions {}
    export interface IndexesGetOptionalParams extends OperationOptions {}
    export interface IndexesListOptionalParams extends OperationOptions {}
    export interface IndexesListVersionsOptionalParams extends OperationOptions {}

    export interface DeploymentsGetOptionalParams extends OperationOptions {}
    export interface DeploymentsListOptionalParams extends OperationOptions {}

    export interface EvaluationRulesCreateOrUpdateOptionalParams extends OperationOptions {}
    export interface EvaluationRulesDeleteOptionalParams extends OperationOptions {}
    export interface EvaluationRulesGetOptionalParams extends OperationOptions {}
    export interface EvaluationRulesListOptionalParams extends OperationOptions {}

    export interface BetaEvaluatorsCreateVersionOptionalParams extends OperationOptions {}
    export interface BetaEvaluatorsDeleteVersionOptionalParams extends OperationOptions {}
    export interface BetaEvaluatorsGetVersionOptionalParams extends OperationOptions {}
    export interface BetaEvaluatorsListLatestVersionsOptionalParams extends OperationOptions {}
    export interface BetaEvaluatorsListVersionsOptionalParams extends OperationOptions {}
    export interface BetaEvaluatorsUpdateVersionOptionalParams extends OperationOptions {}

    export interface BetaInsightsGenerateOptionalParams extends OperationOptions {}
    export interface BetaInsightsGetOptionalParams extends OperationOptions {}
    export interface BetaInsightsListOptionalParams extends OperationOptions {}

    export interface BetaMemoryStoresCreateOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresDeleteOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresDeleteScopeOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresGetOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresGetUpdateResultOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresListOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresSearchMemoriesOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresUpdateOptionalParams extends OperationOptions {}
    export interface BetaMemoryStoresUpdateMemoriesOptionalParams extends OperationOptions {}

    export interface BetaRedTeamsCreateOptionalParams extends OperationOptions {}
    export interface BetaRedTeamsGetOptionalParams extends OperationOptions {}
    export interface BetaRedTeamsListOptionalParams extends OperationOptions {}

    export interface BetaSchedulesCreateOrUpdateOptionalParams extends OperationOptions {}
    export interface BetaSchedulesDeleteOptionalParams extends OperationOptions {}
    export interface BetaSchedulesGetOptionalParams extends OperationOptions {}
    export interface BetaSchedulesGetRunOptionalParams extends OperationOptions {}
    export interface BetaSchedulesListOptionalParams extends OperationOptions {}
    export interface BetaSchedulesListRunsOptionalParams extends OperationOptions {}

    // ── Agent model types (forward refs to @azure/ai-projects/models) ─────

    export type AgentDefinitionUnion = PromptAgentDefinition | WorkflowAgentDefinition | HostedAgentDefinition | AgentDefinition;
    export type AgentKind = "prompt" | "hosted" | "workflow";
    export type AgentDefinitionOptInKeys = "HostedAgents=V1Preview" | "WorkflowAgents=V1Preview";
    export type PageOrder = "asc" | "desc";
    export type ConnectionType = "AzureOpenAI" | "CognitiveServices" | "CustomKeys" | "MicrosoftFabric";
    export type DatasetType = "file" | "folder";
    export type IndexType = "AzureSearch" | "CosmosDB" | "ManagedAzureSearch";
    export type DeploymentType = "model";

    export interface AgentDefinition {
        kind: AgentKind;
        rai_config?: RaiConfig;
    }

    export interface PromptAgentDefinition extends AgentDefinition {
        readonly kind: "prompt";
        tools?: Tool[];
        tool_resources?: Record<string, unknown>;
    }

    export interface WorkflowAgentDefinition extends AgentDefinition {
        readonly kind: "workflow";
    }

    export interface HostedAgentDefinition extends AgentDefinition {
        readonly kind: "hosted";
    }

    export interface RaiConfig {
        policy?: string;
    }

    export interface Tool {
        type: string;
    }

    export interface Agent {
        id: string;
        name: string;
        object: "agent";
        versions: { latest: AgentVersion; };
    }

    export interface AgentVersion {
        id: string;
        name: string;
        version: string;
        createdAt: Date;
        definition: AgentDefinitionUnion;
    }

    export interface DeleteAgentResponse {
        deleted: boolean;
        id: string;
    }

    export interface DeleteAgentVersionResponse {
        deleted: boolean;
        id: string;
        version: string;
    }

    export interface Connection {
        id: string;
        name: string;
        type: ConnectionType;
        credentials?: BaseCredentialsUnion;
    }

    export type BaseCredentialsUnion = ApiKeyCredentials | EntraIDCredentials | NoAuthenticationCredentials | BaseCredentials;
    export type CredentialType = "ApiKey" | "EntraID" | "NoAuthentication" | "SASToken" | "AgenticIdentityToken_Preview";

    export interface BaseCredentials {
        readonly type: CredentialType;
    }

    export interface ApiKeyCredentials extends BaseCredentials {
        readonly type: "ApiKey";
        key: string;
    }

    export interface EntraIDCredentials extends BaseCredentials {
        readonly type: "EntraID";
    }

    export interface NoAuthenticationCredentials extends BaseCredentials {
        readonly type: "NoAuthentication";
    }

    export type DatasetVersionUnion = FileDatasetVersion | FolderDatasetVersion | DatasetVersion;
    export type IndexUnion = AzureAISearchIndex | ManagedAzureAISearchIndex | Index;
    export type DeploymentUnion = ModelDeployment | Deployment;
    export type EvaluatorDefinitionUnion = CodeBasedEvaluatorDefinition | PromptBasedEvaluatorDefinition | EvaluatorDefinition;
    export type InsightRequestUnion = EvaluationRunClusterInsightRequest | AgentClusterInsightRequest | EvaluationComparisonInsightRequest | InsightRequest;
    export type InsightResultUnion = EvaluationComparisonInsightResult | EvaluationRunClusterInsightResult | AgentClusterInsightResult | InsightResult;
    export type MemoryStoreDefinitionUnion = MemoryStoreDefaultDefinition | MemoryStoreDefinition;
    export type MemoryItemUnion = UserProfileMemoryItem | ChatSummaryMemoryItem | MemoryItem;

    export interface DatasetVersion {
        description?: string;
        id?: string;
        name?: string;
        tags?: Record<string, string>;
        type: DatasetType;
        version?: string;
    }

    export interface FileDatasetVersion extends DatasetVersion {
        readonly type: "file";
        uri?: string;
    }

    export interface FolderDatasetVersion extends DatasetVersion {
        readonly type: "folder";
        uri?: string;
    }

    export interface DatasetCredential {
        credential: SasCredential;
        blobReference: BlobReference;
    }

    export interface SasCredential {
        sasUri: string;
    }

    export interface BlobReference {
        blobUri: string;
        storageAccountArmId?: string;
    }

    export interface PendingUploadRequest {
        pendingUploadId?: string;
        pendingUploadType: PendingUploadType;
    }

    export interface PendingUploadResponse {
        blobReference: BlobReference;
        pendingUploadId: string;
        pendingUploadType: PendingUploadType;
    }

    export type PendingUploadType = "None" | "TemporaryBlobReference";

    export interface Index {
        description?: string;
        id?: string;
        name?: string;
        tags?: Record<string, string>;
        type: IndexType;
        version?: string;
    }

    export interface AzureAISearchIndex extends Index {
        readonly type: "AzureSearch";
        fieldMapping: FieldMapping;
        indexName?: string;
        indexingConnectionId?: string;
        queryConnectionId?: string;
    }

    export interface ManagedAzureAISearchIndex extends Index {
        readonly type: "ManagedAzureSearch";
        embeddingConfiguration?: EmbeddingConfiguration;
        fieldMapping?: FieldMapping;
    }

    export interface FieldMapping {
        contentFields?: string[];
        filepathField?: string;
        metadataFields?: string[];
        titleField?: string;
        urlField?: string;
        vectorFields?: string[];
    }

    export interface EmbeddingConfiguration {
        connectionId?: string;
        deploymentName?: string;
    }

    export interface Deployment {
        name: string;
        type: DeploymentType;
    }

    export interface ModelDeployment extends Deployment {
        readonly type: "model";
        modelName?: string;
        modelPublisher?: string;
        modelVersion?: string;
        sku?: ModelDeploymentSku;
    }

    export interface ModelDeploymentSku {
        capacity?: number;
        name?: string;
    }

    export interface EvaluationRule {
        action: EvaluationRuleActionUnion;
        filter: EvaluationRuleFilter;
        name?: string;
    }

    export type EvaluationRuleActionUnion = ContinuousEvaluationRuleAction | HumanEvaluationPreviewRuleAction | EvaluationRuleAction;
    export type EvaluationRuleActionType = "continuousEvaluation" | "humanEvaluation_Preview";

    export interface EvaluationRuleAction {
        type: EvaluationRuleActionType;
    }

    export interface ContinuousEvaluationRuleAction extends EvaluationRuleAction {
        readonly type: "continuousEvaluation";
        evaluators: EvaluatorDefinitionUnion[];
        samplingRate?: number;
    }

    export interface HumanEvaluationPreviewRuleAction extends EvaluationRuleAction {
        readonly type: "humanEvaluation_Preview";
        samplingRate?: number;
    }

    export interface EvaluationRuleFilter {
        eventType: EvaluationRuleEventType;
        tags?: Record<string, string>;
    }

    export type EvaluationRuleEventType = "AgentRun" | "Span";

    export interface EvaluatorDefinition {
        type: EvaluatorType;
    }

    export type EvaluatorType = "code" | "prompt";

    export interface CodeBasedEvaluatorDefinition extends EvaluatorDefinition {
        readonly type: "code";
        evaluatorId?: string;
        evaluatorVersion?: string;
    }

    export interface PromptBasedEvaluatorDefinition extends EvaluatorDefinition {
        readonly type: "prompt";
        evaluatorId?: string;
        evaluatorVersion?: string;
    }

    export interface EvaluatorVersion {
        evaluatorId?: string;
        version?: string;
    }

    export interface InsightRequest {
        type: InsightType;
    }

    export type InsightType = "EvaluationRunClusterInsight" | "AgentClusterInsight" | "EvaluationComparisonInsight";

    export interface InsightResult {
        type: InsightType;
    }

    export interface EvaluationRunClusterInsightRequest extends InsightRequest {
        readonly type: "EvaluationRunClusterInsight";
        evaluationRunIds: string[];
        modelConfiguration?: InsightModelConfiguration;
    }

    export interface AgentClusterInsightRequest extends InsightRequest {
        readonly type: "AgentClusterInsight";
        agentName: string;
        modelConfiguration?: InsightModelConfiguration;
    }

    export interface EvaluationComparisonInsightRequest extends InsightRequest {
        readonly type: "EvaluationComparisonInsight";
        baselineRunId: string;
        candidateRunIds: string[];
        modelConfiguration?: InsightModelConfiguration;
    }

    export interface InsightModelConfiguration {
        deploymentName: string;
        endpoint?: string;
    }

    export interface Insight {
        createdAt: Date;
        id: string;
        insightRequest?: InsightRequest;
        insightResult?: InsightResult;
        status: OperationState;
    }

    export type OperationState = "NotStarted" | "Running" | "Succeeded" | "Failed" | "Canceled";

    export interface EvaluationRunClusterInsightResult extends InsightResult {
        readonly type: "EvaluationRunClusterInsight";
        clusterInsight: ClusterInsightResult;
    }

    export interface AgentClusterInsightResult extends InsightResult {
        readonly type: "AgentClusterInsight";
        clusterInsight: ClusterInsightResult;
    }

    export interface EvaluationComparisonInsightResult extends InsightResult {
        readonly type: "EvaluationComparisonInsight";
        comparisons: EvalRunResultComparison[];
    }

    export interface ClusterInsightResult {
        clusters: InsightCluster[];
        summary: InsightSummary;
        usage: ClusterTokenUsage;
    }

    export interface InsightSummary {
        text: string;
    }

    export interface ClusterTokenUsage {
        completion: number;
        prompt: number;
    }

    export interface InsightCluster {
        id: string;
        label: string;
        representative: string;
        samples: InsightSampleUnion[];
        size: number;
    }

    export type InsightSampleUnion = EvaluationResultSample | InsightSample;
    export type SampleType = "EvaluationResult";

    export interface InsightSample {
        type?: SampleType;
    }

    export interface EvaluationResultSample extends InsightSample {
        readonly type: "EvaluationResult";
        evalResult: EvalResult;
    }

    export interface EvalResult {
        id: string;
        status?: OperationState;
    }

    export interface EvalRunResultComparison {
        runId: string;
        summary?: EvalRunResultSummary;
    }

    export interface EvalRunResultSummary {
        items: EvalRunResultCompareItem[];
    }

    export interface EvalRunResultCompareItem {
        direction: TreatmentEffectType;
        metricName: string;
    }

    export type TreatmentEffectType = "positive" | "negative" | "neutral";

    // ── Memory store types ────────────────────────────────────────────────

    export interface MemoryStoreDefinition {
        kind: MemoryStoreKind;
    }

    export type MemoryStoreKind = "default";

    export interface MemoryStoreDefaultDefinition extends MemoryStoreDefinition {
        readonly kind: "default";
        options?: MemoryStoreDefaultOptions;
    }

    export interface MemoryStoreDefaultOptions {
        maxSize?: number;
    }

    export interface MemoryStore {
        id: string;
        kind: MemoryStoreKind;
        name: string;
    }

    export interface DeleteMemoryStoreResponse {
        deleted: boolean;
        id: string;
    }

    export interface MemoryStoreSearchResponse {
        items: MemorySearchItem[];
    }

    export interface MemorySearchItem {
        item: MemoryItemUnion;
        score?: number;
    }

    export interface MemoryItem {
        kind: MemoryItemKind;
    }

    export type MemoryItemKind = "userProfile" | "chatSummary";

    export interface UserProfileMemoryItem extends MemoryItem {
        readonly kind: "userProfile";
        content: string;
    }

    export interface ChatSummaryMemoryItem extends MemoryItem {
        readonly kind: "chatSummary";
        content: string;
        sessionId?: string;
    }

    export interface MemoryStoreUpdateResponse {
        operationId: string;
        status: MemoryStoreUpdateStatus;
        result?: MemoryStoreUpdateCompletedResult;
    }

    export type MemoryStoreUpdateStatus = "NotStarted" | "Running" | "Succeeded" | "Failed";

    export interface MemoryStoreUpdateCompletedResult {
        added: number;
        removed: number;
        updated: number;
    }

    export type MemoryOperation = "Add" | "Remove";
    export type MemoryOperationKind = "Add" | "Remove";

    export interface MemoryStoreDeleteScopeResponse {
        deleted: boolean;
    }

    // ── RedTeam types ─────────────────────────────────────────────────────

    export interface RedTeam {
        attackStrategies?: AttackStrategy[];
        displayName?: string;
        name?: string;
        numTurns?: number;
        target?: TargetConfigUnion;
    }

    /** All attack strategies supported by the red team. */
    export type AttackStrategy = "easy" | "moderate" | "difficult" | "ascii_art" | "ascii_smuggler" | "atbash" | "base64" | "binary" | "caesar" | "character_space" | "jailbreak" | "ansi_attack" | "character_swap" | "suffix_append" | "string_join" | "unicode_confusable" | "unicode_substitution" | "diacritic" | "flip" | "leetspeak" | "rot13" | "morse" | "url" | "baseline" | "indirect_jailbreak" | "tense" | "multi_turn" | "crescendo";

    export type TargetConfigUnion = AzureOpenAIModelConfiguration | TargetConfig;

    export interface TargetConfig {
        type: string;
    }

    export interface AzureOpenAIModelConfiguration extends TargetConfig {
        azureDeployment?: string;
        azureEndpoint?: string;
    }

    // ── Schedule types ────────────────────────────────────────────────────

    export interface Schedule {
        displayName?: string;
        isEnabled?: boolean;
        name?: string;
        provisioningStatus?: ScheduleProvisioningStatus;
        task?: ScheduleTaskUnion;
        trigger?: TriggerUnion;
    }

    export type ScheduleProvisioningStatus = "Creating" | "Updating" | "Deleting" | "Succeeded" | "Failed" | "Canceled";

    export type TriggerUnion = CronTrigger | RecurrenceTrigger | Trigger;
    export type TriggerType = "Recurrence" | "Cron" | "OneTime";

    export interface Trigger {
        type: TriggerType;
    }

    export interface CronTrigger extends Trigger {
        readonly type: "Cron";
        expression: string;
    }

    export interface RecurrenceTrigger extends Trigger {
        readonly type: "Recurrence";
        frequency: RecurrenceType;
        interval: number;
        schedule?: RecurrenceScheduleUnion;
    }

    export type RecurrenceType = "Hour" | "Day" | "Week" | "Month";
    export type RecurrenceScheduleUnion = HourlyRecurrenceSchedule | DailyRecurrenceSchedule | WeeklyRecurrenceSchedule | MonthlyRecurrenceSchedule | RecurrenceSchedule;

    export interface RecurrenceSchedule {
        type: RecurrenceType;
    }

    export interface HourlyRecurrenceSchedule extends RecurrenceSchedule {
        readonly type: "Hour";
        minutes?: number[];
    }

    export interface DailyRecurrenceSchedule extends RecurrenceSchedule {
        readonly type: "Day";
        hours?: number[];
        minutes?: number[];
    }

    export interface WeeklyRecurrenceSchedule extends RecurrenceSchedule {
        readonly type: "Week";
        days?: DayOfWeek[];
        hours?: number[];
        minutes?: number[];
    }

    export type DayOfWeek = "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday" | "Sunday";

    export interface MonthlyRecurrenceSchedule extends RecurrenceSchedule {
        readonly type: "Month";
        days?: number[];
        hours?: number[];
        minutes?: number[];
    }

    export type ScheduleTaskUnion = EvaluationScheduleTask | InsightScheduleTask | ScheduleTask;
    export type ScheduleTaskType = "Evaluation" | "Insight";

    export interface ScheduleTask {
        type: ScheduleTaskType;
    }

    export interface EvaluationScheduleTask extends ScheduleTask {
        readonly type: "Evaluation";
        evaluationRunData?: Record<string, unknown>;
    }

    export interface InsightScheduleTask extends ScheduleTask {
        readonly type: "Insight";
        insightRequest?: InsightRequest;
    }

    export interface ScheduleRun {
        id: string;
        runId?: string;
        scheduleName: string;
        status: OperationState;
    }

    // ── Deprecated type ───────────────────────────────────────────────────

    /** @deprecated Use AIProjectClientOptionalParams instead. */
    export interface LegacyProjectClientOptions {
        apiVersion?: string;
    }
}

/* ============================================================
   @azure/ai-projects/models  –  raw generated models subpath
   ============================================================ */

declare module "@azure/ai-projects/models" {
    /** All tool types available for agents. */
    export type ToolUnion = BingGroundingTool | AzureAISearchTool | AzureFunctionTool | CodeInterpreterTool | FileSearchTool | FunctionTool | OpenApiTool | WebSearchTool | MCPTool | ComputerUsePreviewTool | ImageGenTool | WebSearchPreviewTool | Tool;

    export type ToolType =
        | "bing_grounding"
        | "azure_ai_search"
        | "azure_function"
        | "code_interpreter"
        | "file_search"
        | "function"
        | "openapi"
        | "web_search"
        | "mcp"
        | "computer_use_preview"
        | "image_gen"
        | "web_search_preview";

    export interface Tool {
        type: ToolType;
    }

    export interface BingGroundingTool extends Tool {
        readonly type: "bing_grounding";
        search_configurations?: BingGroundingSearchConfiguration[];
    }

    export interface BingGroundingSearchConfiguration {
        connection_id?: string;
        count?: number;
        freshness?: string;
        market?: string;
        query?: string;
    }

    export interface AzureAISearchTool extends Tool {
        readonly type: "azure_ai_search";
        search_configurations?: AzureAISearchToolResource[];
    }

    export interface AzureAISearchToolResource {
        index_connection_id?: string;
        index_name?: string;
        query_type?: AzureAISearchQueryType;
    }

    export type AzureAISearchQueryType = "simple" | "semantic" | "vector" | "vector_simple_hybrid" | "vector_semantic_hybrid";

    export interface AzureFunctionTool extends Tool {
        readonly type: "azure_function";
        azure_function?: AzureFunctionDefinition;
    }

    export interface AzureFunctionDefinition {
        function?: FunctionDefinition;
        input_binding?: AzureFunctionBinding;
        output_binding?: AzureFunctionBinding;
    }

    export interface FunctionDefinition {
        description?: string;
        name: string;
        parameters?: unknown;
    }

    export interface AzureFunctionBinding {
        queue?: AzureFunctionStorageQueue;
        type: string;
    }

    export interface AzureFunctionStorageQueue {
        queue_name: string;
        storage_service_endpoint: string;
    }

    export interface CodeInterpreterTool extends Tool {
        readonly type: "code_interpreter";
        container?: ContainerDefinition;
    }

    export interface ContainerDefinition {
        memory_limit?: ContainerMemoryLimit;
        network_policy?: ContainerNetworkPolicyParam;
    }

    export type ContainerMemoryLimit = "1gb" | "2gb" | "4gb" | "8gb";

    export interface ContainerNetworkPolicyParam {
        type: ContainerNetworkPolicyParamType;
    }

    export type ContainerNetworkPolicyParamType = "disabled" | "allowlist" | "domain_secret";

    export interface FileSearchTool extends Tool {
        readonly type: "file_search";
        file_search?: FileSearchOptions;
    }

    export interface FileSearchOptions {
        max_num_results?: number;
        ranking_options?: RankingOptions;
    }

    export interface RankingOptions {
        ranker: RankerVersionType;
        score_threshold?: number;
    }

    export type RankerVersionType = "auto" | "default_2024_08_21";

    export interface FunctionTool extends Tool {
        readonly type: "function";
        function: FunctionDefinition;
    }

    export interface OpenApiTool extends Tool {
        readonly type: "openapi";
        openapi?: OpenApiFunctionDefinition;
    }

    export interface OpenApiFunctionDefinition {
        auth?: OpenApiAuthDetails;
        description?: string;
        name?: string;
        spec?: unknown;
    }

    export interface OpenApiAuthDetails {
        type: OpenApiAuthType;
    }

    export type OpenApiAuthType = "anonymous" | "connection" | "managed_identity";

    export interface WebSearchTool extends Tool {
        readonly type: "web_search";
        search_configurations?: WebSearchConfiguration[];
    }

    export interface WebSearchConfiguration {
        connection_id?: string;
        filters?: WebSearchToolFilters;
    }

    export interface WebSearchToolFilters {
        approximate_location?: WebSearchApproximateLocation;
    }

    export interface WebSearchApproximateLocation {
        city?: string;
        country?: string;
        latitude?: number;
        longitude?: number;
        state?: string;
    }

    export interface MCPTool extends Tool {
        readonly type: "mcp";
        allowed_tools?: MCPToolFilter;
        require_approval?: MCPToolRequireApproval;
        server_label: string;
        server_url: string;
    }

    export interface MCPToolFilter {
        tool_names?: string[];
    }

    export type MCPToolRequireApproval = "always" | "never";

    export interface ComputerUsePreviewTool extends Tool {
        readonly type: "computer_use_preview";
        environment: ComputerEnvironment;
    }

    export type ComputerEnvironment = "browser" | "mac" | "ubuntu" | "windows";

    export interface ImageGenTool extends Tool {
        readonly type: "image_gen";
        input_fidelity?: InputFidelity;
    }

    export type InputFidelity = "low" | "high";

    export interface WebSearchPreviewTool extends Tool {
        readonly type: "web_search_preview";
        search_context_size?: SearchContextSize;
        user_location?: ApproximateLocation;
    }

    export type SearchContextSize = "low" | "medium" | "high";

    export interface ApproximateLocation {
        city?: string;
        country?: string;
        region?: string;
        timezone?: string;
        type: string;
    }
}
