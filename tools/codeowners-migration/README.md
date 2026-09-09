# CODEOWNERS Migration Verifier

A one-time migration aid that compares two `CODEOWNERS` files and reports whether they assign
ownership identically.

It exists to gate tooling that **regenerates** `CODEOWNERS`. A generator reformats the file — it
rewrites banners, drops hand-written asides, and standardizes spacing — so a textual diff of its
output against the original is unreadable and proves nothing. This tool ignores everything that is
presentation and compares only what GitHub and the Azure SDK tooling act on.

- **Base** — the existing, hand-maintained `CODEOWNERS` file.
- **Compare** — the `CODEOWNERS` file produced by the generator.

Exit code `0` means the two files are semantically identical, `1` means they differ, and `2` means
the comparison could not be performed. That makes it usable directly as a migration gate.

## Prerequisites

- .NET SDK 8.0

## Build and test

```bash
cd tools/codeowners-migration
dotnet build
dotnet test
```

## Usage

```bash
dotnet run --project Azure.Sdk.Tools.CodeownersMigration -- \
  --base <repo>/.github/CODEOWNERS.orig \
  --compare <repo>/.github/CODEOWNERS
```

| Option | Meaning |
|--------|---------|
| `--base` | Path to the base CODEOWNERS file. Required. |
| `--compare` | Path to the CODEOWNERS file to compare against the base. Required. |
| `--team-storage-uri` | Override the team/user blob storage URI used for team expansion. |
| `--max-differences` | Maximum number of differences to print. Defaults to 50; `0` prints all. |

Back up the original file before regenerating, since the generator overwrites `.github/CODEOWNERS`
in place and this tool needs the original to compare against.

## What is compared

Both files are parsed with `CodeownersParser` from `Azure.Sdk.Tools.CodeownersUtils`, producing a
list of `CodeownersEntry` objects. Entry *i* of the base is compared against entry *i* of the
compare file, on these fields:

| Field | Compared as |
|-------|-------------|
| `PathExpression` | Exact string, after trimming and adding a leading slash |
| `SourceOwners` | Set, case-insensitive, `@` ignored |
| `ServiceOwners` | Set, case-insensitive, `@` ignored |
| `AzureSdkOwners` | Set, case-insensitive, `@` ignored |
| `PRLabels` | Set, case-insensitive, `%` ignored |
| `ServiceLabels` | Set, case-insensitive, `%` ignored |

Comments, blank lines, section banners, and whitespace are not compared. They are exactly the
differences the generator is expected to introduce.

### Ordering is compared; ordering within an entry is not

CODEOWNERS resolution is **last-match-wins**, so the order of entries decides who owns a file. Two
files holding an identical set of entries in a different order assign ownership differently. The
comparison is therefore ordinal — position *i* against position *i* — and a reordering is reported.
A set-based comparison would call such a pair equal, which is precisely the defect a regeneration
gate exists to catch.

The order of owners *within* one entry carries no meaning to GitHub, so owner and label lists are
compared as sets.

### Paths are compared exactly

`/sdk/foo` and `/sdk/foo/` select different files in GitHub's matcher: the first matches a file or a
directory of that name, the second only the directory's contents. Normalizing one into the other
would let the generator silently change what a rule matches, so no trailing-slash normalization is
applied.

### Owners are compared unexpanded

`CodeownersParser` expands `@Azure/<team>` into individual members using the team membership blob.
Comparing expanded lists would make the result depend on live team membership, so that a team gaining
a member between two runs would read as an ownership change. The comparison uses the parser's
`OriginalSourceOwners`, `OriginalServiceOwners` and `OriginalAzureSdkOwners` properties, which
preserve the alias as written.

### Implicit owner inheritance is re-applied

When a block ends in a source path/owner line, the parser fills a `# ServiceLabel:` moniker and an
empty `# AzureSdkOwners:` moniker from the source owners:

```text
# ServiceLabel: %Storage
/sdk/storage/    @carol      <- @carol is also the service owner
```

That is a semantic rule, not formatting, and it does not appear in the `Original*` properties. The
comparison re-applies it on both sides. Without this, every block using the shorthand would compare
as having no service owners on both sides, and a change that stripped service ownership would pass
unnoticed.

### Rejected blocks fail the run

`CodeownersParser` reports malformed blocks on stderr and then **drops** them. A dropped block breaks
an ordinal comparison twice over: it shifts the position of every entry after it, and if the same
block is dropped from both files the run passes while having checked nothing about it. Parser output
is captured and treated as a load failure (exit `2`), not as a difference.

## Output

A failing run names the entry position, the field, and the value on each side, with line numbers into
both files:

```text
base:    .github/CODEOWNERS.orig (71 entries)
compare: .github/CODEOWNERS (71 entries)

Found 1 difference(s):

[11] /tools/codeowners-migration/ (SourceOwners differs; base line 37, compare line 37)
      base:    danieljurek
      compare: someoneelse
```

Because the comparison is positional, a single inserted or deleted entry shifts everything after it
and will be reported as a long run of differences. The first reported index is the one to fix.

## Testing

A defect in this tool is worse than no verification, because it produces a false assurance that
ownership was preserved. The tests are organized around the two directions of failure:

- **No false positives** — identical files compare equal; comments, banners, blank lines, whitespace,
  owner case and owner order are all ignored; the inherited-owners shorthand compares equal to itself.
- **No false negatives** — changed, added and removed source owners, service owners, Azure SDK owners,
  PR labels and service labels are each reported; a trailing-slash change is reported; reordered
  entries are reported; an entry present in only one file is reported with its index.

All fixtures use plain `@user` owners, so the suite needs no team membership blob and runs offline.

## Where this is used

Run by hand during migration, per repository. Not wired into any pipeline.

**Delete this directory once every repository has migrated.**
