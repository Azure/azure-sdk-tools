// eslint-disable-next-line simple-import-sort/imports
import { closeWithSuccess } from './utils/childProcesses';
import { GitOperationWrapper } from '../../src/utils/GitOperationWrapper';
import { mockChildProcessModule } from './utils/mockChildProcess';

describe('git operation wrapper', () => {
    let gitOperationWrapper: GitOperationWrapper;

    beforeAll(() => {
        gitOperationWrapper = new GitOperationWrapper();
    }
    );
    it('getFileListInPackageFolder', async () => {
        gitOperationWrapper.getFileListInPackageFolder('.');
        await closeWithSuccess();
        expect(mockChildProcessModule.$mostRecent().$args).toEqual(['ls-files', '-cmo', '--exclude-standard']);
    });

    it('getHeadSha', async () => {
        gitOperationWrapper.getHeadSha();
        await closeWithSuccess();
        expect(mockChildProcessModule.$mostRecent().$args).toEqual(['rev-parse', 'HEAD']);
    });

    it('getHeadRef', async () => {
        gitOperationWrapper.getHeadRef();
        await closeWithSuccess();
        expect(mockChildProcessModule.$mostRecent().$args).toEqual(['rev-parse', '--abbrev-ref', 'HEAD']);
    });

    it('safeDirectory', async () => {
        gitOperationWrapper.safeDirectory();
        await closeWithSuccess();
        expect(mockChildProcessModule.$mostRecent().$args).toEqual(['config', '--global', '--add', 'safe.directory', '.']);
    });

    it('disableFileMode', async () => {
        gitOperationWrapper.disableFileMode();
        await closeWithSuccess();
        expect(mockChildProcessModule.$mostRecent().$args).toEqual(['config', 'core.fileMode', 'false', '--replace-all']);
    });

    it('getChangedPackageDirectory', async () => {
        gitOperationWrapper.getChangedPackageDirectory();
        await closeWithSuccess();
        expect(mockChildProcessModule.$mostRecent().$args).toEqual(['ls-files', '-mdo', '--exclude-standard']);
    });
});
