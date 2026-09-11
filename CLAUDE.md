# {{REPOSITORY_NAME}}

*One line on what this system does.* See `INTENT.md` for what the system does and
`Documentation/Design/Design.md` for how it is designed. `DEVELOPERS.md` walks a person
through the same workflow end to end — the four roles, the documentation layout,
and how a mockup becomes a design section, an issue, and merged code.

## Before this repository is real

The setup checklist lives in `README.md`, under "After creating this repository".
That is the one list — this file does not keep a second copy that would drift from
it. Delete this section once you have worked through it.

One item on it still matters to you before anything else: **`INTENT.md` does not
exist**, so there is no prose statement of what this system is for. Treat a
question about purpose as unsettled rather than inferring it from whatever code
happens to be here.

The design itself is real. It lives in `Documentation/Design/` as four
area-scoped files — `Design.md` (solution, `SOL`), `Abstractions.md` (`ABS`),
`ApiBible.md` (`APB`) and `YouVersion.md` (`YVN`) — and `Design.md` carries the
conventions the other three follow.

## Where the rules live

- **The Standard** — `.claude/skills/the-standard-*`. These own the layer model,
  naming, testing discipline, and the commit, branch and PR formats. Load the
  skill for the layer you are working in rather than working from memory.
- **The design** — `Documentation/Design/` on main is authoritative, starting at
  `Design.md`. Sections are flat and area-prefixed (`§ABS6`, `§APB14`), so cite
  them by prefix. An issue that disagrees with it is stale intent, not an
  instruction; correct the issue.
- **The CI gates** — `.github/workflows/prLinter.yml` holds the authoritative PR
  title prefixes and fails any PR whose body links no issue or task. `Closes
  #<n>` is the preferred form; `fixes`/`resolves` (and their past-tense
  variants) and `AB#<n>` are also accepted — see the workflow for the exact
  pattern.
- **The labels** — `.github/labels.json` is the org label set and
  `.github/workflows/labels.yml` applies it. A PR title prefix that has no label
  in the manifest is a prefix the linter cannot label, so the two are edited
  together.

## Development workflow

Non-trivial work moves through four roles, defined in `.claude/agents/`. Each
hands over a durable artifact, not a conversation.

1. **architect** — settles layer placement, event contracts and the security
   boundary, recorded in `Documentation/Design/Design.md`. Skip only for changes
   touching a single file, no schema, no event and no boundary.
2. **analyst** — writes numbered acceptance criteria into the GitHub issue.
   Requires approval before the developer starts.
3. **developer** — test first, `-> FAIL` then `-> PASS`, one criterion at a time.
4. **qa** — verifies the diff against the criteria in a fresh context. Reports
   BLOCKING and ADVISORY findings. Never fixes anything.

Failed QA goes back to whoever owns the finding: implementation to the developer,
missing or contradictory criteria to the analyst, a crossed boundary or a wrong
layer to the architect.

Every issue carries a `Model - Effort` line in its body and the matching label
spelled out in full, such as `Opus 5 - Medium`.

## Non-negotiables

- No production code without a failing test that demanded it, committed as
  `{TestName} -> FAIL` before the implementation.
- Where this repository carries events, identity travels on the signed event
  envelope, never an ambient accessor, and an identity-filtered read never
  decides an invariant.
- Brokers hold no logic and get no unit tests.
- No layer calls two layers below it — **except** an orchestration depending
  only on foundation services (never a mix of foundation and processing, and
  never a broker). `.claude/agents/architect.md` and `qa.md` enforce that as
  "same kind, never mixed", and deliberately override `the-standard-orchestrations`'
  blanket ban on it — the reasoning is in those two files.
- Schema changes are new migrations. Applied migrations are never edited, and a
  migration script must work as a single batch on the deploy path.
- Never add AI or assistant attribution to a commit message or PR description — it
  blocks the merge.
- Never implement behaviour that is not in an approved criterion.
- Adding a dependency, an event, or a layer change is an architect decision.

## Commands

Run from the repository root. `.github/workflows/build.yml` is the authoritative
list — this section describes it rather than competing with it.

*Trim to what exists here:*

- Build: `dotnet build`
- All unit tests: `Get-ChildItem -Filter "*Tests.Unit*.csproj" -Recurse | % { dotnet test $_.FullName }`
- All acceptance tests: same pattern with `*Tests.Acceptance*.csproj`
- All integration tests: same pattern with `*Tests.Integration*.csproj`
- React: `npm run lint`, `npm run test`, `npm run build` from the app's own
  directory.

`build.yml` discovers test projects by glob, so name them so a recursive
`*Tests.Unit*.csproj` or `*Tests.Acceptance*.csproj` match finds them and CI picks
up a new test project without anyone editing the workflow. Integration tests are
deliberately not discovered — they need live credentials and are a local guard
only.

**`.github/workflows/*.yml` are build output. Edit
`Glory2Him.BibleProviders.Infrastructure` and regenerate; never hand-edit the
YAML** — a hand-edit is reverted silently by the next regeneration, with no
conflict and no warning. Design §SOL7 rule 10.

## Worktrees

The stash stack is shared across worktrees and other sessions may use it
concurrently. Never use bare `git stash` or `git stash pop`; prefer a WIP commit,
or `git stash push -u -m "<unique-tag>"` and apply by SHA.
