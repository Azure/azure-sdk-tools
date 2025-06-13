
import { default as Transport } from 'winston-transport';
import { SdkAutoContext, VsoLogs } from './Entrypoint';
import { PackageData } from './PackageData';
import { SDKAutomationState } from '../automation/sdkAutomationState';

export enum FailureType {
  CodegenFailed = 'Code Generator Failed',
  SpecGenSdkFailed = 'Spec-Gen-Sdk Failed'
}

export type WorkflowContext = SdkAutoContext & {
  stagedArtifactsFolder?: string;
  sdkArtifactFolder?: string;
  isSdkConfigDuplicated?: boolean;
  specConfigPath?: string;
  pendingPackages: PackageData[];
  handledPackages: PackageData[];
  status: SDKAutomationState;
  failureType?: FailureType;
  messages: string[];
  messageCaptureTransport: Transport;
  scriptEnvs: { [key: string]: string | undefined };
  tmpFolder: string;
  vsoLogs: VsoLogs;
};
