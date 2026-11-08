# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

# Use case: This script merges changes from a source branch into the current branch with the behavior:
# 1. Overwrite paths matching $Theirs (this includes deletes)
# 2. Ensure files in $Ours remain untouched
# 3. For paths matching $Merge, merge changes from $SourceBranch allowing the user to resolve conflicts manually
#
# Adding paths to $Merge excludes them from the default keep or overwrite behaviour of $Ours and $Theirs.
# Only the selected paths are resolved; untracked files and conflicts requiring a manual merge are left untouched.
#
# This script can be run locally from the root of the repo:
# .\eng\scripts\Merge-Branch.ps1 -SourceBranch 'main' -Theirs '**' -Ours 'sdk/template' -Merge 'sdk/template/ci.yml', '**/README.md'
#
# This would merge main into the local branch, making the working folder look like main. It will not overwrite sdk\template.
# Changes in sdk\template\ci.yml and readme.md files would be merged, not excluded or overwritten.

[CmdLetBinding()]
param(
    [string]$SourceBranch,
    [string[]]$Theirs = @(), # paths to always overwrite
    [string[]]$Ours = @(), # paths to never merge or overwrite
    [string[]]$Merge = @(), # paths to merge or overwrite
    [bool]$AcceptTheirsForFinalMerge = $false
)

# Pathspec glossary entry: https://git-scm.com/docs/gitglossary#Documentation/gitglossary.txt-aiddefpathspecapathspec
#
# - They're space separeted strings that match file paths in the repository
# - They're repository paths, not filesystem paths and are slash direction sensitive.
# - They support "magic words" between parenthesis that control how the following path matches files:
#   - top: treat the path a top level / repository rooted
#   - glob: treat wildcards using glob patterns
#       /*/ == single wildcarded directory level
#       /**/ == all subdirectories, recursive
#   - exclude: after processing other pathspecs, remove any path matching this pathspec from the results

# Apply git pathspec magic to the paths
$theirIncludes = @($Theirs | ForEach-Object { ":(top,glob)$_" })
$ourIncludes = @($Ours | ForEach-Object { ":(top,glob)$_" })
$mergeIncludes = @($Merge | ForEach-Object { ":(top,glob)$_" })
$mergeExcludes = @($Merge | ForEach-Object { ":(top,glob,exclude)$_" })
$ourExcludes = @($Ours | ForEach-Object { ":(top,glob,exclude)$_" })

function ErrorExit($exitCode) {
    Write-Host "`nError creating merge commit`n" `
    "  Your local repository is in an unknown state`n" `
    "  Run `"git reset --hard`" to revert the partial merge"

    exit $exitCode
}

# Start a three-way merge to apply nonconflicting changes and identify conflicts.
# --no-ff keeps HEAD at the target's pre-merge commit, even if a fast-forward is possible.
# --no-commit leaves the merge open so the path policies below can be applied before the caller commits.
Write-Verbose "git -c user.name=`"azure-sdk`" -c user.email=`"azuresdk@microsoft.com`" merge $SourceBranch --no-ff --no-commit"
git -c user.name="azure-sdk" -c user.email="azuresdk@microsoft.com" merge $SourceBranch --no-ff --no-commit | Tee-Object -Variable mergeOutput
if ($LASTEXITCODE -and -not $mergeOutput.EndsWith('Automatic merge failed; fix conflicts and then commit the result.')) { ErrorExit $LASTEXITCODE }

# Update paths matching "theirs", except for "ours" and "merge", to the state in $SourceBranch.
if ($Theirs.Length) {
    # Stage tracked updates and deletions in this selection to clear unmerged index entries.
    # This is only an intermediate resolution; restore below supplies the final contents.
    # -u leaves untracked files alone, and the exclusions preserve the other path policies.
    Write-Verbose "git add -u -- $theirIncludes $ourExcludes $mergeExcludes"
    git add -u -- $theirIncludes $ourExcludes $mergeExcludes
    if ($LASTEXITCODE) { ErrorExit $LASTEXITCODE }
    # Replace both the index (--staged) and files on disk (--worktree) with the source snapshot.
    # Clearing conflicts first lets restore remove source-deleted files from both places;
    # --ignore-unmerged could instead leave those files on disk to be accidentally added back.
    Write-Verbose "git restore -s $SourceBranch --staged --worktree -- $theirIncludes $ourExcludes $mergeExcludes"
    git restore -s $SourceBranch --staged --worktree -- $theirIncludes $ourExcludes $mergeExcludes
    if ($LASTEXITCODE) { ErrorExit $LASTEXITCODE }
}

# Update paths matching "ours", except for "merge", to their pre-merge state.
if ($Ours.Length) {
    # Clear unmerged index entries only in the protected selection, without staging untracked files
    # or resolving conflicts reserved for the separate merge policy.
    Write-Verbose "git add -u -- $ourIncludes $mergeExcludes"
    git add -u -- $ourIncludes $mergeExcludes
    if ($LASTEXITCODE) { ErrorExit $LASTEXITCODE }
    # rev-parse HEAD identifies the target's unchanged pre-merge commit.
    # Restore that snapshot to the index and disk, including files the target had deleted.
    Write-Verbose "git restore -s (git rev-parse HEAD) --staged --worktree -- $ourIncludes $mergeExcludes"
    git restore -s (git rev-parse HEAD) --staged --worktree -- $ourIncludes $mergeExcludes
    if ($LASTEXITCODE) { ErrorExit $LASTEXITCODE }
}

# Resolve the merge selection automatically only when requested and nonempty;
# passing no pathspecs to restore would select the entire repository.
if ($AcceptTheirsForFinalMerge -and $Merge.Length) {
    # Clear tracked conflicts within the merge selection before applying its source-wins policy.
    Write-Verbose "git add -u -- $mergeIncludes"
    git add -u -- $mergeIncludes
    if ($LASTEXITCODE) { ErrorExit $LASTEXITCODE }
    # Take the source snapshot, including deletions, for both the index and working tree.
    # Without this opt-in, these paths keep the original merge result for manual resolution.
    Write-Verbose "git restore -s $SourceBranch --staged --worktree -- $mergeIncludes"
    git restore -s $SourceBranch --staged --worktree -- $mergeIncludes
    if ($LASTEXITCODE) { ErrorExit $LASTEXITCODE }
}
else {
    Write-Host "Merge commit started`n" `
    "  Use `"git reset --hard`" to revert the partial merge`n" `
    "  Use `"git commit --no-edit`" to complete the merge with the default merge message`n" `
    "  Use `"git commit -m <message>`" to complete the merge with a custom message"
}

exit 0
