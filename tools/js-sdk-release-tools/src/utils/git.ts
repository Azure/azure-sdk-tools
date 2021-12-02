import simpleGit, {SimpleGit} from 'simple-git';
import { execSync } from "child_process";
import {logger} from "./logger";

const git: SimpleGit = simpleGit();
const path = require('path');

export async function getChangedPackageDirectory(root: string = './') {
  const changedPackageDirectories: Set<string> = new Set<string>();
  const gitStatus = await git.status();
  const files = gitStatus.files;
  for (const file of files) {
    const filePath = file.path;
      if (filePath.match(/sdk\/[^\/]*\/arm-.*/)) {
      const packageDirectory = /sdk\/[^\/]*\/arm-[^\/]*/.exec(filePath);
      if (packageDirectory) {
        changedPackageDirectories.add(packageDirectory[0]);
      }
    }
  }
  return changedPackageDirectories;
}


export async function getLastCommitId(repository: string) {
  let commitId = '';
  try {
    commitId = execSync(`git --git-dir=${path.join(repository, '.git')} log --format=%H -n 1`, { encoding: "utf8" });
  } catch (e) {
    logger.log(`cannot get commit id from ${repository}`);
  }
  return commitId.trim();
}
