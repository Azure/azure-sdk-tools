import { setSdkAutoStatus } from '../utils/runScript';
import { CommentCaptureTransport } from './logging';
import { PackageData } from '../types/PackageData';
import {
  workflowPkgCallBuildScript,
  workflowPkgCallChangelogScript,
  workflowPkgCallInstallInstructionScript,
  workflowPkgDetectArtifacts,
  workflowPkgSaveApiViewArtifact,
  workflowPkgSaveSDKArtifact,
} from './workflowPackageSteps';
import { WorkflowContext } from '../types/Workflow';

export const workflowPkgMain = async (context: WorkflowContext, pkg: PackageData) => {
  context.logger.log('section', `Handle package ${pkg.name}`);
  context.logger.info(`Package log to a new logFile`);

  const pkgCaptureTransport = new CommentCaptureTransport({
    extraLevelFilter: ['error', 'warn'],
    level: 'debug',
    output: pkg.messages,
  });
  context.logger.add(pkgCaptureTransport);

  await workflowPkgCallBuildScript(context, pkg);
  await workflowPkgCallChangelogScript(context, pkg);
  await workflowPkgDetectArtifacts(context, pkg);
  await workflowPkgSaveSDKArtifact(context, pkg);
  await workflowPkgSaveApiViewArtifact(context, pkg);
  await workflowPkgCallInstallInstructionScript(context, pkg);

  setSdkAutoStatus(pkg, 'succeeded');
  context.logger.remove(pkgCaptureTransport);
  context.logger.log('endsection', `Handle package ${pkg.name}`);
};
