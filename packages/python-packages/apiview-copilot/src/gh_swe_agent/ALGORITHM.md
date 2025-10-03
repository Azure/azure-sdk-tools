APIView GitHub SWE Agent Algorithm

Notes:
- use the gpt-4.x. models on each step since those are more deterministic

1. consolidate duplicates
2. run a prompt on each remaining comment to determine where the change should be made
3. convert the comment and change location into a "plan" to pass to the SWE Agent.

4. Add commands to cli.bat to run this. Like: `cli-bat pr plan --comments <file> --format <xml|md>`

## Stages
- **Comment Consolidation:** Get comments and de-duplicate.
- **Context:** Search the SDK repos to determine where change will need to be made. Create instructions with repo, file, and method/class/parameter to make a change to along with the applicable comment.
- **Prompt:** Batch instructions into reasonably sized and scoped md files for creating PRs.
