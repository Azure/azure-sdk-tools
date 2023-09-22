import { SDKPipelineStatus } from './commonType';
import { TaskResult } from './taskResult';

export type CommonTrigger = {
    name: string; // the agent, e.g. UnifiedPipeline, Release, individual
};

export type PipelineTriggerSource = 'github' | 'openapi_hub';

export type UnifiedPipelineTrigger = CommonTrigger & {
    source: PipelineTriggerSource;
    pullRequestNumber: string; // the pull request number if it is triggerred by pr
    headSha: string; // the CI commit
    unifiedPipelineBuildId: string; // a unique build id unified pipeline assigned for each completed pipeline build id
    unifiedPipelineTaskKey: string; // a unified pipeline task key, e.g. LintDiff, Semantic
    unifiedPipelineSubTaskKey?: string; // sub task key, for dynamic generated sub task message
};

export type Trigger = CommonTrigger | UnifiedPipelineTrigger;

export type PipelineRun = {
    trigger: Trigger;
    pipelineBuildId: string; // the id of the record for the completed azure pipeline build.
    status: SDKPipelineStatus;
};

export type QueuedEvent = PipelineRun & {
    status: 'queued';
};

export type SkippedEvent = PipelineRun & {
    status: 'skipped';
    subTitle?: string;
};

export type InProgressEvent = PipelineRun & {
    status: 'in_progress';
};

export type CompletedEvent = PipelineRun & {
    status: 'completed';
    result: TaskResult;
    logPath: string;
    subTitle?: string;
};

export type PipelineRunEvent = QueuedEvent | InProgressEvent | CompletedEvent | SkippedEvent;
