const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { afterEach, test } = require('node:test');
const { overlaySkill } = require('./typespec-author-overlay-skill.cjs');

const skillRoot = '.github/skills/azure-typespec-author';
const roots = [];

function git(root, ...args) {
    return execFileSync('git', args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
}

function writeSkill(root, content) {
    const references = path.join(root, skillRoot, 'references');
    fs.mkdirSync(references, { recursive: true });
    fs.writeFileSync(path.join(root, skillRoot, 'SKILL.md'), content);
    fs.writeFileSync(path.join(references, `${content}.md`), content);
}

function fixture() {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'typespec-overlay-'));
    roots.push(root);
    const source = path.join(root, 'source');
    const checkout = path.join(root, 'checkout');
    fs.mkdirSync(source);
    git(source, 'init', '-b', 'main');
    git(source, 'config', 'user.name', 'Overlay Test');
    git(source, 'config', 'user.email', 'overlay@example.com');
    writeSkill(source, 'original');
    fs.writeFileSync(path.join(source, 'unrelated.txt'), 'keep');
    git(source, 'add', '.');
    git(source, 'commit', '-m', 'Original skill');
    git(root, 'clone', source, checkout);
    git(source, 'switch', '-c', 'updated');
    fs.rmSync(path.join(source, skillRoot, 'references', 'original.md'));
    writeSkill(source, 'updated');
    git(source, 'add', '.');
    git(source, 'commit', '-m', 'Updated skill');
    git(source, 'update-ref', 'refs/pull/42/head', 'HEAD');
    git(source, 'update-ref', 'refs/heads/updated;command', 'HEAD');
    return { source, checkout };
}

function assertOverlay(checkout) {
    assert.equal(fs.readFileSync(path.join(checkout, skillRoot, 'SKILL.md'), 'utf8'), 'updated');
    assert.equal(fs.readFileSync(path.join(checkout, skillRoot, 'references', 'updated.md'), 'utf8'), 'updated');
    assert.equal(fs.existsSync(path.join(checkout, skillRoot, 'references', 'original.md')), false);
    assert.equal(fs.readFileSync(path.join(checkout, 'unrelated.txt'), 'utf8'), 'keep');
    assert.equal(git(checkout, 'branch', '--show-current').trim(), 'main');
}

afterEach(() => {
    for (const root of roots.splice(0)) {
        fs.rmSync(root, { recursive: true, force: true });
    }
});

test('an unset SkillBranch, including an unresolved pipeline variable, leaves the checkout unchanged', () => {
    const { checkout } = fixture();
    for (const branch of [undefined, '', '$(SkillBranch)']) {
        overlaySkill(branch, undefined, checkout);
        assert.equal(git(checkout, 'status', '--porcelain'), '');
    }
});

for (const branch of ['updated', 'refs/heads/updated', 'origin/updated', 'refs/remotes/origin/updated', 'refs/pull/42/head', 'updated;command']) {
    test(`the default repository preserves origin fetch behavior for ${branch}`, () => {
        const { checkout } = fixture();
        overlaySkill(branch, '', checkout);
        assertOverlay(checkout);
    });
}

test('a specs-hosted ref is fetched from SkillRepository instead of origin', () => {
    const { source, checkout } = fixture();
    const repository = 'https://github.com/Azure/azure-rest-api-specs.git';
    git(checkout, 'config', `url.${source.replaceAll('\\', '/')}.insteadOf`, repository);
    git(checkout, 'remote', 'set-url', 'origin', path.join(checkout, 'nonexistent-origin'));
    overlaySkill('refs/pull/42/head', repository, checkout);
    assertOverlay(checkout);
});

test('invalid repositories and refs fail before changing the checkout', () => {
    const { checkout } = fixture();
    for (const repository of [
        '--upload-pack=command',
        'https://example.com/repo.git',
        'https://github.com/Azure/repo.git;command',
        'https://github.com/Azure/repo.git\n',
    ]) {
        assert.throws(() => overlaySkill('updated', repository, checkout), /SkillRepository/);
    }
    assert.throws(() => overlaySkill('updated; command', '', checkout), /check-ref-format/);
    assert.equal(git(checkout, 'status', '--porcelain'), '');
});

test('a missing remote ref leaves the current skill intact', () => {
    const { checkout } = fixture();
    assert.throws(() => overlaySkill('nonexistent', '', checkout), /fetch/);
    assert.equal(git(checkout, 'status', '--porcelain'), '');
});

test('a source without the required skill files leaves the current skill intact', () => {
    const { source, checkout } = fixture();
    fs.rmSync(path.join(source, skillRoot, 'references'), { recursive: true });
    git(source, 'add', '.');
    git(source, 'commit', '-m', 'Remove source references');
    assert.throws(() => overlaySkill('updated', '', checkout), /cat-file/);
    assert.equal(git(checkout, 'status', '--porcelain'), '');
});
