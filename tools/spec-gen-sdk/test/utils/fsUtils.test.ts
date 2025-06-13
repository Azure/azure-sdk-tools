import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import fs from 'fs';
import path from 'path';
import * as fsUtils from '../../src/utils/fsUtils';
import { WorkflowContext } from '../../src/types/Workflow';

describe('fsUtils', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('file operation', () => {
    const mockContext = ({
      tmpFolder: 'azure-sdk-for-js_tmp',
      logger: {
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
      },
    } as any) as WorkflowContext;

    const mockFileName = 'generateInput.json';
    const mockJoinedPath = '/mnt/vss/_work/1/s/azure-sdk-for-js_tmp/generateInput.json';
    const mockContent = {
      specFolder: '../azure-rest-api-specs',
      headSha: '1111111111111111111111111111111111111111',
      repoHttpsUrl: 'https://github.com/Azure/azure-rest-api-specs',
      changedFiles: ['a', 'b', 'c'],
      runMode: 'spec-pull-request',
      sdkReleaseType: 'beta',
      installInstructionInput: {
        isPublic: true,
        downloadUrlPrefix: '',
        downloadCommandTemplate: 'downloadCommand',
      },
      relatedReadmeMdFiles: ['specification/cdn/resource-manager/readme.md'],
    };

    describe('writeTmpJsonFile', () => {
      it('should write content to temporary file', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'writeFileSync').mockImplementation(() => {
          // mock implementation intentionally left blank
        });

        fsUtils.writeTmpJsonFile(mockContext, mockFileName, mockContent);

        expect(fs.writeFileSync).toHaveBeenCalledWith(mockJoinedPath, JSON.stringify(mockContent, undefined, 2));
        expect(mockContext.logger.info).toHaveBeenCalledTimes(2);
        expect(mockContext.logger.info).toHaveBeenCalledWith(`Write temp file ${mockJoinedPath} with content:`);
      });
    });

    describe('readTmpJsonFile', () => {
      it('should return undefined when file does not exist', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'existsSync').mockReturnValue(false);

        const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

        expect(result).toBeUndefined();
        expect(mockContext.logger.warn).toHaveBeenCalledWith(
          `Warning: File ${mockJoinedPath} not found to read. Re-run if the error is transient or report this issue through https://aka.ms/azsdk/support/specreview-channel.`,
        );
      });

      it('should read and parse JSON file successfully', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'existsSync').mockReturnValue(true);
        vi.spyOn(fs, 'readFileSync').mockReturnValue(JSON.stringify(mockContent));

        const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

        expect(result).toEqual(mockContent);
        expect(mockContext.logger.info).toHaveBeenCalledTimes(2);
      });

      it('should handle JSON parse errors', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'existsSync').mockReturnValue(true);
        vi.spyOn(fs, 'readFileSync').mockReturnValue('invalid json');

        const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

        expect(result).toBeUndefined();
        expect(mockContext.logger.error).toHaveBeenCalled();
        expect(mockContext.logger.error).toHaveBeenCalledWith(
          expect.stringContaining('Re-run if the error is retryable or report this issue through https://aka.ms/azsdk/support/specreview-channel.'),
        );
      });

      it('should handle readFileSync errors', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'existsSync').mockReturnValue(true);
        vi.spyOn(fs, 'readFileSync').mockImplementation(() => {
          throw new Error('File not found');
        });

        const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

        expect(result).toBeUndefined();
        expect(mockContext.logger.error).toHaveBeenCalled();
        expect(mockContext.logger.error).toHaveBeenCalledWith(expect.stringContaining('File not found'));
      });
    });

    describe('deleteTmpJsonFile', () => {
      it('should delete file if it exists', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'existsSync').mockReturnValue(true);
        const unlinkSpy = vi.spyOn(fs, 'unlinkSync').mockImplementation(() => {});

        fsUtils.deleteTmpJsonFile(mockContext, mockFileName);

        expect(unlinkSpy).toHaveBeenCalledWith(mockJoinedPath);
      });

      it('should not attempt to delete non-existent file', () => {
        vi.spyOn(path, 'join').mockReturnValue(mockJoinedPath);
        vi.spyOn(fs, 'existsSync').mockReturnValue(false);
        const unlinkSpy = vi.spyOn(fs, 'unlinkSync').mockImplementation(() => {});

        fsUtils.deleteTmpJsonFile(mockContext, mockFileName);

        expect(unlinkSpy).not.toHaveBeenCalled();
      });
    });
  });

  describe('path operation', () => {
    describe('joinPath', () => {
      it('joins path segments with forward slashes', () => {
        expect(fsUtils.joinPath('a', 'b', 'c')).toBe('a/b/c');
      });

      it('normalizes backslashes to forward slashes', () => {
        expect(fsUtils.joinPath('a\\b', 'c')).toBe('a/b/c');
      });
    });

    describe('normalizePath', () => {
      it('replaces backslashes with slashes by default', () => {
        expect(fsUtils.normalizePath('a\\b\\c')).toBe('a/b/c');
      });
      it('replaces slashes with backslashes on win32', () => {
        expect(fsUtils.normalizePath('a/b/c', 'win32')).toBe('a\\b\\c');
      });
      it('returns empty string if input is empty', () => {
        expect(fsUtils.normalizePath('')).toBe('');
      });
    });

    describe('getRootPath', () => {
      it('returns root for absolute win32 path', () => {
        expect(fsUtils.getRootPath('C:\\foo\\bar')).toBe('C:\\');
      });
      it('returns root for absolute posix path', () => {
        expect(fsUtils.getRootPath('/foo/bar')).toBe('/');
      });
      it('returns undefined for relative path', () => {
        expect(fsUtils.getRootPath('foo/bar')).toBeUndefined();
      });
    });

    describe('isRooted', () => {
      it('returns true for absolute path', () => {
        expect(fsUtils.isRooted('/foo/bar')).toBe(true);
      });
      it('returns false for relative path', () => {
        expect(fsUtils.isRooted('foo/bar')).toBe(false);
      });
    });

    describe('getName and getPathName', () => {
      it('returns last segment of path', () => {
        expect(fsUtils.getName('/foo/bar/baz.txt')).toBe('baz.txt');
        expect(fsUtils.getPathName('foo\\bar\\baz.txt')).toBe('baz.txt');
      });
      it('returns input if no slashes', () => {
        expect(fsUtils.getName('baz.txt')).toBe('baz.txt');
      });
    });

  });

});
