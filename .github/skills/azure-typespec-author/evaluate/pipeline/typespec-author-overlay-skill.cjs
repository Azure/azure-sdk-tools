const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

function overlaySkill(branch, repository, repoRoot = process.cwd()) {
    if (!branch || branch === '$(SkillBranch)') {
        console.log('SkillBranch not set; keep current checked-out skill files.');
        return;
    }

    const remote = repository || 'origin';
    if (repository && (repository !== repository.trim() ||
        !/^https:\/\/github\.com\/[A-Za-z0-9-]+\/[A-Za-z0-9_.-]+$/.test(repository))) {
        throw new Error('SkillRepository must be a GitHub HTTPS repository URL, or empty to use origin.');
    }

    let ref = branch;
    if (ref.startsWith('refs/remotes/origin/')) {
        ref = `refs/heads/${ref.slice('refs/remotes/origin/'.length)}`;
    } else if (ref.startsWith('origin/')) {
        ref = `refs/heads/${ref.slice('origin/'.length)}`;
    } else if (!ref.startsWith('refs/')) {
        ref = `refs/heads/${ref}`;
    }

    const git = (args) => execFileSync('git', args, {
        cwd: repoRoot,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
    });
    git(['check-ref-format', ref]);
    console.log(`Overlaying azure-typespec-author skill from ${remote}: ${ref}`);
    git(['fetch', remote, ref]);

    const skillPaths = [
        '.github/skills/azure-typespec-author/SKILL.md',
        '.github/skills/azure-typespec-author/references',
    ];
    for (let i = 0; i < skillPaths.length; i++) {
        const expectedType = i === 0 ? 'blob' : 'tree';
        if (git(['cat-file', '-t', `FETCH_HEAD:${skillPaths[i]}`]).trim() !== expectedType) {
            throw new Error(`Skill source must contain ${skillPaths[i]} as a ${expectedType}.`);
        }
    }
    for (const skillPath of skillPaths) {
        fs.rmSync(path.join(repoRoot, skillPath), { recursive: true, force: true });
    }
    git(['checkout', 'FETCH_HEAD', '--', ...skillPaths]);
    console.log(`Skill source overlaid from ${ref}`);
}

if (require.main === module) {
    try {
        overlaySkill(process.env.SKILL_BRANCH, process.env.SKILL_REPOSITORY);
    } catch (error) {
        console.error(`Failed to overlay skill source: ${error.message}`);
        process.exitCode = 1;
    }
}

module.exports = { overlaySkill };
