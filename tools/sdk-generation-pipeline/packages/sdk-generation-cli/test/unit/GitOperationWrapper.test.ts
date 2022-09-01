// eslint-disable-next-line simple-import-sort/imports
import * as cp from './utils/mockChildProcess';
import { GitOperationWrapper } from '../../src/utils/GitOperationWrapper';

describe('git operation wrapper', () => {
    let gitOperationWrapper: GitOperationWrapper;

    function assertExecutedCommands(...commands: string[]) {
        expect(cp.mockChildProcessModule.$mostRecent().$args).toEqual(commands);
    }

    beforeAll(() => {
        gitOperationWrapper = new GitOperationWrapper();
    }
    );
    it('getFileListInPackageFolder', async () => {
        gitOperationWrapper.getFileListInPackageFolder('.');
        await cp.closeWithSuccess();
        assertExecutedCommands('ls-files', '-cmo', '--exclude-standard');
    });

    it('getHeadSha', async () => {
        gitOperationWrapper.getHeadSha();
        await cp.closeWithSuccess();
        assertExecutedCommands('rev-parse', 'HEAD');
    });

    it('getHeadRef', async () => {
        gitOperationWrapper.getHeadRef();
        await cp.closeWithSuccess();
        assertExecutedCommands('rev-parse', '--abbrev-ref', 'HEAD');
    });

    it('safeDirectory', async () => {
        gitOperationWrapper.safeDirectory();
        await cp.closeWithSuccess();
        assertExecutedCommands('config', '--global', '--add', 'safe.directory', '.');
    });

    it('disableFileMode', async () => {
        gitOperationWrapper.disableFileMode();
        await cp.closeWithSuccess();
        assertExecutedCommands('config', 'core.fileMode', 'false', '--replace-all');
    });

    it('getChangedPackageDirectory', async () => {
        gitOperationWrapper.getChangedPackageDirectory();
        await cp.closeWithSuccess();
        assertExecutedCommands('ls-files', '-mdo', '--exclude-standard');
    });
});
