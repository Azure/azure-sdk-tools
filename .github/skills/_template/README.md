# Skills Test Template

Use this template to create tests for a new skill.

## Quick Start

1. **Copy this folder** to `tests/skills/{skill-name}/`
2. **Update SKILL_NAME** in prompts.json
3. **Add trigger prompts** that should/shouldn't match
4. **Run tests** to validate

## File Structure

```
tests/skills/{skill-name}/
├── prompts.json       # Trigger phrase tests
├── README.md          # This file (customize for your skill)
└── fixtures/          # Optional test data
```

## prompts.json Format

```json
{
  "skillName": "your-skill-name",
  "shouldTrigger": [
    "Prompts that SHOULD activate this skill",
    "Include variations users might ask"
  ],
  "shouldNotTrigger": [
    "Prompts for OTHER skills",
    "Unrelated questions"
  ]
}
```

## Running Tests

```bash
# From repo root
dotnet test --filter "Name~SkillTrigger"

# Or run the token checker
pwsh scripts/check-skill-tokens.ps1 -SkillName your-skill-name
```

## Test Categories

### 1. Trigger Tests
Validate that prompts correctly match (or don't match) the skill.

### 2. Metadata Tests  
Validate SKILL.md has required frontmatter (name, description).

### 3. Token Budget Tests
Validate skill content is under limits.

### 4. Integration Tests (Manual)
Validate the full workflow completes successfully.

## Checklist for New Skill Tests

- [ ] At least 5 prompts that SHOULD trigger
- [ ] At least 5 prompts that should NOT trigger
- [ ] Skill name matches folder name exactly
- [ ] All tests pass locally
