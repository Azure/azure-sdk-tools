import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import fs from 'fs';
import path from 'path';
import { WorkflowContext } from '../../src/automation/workflow';
import * as fsUtils from '../../src/utils/fsUtils';

describe('fsUtils', () => {
  const mockContext = {
    tmpFolder: 'azure-sdk-for-js_tmp',
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn()
    }
  } as any as WorkflowContext;

  const mockFileName = 'generateInput.json';
  const mockJoinedPath = '/mnt/vss/_work/1/s/azure-sdk-for-js_tmp/generateInput.json';
  const mockContent = {
   "specFolder": "../azure-rest-api-specs",
   "headSha": "1111111111111111111111111111111111111111",
   "repoHttpsUrl": "https://github.com/Azure/azure-rest-api-specs",
   "changedFiles": [],
   "runMode": "spec-pull-request",
   "sdkReleaseType": "beta",
   "installInstructionInput": {
       "isPublic": true,
       "downloadUrlPrefix": "",
       "downloadCommandTemplate": "downloadCommand"
   },
   "relatedReadmeMdFiles": [
       "specification/cdn/resource-manager/readme.md"
   ]
   };

  beforeEach(() => {
    vi.resetAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('writeTmpJsonFile', () => {
    it('should write content to temporary file', () => {
    
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, "writeFileSync").mockImplementation(() => {
      // mock implementation intentionally left blank
    });

      fsUtils.writeTmpJsonFile(mockContext, mockFileName, mockContent);

      expect(fs.writeFileSync).toHaveBeenCalledWith(
        mockJoinedPath,
        JSON.stringify(mockContent, undefined, 2)
      );
      expect(mockContext.logger.info).toHaveBeenCalledTimes(2);
      expect(mockContext.logger.info).toHaveBeenCalledWith(`Write temp file ${mockJoinedPath} with content:`);
    });
    });

  describe('readTmpJsonFile', () => {
    it('should return undefined when file does not exist', () => {
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, "existsSync").mockReturnValue(false);
      
      const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);
      
      expect(result).toBeUndefined();
      expect(mockContext.logger.warn).toHaveBeenCalledWith(`Warning: File ${mockJoinedPath} not found to read. Re-run if the error is transient or report this issue through https://aka.ms/azsdk/support/specreview-channel.`);
    });

    it('should read and parse JSON file successfully', () => {
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, "existsSync").mockReturnValue(true);
      vi.spyOn(fs, "readFileSync").mockReturnValue(JSON.stringify(mockContent));

      const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

      expect(result).toEqual(mockContent);
      expect(mockContext.logger.info).toHaveBeenCalledTimes(2);
    });

    it('should handle JSON parse errors', () => {
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, "existsSync").mockReturnValue(true);
      vi.spyOn(fs, "readFileSync").mockReturnValue('invalid json');

      const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

      expect(result).toBeUndefined();
      expect(mockContext.logger.error).toHaveBeenCalled();
      expect(mockContext.logger.error).toHaveBeenCalledWith(expect.stringContaining("Re-run if the error is retryable or report this issue through https://aka.ms/azsdk/support/specreview-channel."));
    });
    
    it('should handle readFileSync errors', () => {
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, "existsSync").mockReturnValue(true);
      vi.spyOn(fs, "readFileSync").mockImplementation(() => {
      throw new Error('File not found');
        });

      const result = fsUtils.readTmpJsonFile(mockContext, mockFileName);

      expect(result).toBeUndefined();
      expect(mockContext.logger.error).toHaveBeenCalled();
      expect(mockContext.logger.error).toHaveBeenCalledWith(expect.stringContaining("File not found"));
    });

});

  describe('deleteTmpJsonFile', () => {
    it('should delete file if it exists', () => {
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, 'existsSync').mockReturnValue(true);
      const unlinkSpy = vi.spyOn(fs, 'unlinkSync').mockImplementation(() => {});

      fsUtils.deleteTmpJsonFile(mockContext, mockFileName);

      expect(unlinkSpy).toHaveBeenCalledWith(mockJoinedPath);
    });

    it('should not attempt to delete non-existent file', () => {
      vi.spyOn(path, "join").mockReturnValue(mockJoinedPath);
      vi.spyOn(fs, 'existsSync').mockReturnValue(false);
      const unlinkSpy = vi.spyOn(fs, 'unlinkSync').mockImplementation(() => {});

      fsUtils.deleteTmpJsonFile(mockContext, mockFileName);

      expect(unlinkSpy).not.toHaveBeenCalled();
    });
  });

  describe('joinPath', () => {
    it('joins path segments with forward slashes', () => {
      expect(fsUtils.joinPath('a', 'b', 'c')).toBe('a/b/c');
    });
    it('normalizes backslashes to forward slashes', () => {
      expect(fsUtils.joinPath('a\\b', 'c')).toBe('a/b/c');
    });
  });

  describe('resolvePath', () => {
    it('resolves path segments to an absolute path with forward slashes', () => {
      const resolved = fsUtils.resolvePath('a', 'b', 'c');
      expect(resolved.endsWith('a/b/c')).toBe(true);
      expect(resolved).not.toContain('\\');
    });
  });

  describe('pathRelativeTo', () => {
    it('returns relative path from base', () => {
      expect(fsUtils.pathRelativeTo('/my/path', '/')).toBe('my/path');
    });
    it('returns "/" for same paths', () => {
      expect(fsUtils.pathRelativeTo('/a/b', '/a/b')).toBe('');
    });
    it('appends slash if result ends with /..', () => {
      expect(fsUtils.pathRelativeTo('/a', '/a/b')).toBe('../');
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

  describe('pathWithoutFileExtension', () => {
    it('removes file extension', () => {
      expect(fsUtils.pathWithoutFileExtension('foo/bar.txt')).toBe('foo/bar');
    });
    it('returns same string if no extension', () => {
      expect(fsUtils.pathWithoutFileExtension('foo/bar')).toBe('foo/bar');
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

  describe('getParentFolderPath', () => {
    it('returns parent folder path', () => {
      expect(fsUtils.getParentFolderPath('/foo/bar/baz.txt')).toBe('/foo/bar');
    });
  });

  describe('fileExistsSync', () => {
    it('returns true if file exists and is file', () => {
      const statMock = { isFile: () => true } as fs.Stats;
      vi.spyOn(fs, 'lstatSync').mockReturnValue(statMock);
      expect(fsUtils.fileExistsSync('foo.txt')).toBe(true);
    });
    it('returns false if file does not exist', () => {
      vi.spyOn(fs, 'lstatSync').mockImplementation(() => { throw new Error('not found'); });
      expect(fsUtils.fileExistsSync('foo.txt')).toBe(false);
    });
    it('returns false if not a file', () => {
      const statMock = { isFile: () => false } as fs.Stats;
      vi.spyOn(fs, 'lstatSync').mockReturnValue(statMock);
      expect(fsUtils.fileExistsSync('foo.txt')).toBe(false);
    });
  });

  describe('getPathName', () => {
    it('returns last segment for posix and win32', () => {
      expect(fsUtils.getPathName('/foo/bar/baz')).toBe('baz');
      expect(fsUtils.getPathName('foo\\bar\\baz')).toBe('baz');
    });
  });

  describe('deleteFile', () => {
    it('resolves true if file deleted', async () => {
      vi.spyOn(fs, 'unlink').mockImplementation((_, cb) => cb(null));
      await expect(fsUtils.deleteFile('foo.txt')).resolves.toBe(true);
    });
    it('resolves false if file does not exist', async () => {
      vi.spyOn(fs, 'unlink').mockImplementation((_, cb) => cb({ code: 'ENOENT' } as any));
      await expect(fsUtils.deleteFile('foo.txt')).resolves.toBe(false);
    });
    it('rejects on other errors', async () => {
      vi.spyOn(fs, 'unlink').mockImplementation((_, cb) => cb({ code: 'EACCES' } as any));
      await expect(fsUtils.deleteFile('foo.txt')).rejects.toBeDefined();
    });
  });

  describe('writeFileContents', () => {
    it('resolves when write succeeds', async () => {
      vi.spyOn(fs, 'writeFile').mockImplementation((_, __, cb) => cb(null));
      await expect(fsUtils.writeFileContents('foo.txt', 'bar')).resolves.toBeUndefined();
    });
    it('rejects on error', async () => {
      vi.spyOn(fs, 'writeFile').mockImplementation((_, __, cb) => cb(new Error('fail')));
      await expect(fsUtils.writeFileContents('foo.txt', 'bar')).rejects.toBeDefined();
    });
  });

  describe('readFileContents', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('should return content when file exists', async () => {
    vi.spyOn(fs, 'readFile').mockImplementation(
      ((_, __, callback: (err: NodeJS.ErrnoException | null, data: string) => void) => {
        callback(null, 'file content');
      }) as typeof fs.readFile
    );

    const result = await fsUtils.readFileContents('some/file.txt');
    expect(result).toBe('file content');
  });

  it('should return undefined when file does not exist (ENOENT)', async () => {
    vi.spyOn(fs, 'readFile').mockImplementation(
      ((_, __, callback: (err: NodeJS.ErrnoException | null, data: string) => void) => {
        const err = new Error('File not found') as NodeJS.ErrnoException;
        err.code = 'ENOENT';
        callback(err, '' as string);
      }) as typeof fs.readFile
    );

    const result = await fsUtils.readFileContents('missing/file.txt');
    expect(result).toBeUndefined();
  });

  it('should throw error when other error occurs', async () => {
    vi.spyOn(fs, 'readFile').mockImplementation(
      ((_, __, callback: (err: NodeJS.ErrnoException | null, data: string) => void) => {
        const err = new Error('Permission denied') as NodeJS.ErrnoException;
        err.code = 'EACCES';
        callback(err, '' as string);
      }) as typeof fs.readFile
    );

    await expect(fsUtils.readFileContents('some/file.txt')).rejects.toThrow('Permission denied');
  });
  
  describe('getParentFolderPath', () => {
    it('returns parent directory', () => {
      expect(fsUtils.getParentFolderPath('/foo/bar/baz.txt')).toBe('/foo/bar');
    });
  });
  });
});
