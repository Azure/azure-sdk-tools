export function createFileTree(url: string): FileTree {
  const rootUrl = url;
  const fileMap = new Map<string, string>();

  function longestCommonPrefix(a: string, b: string): string {
    if (a === b) {
      return a;
    }
    let lastCommonSlash = -1;
    for (let i = 0; i < Math.min(a.length, b.length); i++) {
      if (a[i] === b[i]) {
        if (a[i] === "/") {
          lastCommonSlash = i;
        }
      } else {
        break;
      }
    }
    if (lastCommonSlash === -1) {
      throw new Error("no common prefix found");
    }
    return a.slice(0, lastCommonSlash + 1);
  }

  function findCommonRoot(): string {
    let candidate = "";
    for (const fileUrl of fileMap.keys()) {
      const lastSlashIndex = fileUrl.lastIndexOf("/");
      const dirUrl = fileUrl.slice(0, lastSlashIndex + 1);
      if (!candidate) {
        candidate = dirUrl;
      } else {
        candidate = longestCommonPrefix(candidate, dirUrl);
      }
    }
    return candidate;
  }

  return {
    addFile(url: string, contents: string): void {
      if (fileMap.has(url)) {
        throw new Error(`file already parsed: ${url}`);
      }
      fileMap.set(url, contents);
    },
    async createTree(): Promise<FileTreeResult> {
      const outputFiles = new Map<string, string>();
      // calculate the highest common root
      const root = findCommonRoot();
      let mainFilePath = "";
      for (const [url, contents] of fileMap.entries()) {
        const relativePath = url.slice(root.length);
        outputFiles.set(relativePath, contents);
        if (url === rootUrl) {
          mainFilePath = relativePath;
        }
      }
      if (!mainFilePath) {
        throw new RangeError(
          `Main file ${rootUrl} not added to FileTree. Did you forget to add it?`,
        );
      }
      return {
        mainFilePath,
        files: outputFiles,
      };
    },
  };
}

export interface FileTreeResult {
  mainFilePath: string;
  files: Map<string, string>;
}

export interface FileTree {
  addFile(url: string, contents: string): void;
  createTree(): Promise<FileTreeResult>;
}
