# Developing in this repository

How work moves from an idea to merged code here, and how to drive the four
Claude Code agents that do most of it.

`CLAUDE.md` is the short version an agent loads automatically. This is the long
version for a person: it explains the same workflow, plus the parts an agent
never sees — where a mockup goes, how to brief a fresh session, and which
conventions are enforced by tooling rather than by good intentions.

Read `INTENT.md` for what the system is for, and `Documentation/Design/Design.md` for
how it is designed. In a repository created from the template `INTENT.md` does not
exist yet and the design document is a stub carrying the conventions and no
design — writing both is the first real work.

---

## 1. The pipeline

```
mockup            (UI work only — Documentation/Mockups/)
   ↓
architect         settles layer, entities, events, storage → writes the design section
   ↓
analyst           turns the design into numbered acceptance criteria → writes them into a GitHub issue
   ↓
qa                checks the issues cover the design — coverage, completeness, size
   ↓
YOU               read the criteria, apply `status: ready-for-dev`
   ↓
developer         test first, one criterion at a time → commits, branch, PR
   ↓
qa                adversarial verification against the criteria → BLOCKING / ADVISORY findings
   ↓
YOU               merge, or send the findings back to whoever owns them
```

**Every arrow is yours.** The agents do not hand work to each other — none of
them can invoke another, because none has a Task tool. You are the only thing
that moves work between roles, and the artifact each role leaves behind is the
whole of the handoff. Two of the rows are also decisions only you can make:
approving the criteria, and merging.

The work does not have to start with an issue. Where a mockup or a design comes
first, the architect writes sections tagged `(needs issue)` and the analyst's
sweep mode opens the issues from them — that is the worked example in §8.

Skip stages deliberately, not by accident:

| Stage | Skip it when |
| --- | --- |
| mockup | there is no UI surface |
| architect | one file, no schema, no event, no layer boundary crossed |
| analyst | never — the developer refuses an issue with no approved criteria |
| qa on the issues | the feature is one issue and every criterion is obviously a test name — **never** when a feature spans more than one issue |
| developer | never |
| qa on the work | never for anything that ships |

---

## 2. Every agent runs in a fresh session

This is the rule people get wrong most often, so it is stated before anything
else.

**Each role is a separate session with an empty context.** The architect does
not remember writing the design when the analyst runs. The developer cannot see
the analyst's reasoning, only what the analyst wrote down. QA is deliberately
given a fresh context so it argues with the code rather than with the
developer's summary of it.

That has one consequence worth internalising: **if it is not in the artifact, it
does not exist.** A decision made in conversation with the architect and not
written into the design document is lost the moment that session ends.

### What each role leaves behind

| Role | Durable artifact | Where the next role reads it |
| --- | --- | --- |
| architect | a design section | `Documentation/Design/*.md` — four area-scoped files |
| analyst | numbered acceptance criteria | the GitHub issue body, under `## Acceptance criteria` |
| developer | commits, a branch, a PR, a handoff report | the PR and its diff |
| qa | BLOCKING / ADVISORY findings | its final report — on the issue when it reviews criteria, on the PR when it reviews code |

### How to brief a fresh session

Give the agent three things: **the role, the issue number, and where to read.**
Everything else it can find for itself.

```
Act as the architect. Read issue #512 and settle the design for it.
```

```
Act as the analyst. Issue #512 now has a design section at
Documentation/Design/Design.md §12. Write acceptance criteria into the issue.
```

```
Act as the developer. Implement issue #512. The criteria are approved and the
issue carries `status: ready-for-dev`.
```

```
Act as QA. Verify PR #520 against the acceptance criteria on issue #512.
```

The agent will read the issue, the design and the code itself. Do not paste the
previous session's transcript in — if an agent needs something to do its job and
cannot find it, that is a signal the artifact is incomplete, and the fix is to
improve the artifact rather than to narrate it.

#### Two briefs that need more than a pointer

Both hand the architect a **source** instead of an issue to read. Say what the
source is, how much authority it carries, and what you want out of it — otherwise
the architect has to guess whether it is describing a decision or proposing one.

**From a Claude Design mockup.** The job is to turn a picture into words, because
a picture cannot become a test name:

```
Act as the architect. Issue #512 has Claude Design mockups at
Documentation/Mockups/saved-searches/ — panel.html for the interaction and
panel.webp for the screens, with the images embedded in the issue body. Open the
HTML, not just the image: hover states, spacing and the real DOM are in there.

Settle the UI design and write it into Documentation/Design/Design.md as a numbered
section tagged (#512). Name the component boundaries, the state each owns, the
events they raise and what the server re-decides regardless of what the client
shows.

Anything you cannot state in words is not design yet. Say so rather than citing
the picture — the analyst has to write criteria from your section alone, and
"matches the mockup" is not a criterion.
```

When it is done, go back and add the **Superseded by** line to the mockup folder's
README (§5.1). The design section is authoritative from that moment and the mockup
is history.

**From a design written somewhere else.** Porting an existing document — a sketch,
a specification, a wiki page, a design carried over from another repository:

```
Act as the architect. There is an existing design at
Documentation/Imported/legacy-search-spec.md, written before this repository
existed. It describes behaviour we intend to keep, but it is a source input and
not an authority.

Port what still applies into Documentation/Design/Design.md in this document's
conventions: numbered sections, numbered rules within them, and every heading
tagged (#512) or (needs issue). Do not restate it wholesale — the parts that no
longer apply must not survive the move just because they were written down once.

Where it disagrees with what is already in Documentation/Design/Design.md, this
document wins, and say so explicitly in the section rather than silently
choosing. List at the end what you deliberately dropped and why, and anything
you could not verify against the code — a claim you could not check is not a
design decision you can make on its behalf.
```

Record where it came from. A ported section that does not name its source reads
as a decision someone made here, and the next person cannot tell which parts were
inherited and which were chosen.

### Set the developer's model and effort before you invoke it

`.claude/agents/developer.md` pins no `model:` and no `effort:` on purpose. The
issue's `Model - Effort` label is the decision, made per issue rather than per
role — but **nothing in this repository reads that label and configures a
session.** No hook, no script, no mechanism. You set it by hand, before you
invoke the developer, because a session cannot change its own model once
running.

The developer can only detect the mismatch afterwards and stop. An issue with no
`Model - Effort` label is not ready to start.

The other three roles are pinned in their own files: architect and analyst run
`opus` / `high`; QA runs `opus` / `max` deliberately, so the reviewer is never
reasoning less hard than the implementer did.

---

## 3. The four agents

Defined in `.claude/agents/`. Each file is the authority on its own role; this
section tells you when to reach for which.

### architect — shape, not syntax

**Owns** layer placement, entity count, event contracts, the security boundary,
storage and migration shape.

**Produces** an update to the design document, in the section that already owns
the subject. Nothing else. "No design needed, hand to the analyst" is a valid
output and you should expect it often.

It settles seven things in order: the problem in one paragraph, layer placement,
**the entity count** (this is what decides the layer — one entity means
foundation or processing, two or three means orchestration, more than three is a
violation), event contracts in `<Subject>-<Verb>` form, storage and migration
shape including the seed consequence, risks split into reversible and not, and an
explicit out-of-scope list.

**Use it** before any non-trivial implementation, and again afterwards to review
whether the structure held — that second mode reports only structural findings,
marked BLOCKING or ADVISORY.

**Skip it** for a change touching a single file with no schema, no event and no
boundary crossed.

It never writes production code and never fixes defects.

### analyst — criteria, not code

**Owns** turning intent into acceptance criteria precise enough that a developer
can write a failing test from them without asking a question.

**Produces** criteria written into the GitHub issue body with `gh issue edit`,
under an `## Acceptance criteria` heading. **The issue is the spec.** There is no
parallel spec file, deliberately — a second document would drift from the issue.

Aim for five to eight criteria; ten is a ceiling, not a target. If it is past ten
and still on the happy path, the issue needs splitting, and the analyst will stop
and propose the split mid-draft rather than write a criteria list nobody can
finish.

Every criterion must be expressible as a single test name. If you cannot imagine
the test name, the criterion is not finished.

**Use it** for every piece of work, including work that skipped the architect.

It has no `Edit` and no `Write` tool — it changes the issue through `gh`, and
touches no file in the repository.

### developer — test first, one criterion at a time

**Owns** implementation. **Produces** commits, a branch, a PR, and a written
handoff naming the criteria implemented, the tests covering each, any migrations
added, and the commit SHAs — ending in its own verdict line, `MERGE READY: YES`
or `MERGE READY: NO`, which judges only whether its work is done and never
whether a human has approved it.

The loop per criterion is: write one test, run it and confirm it fails **for the
right reason**, commit it as `{TestName} -> FAIL`, implement the smallest change
that passes, commit as `{TestName} -> PASS`. No production code exists without a
failing test that demanded it.

**Use it** only when the issue carries approved criteria. It is the only agent
with a `Write` tool.

### qa — adversarial, never fixes

**Owns** finding the reasons a change should not ship. **Produces** findings
marked BLOCKING or ADVISORY. It never fixes anything, deliberately: the person
who broke it should fix it, and a reviewer who patches defects stops looking for
more.

It assumes the developer's summary is optimistic and verifies against the code.
Always run it in a fresh session — that is the whole point of it.

It has a **second mode**, defined in its own agent file: reviewing **the issues
before any code exists**, which is §8 step 4. Does the design have an issue behind
every section, do those issues together capture the whole feature, is any of them
too big, can every criterion become a test name. Where a feature needed more than
one issue that review is mandatory — nothing else in the pipeline ever asks
whether the set is complete. Say which mode you want when you brief it; verifying
a diff is the default.

**Route failures by owner**: implementation defects to the developer, missing or
contradictory criteria to the analyst, a crossed boundary or wrong layer to the
architect.

---

## 4. The Documentation folder

```
Documentation/
  Design.md                the design document — numbered sections, cited from code
  Design/                  area-scoped design documents, once one file is not enough
  Mockups/                 Claude Design exports awaiting or feeding a design section
  Images/                  static visual assets referenced from issues and design docs
```

`Documentation/Design.md` started as a single file. **That split has happened.**
The design is now four area-scoped files under `Documentation/Design/`:

| Area | Prefix | File |
| --- | --- | --- |
| Solution overview | `SOL` | `Design.md` |
| Provider contract | `ABS` | `Abstractions.md` |
| API.Bible provider | `APB` | `ApiBible.md` |
| YouVersion provider | `YVN` | `YouVersion.md` |

A new area reserves its prefix in `Design.md`'s header table before its file is
written. Split by area — never by size.

### Why sections carry prefixes once you split

Sections in a split file carry a **flat, prefixed number** — `§EVN1`, `§EVN2` —
rather than restarting at 1, so that a bare citation stays unambiguous once
several `Design/*.md` files exist side by side. Reserve a prefix per area up
front: `ARC`, `DOM`, `EVN`, `SEC`, `UI`.

A relocated section keeps a `(formerly §10.X)` annotation naming its old
position, so code comments citing the old number still resolve by grep.

Two cautions worth inheriting rather than rediscovering:

- **Resolving is not the same as being right.** The annotation maps an old number
  to a new one; it says nothing about whether the section was the correct one to
  cite originally.
- **Nothing validates citations.** No CI step, no script. The guarantee that an
  old `§10.X` still resolves is the annotation convention and nothing else.

### Citing design from code

Code comments cite design sections constantly, and this is the main reason the
numbering discipline matters:

```csharp
// design §14.6 rule 2: either service must be safe when called alone
// (§EVN2 rule 4, §EVN18(a))
```

---

## 5. From a Claude Design mockup to a design section

UI work usually starts as a picture. The job of this stage is to get the picture
into the repository and then **out of the critical path**, because a picture is
not a test name and a mockup left as a second source of truth will eventually
contradict the design.

### 5.1 Put the export in the repository

A Claude Design export is a single self-contained HTML file. Save it to:

```
Documentation/Mockups/<feature-slug>/<screen-name>.html
```

Alongside it, save a **flattened image** of each screen — `.webp` or `.png` — in
the same folder. The image is what you embed in the issue; the HTML is what
someone opens when they need to see hover states, spacing or the real DOM.

Bundled exports run to several megabytes. Keep the HTML when the interaction
matters and the image alone when it does not, and do not commit five versions of
the same screen because a design iterated.

Every mockup folder gets a `README.md` with three lines: what it shows, the issue
it came from — or `Issue: none yet, this started the work` when the mockup came
first — and, once the architect has written the design, **the design section it
produced**. That last line is what stops the mockup becoming a rival
spec:

```markdown
# Content item search panel
Source: Claude Design export, 2026-05-04. Issue: #37.
Superseded by the design at `Documentation/Design/Design.md` §14 — that section wins
wherever the two disagree.
```

Commit it on its own, with a `DOCUMENTATION:` prefix.

### 5.2 Embed it in the issue by pinned commit

When you reference an image from a GitHub issue, use a **raw URL pinned to the
full 40-character commit SHA**, never a branch:

```markdown
![Cards](https://raw.githubusercontent.com/<owner>/<repo>/<40-char-sha>/Documentation/Images/ContentItemSearchPanel/redesign-cards.png)
```

Pinned to a SHA, the picture in the issue cannot change under it later. Pair the
images with prose, a component tree and an event-hook table — that is the level
of written detail the analyst needs to write criteria from.

Drag-and-dropping an image into the GitHub comment box also works and is hosted
by GitHub, but it lives nowhere in the repository. Use it for a throwaway
annotation, not for the spec.

### 5.3 Ask the architect to turn it into a design section

```
Act as the architect. Issue #512 has a mockup at
Documentation/Mockups/saved-searches/ and images embedded in the issue body.
Settle the UI design for it and write it into the design document.
```

The architect writes the section, numbers it, and tags the heading (§6). From that moment the design section is authoritative and the mockup
is history — go back and add the "Superseded by" line to the mockup's README.

### 5.4 Then the analyst writes criteria in words

```
Act as the analyst. Issue #512's design is at Documentation/Design/Design.md §14.
Write acceptance criteria into the issue.
```

Criteria must be derived from the design in words. "Matches the mockup" is not a
criterion, because it cannot be a test name.

---

## 6. Linking design and issues

Two mechanisms, deliberately different, answering two different questions.

### Heading tags — "what issue defines this section?"

Every numbered heading in the design document carries exactly one of two tags,
never bare:

```markdown
## 14. Saved searches panel (#512)
## 15. Search result density (needs issue)
```

`(#N)` names the **most recent** issue that authoritatively defined the section —
not an accumulating list, because `git log` and `git blame` already give the full
history for free. `(needs issue)` is an explicit, greppable flag for design
content nobody has scheduled yet.

The tag is mandatory rather than inferred, because a bare heading is ambiguous:
deliberately skipped, or just missed? Requiring a tag forces the decision every
time a section is touched. **The architect sets these**, and may not leave a
heading bare when it writes or substantially expands a section.

### Area labels — "show me everything that touched this area"

One label per design area — `design: events`, `design: ui` — applied by the
analyst when it writes an issue's criteria. That gives a live query that never
goes stale, because it is GitHub's own index:

```bash
gh issue list --label "design: events" --state all
```

### Sweep mode — generating issues from the gaps

The analyst has a second way in. Instead of "turn this feature description into
criteria", point it at the design documents:

```
Act as the analyst in sweep mode. Find design sections with no issue behind them
and propose issues for them.
```

It runs:

```bash
grep -rn "^## .*(needs issue)" Documentation/Design/*.md
```

and for each hit does exactly what it does for a human-described feature — the
size check, splitting if too big, criteria into a new issue, the `Model - Effort`
label, the area label — then flips the heading tag from `(needs issue)` to
`(#<new-issue-number>)`. Same skill, different starting point.

---

## 7. Approval is a label

There is no PR-gated approval for a spec, and no approval file. Approval is a
label on the issue, applied by you:

```
status: needs-scoping → status: ready-for-dev → status: in-progress → status: in-qa → status: done
```

The analyst leaves an issue at `status: needs-scoping`. The criteria review in §8
step 4 happens while the issue sits there — it needs no label of its own. You read
the criteria and, when satisfied, apply `status: ready-for-dev` by hand. That is the same judgement
a PR approval would have expressed, as a label toggle instead of a merge.

**The developer's hard rule: never start without `status: ready-for-dev`.** QA's
verdict says which label should come next — `status: done`, or back to
`status: in-progress` on a BLOCKING finding.

**The honest trade-off:** a label has a thinner audit trail than a PR review. To
see who changed a status and when, read the issue's timeline:

```bash
gh api "repos/<owner>/<repo>/issues/512/timeline?per_page=100" \
  --jq '.[] | select(.event=="labeled" or .event=="unlabeled") | "\(.event) \(.label.name) by \(.actor.login) at \(.created_at)"'
```

`gh issue view --json timelineItems` does **not** work — there is no timeline
field on that command. Use the REST endpoint above.

---

## 8. A worked example

"Add a saved-searches panel." UI work, and **there is no issue yet** — someone has
a picture and an intention. The design comes first and the issues fall out of it.

**1 — Mockup.** Export from Claude Design, save to
`Documentation/Mockups/saved-searches/panel.html` plus `panel.webp`, write the
folder README — what it shows, and that no issue exists yet — and commit:

```
DOCUMENTATION: Add The Saved Searches Panel Mockup
```

**2 — Architect.** There is no issue to point it at, so point it at the mockup
(§2 has the long form of this brief):

```
Act as the architect. There are Claude Design mockups at
Documentation/Mockups/saved-searches/ and no issue yet. Open the HTML as well as
the images, extract the requirements, and write the design into
Documentation/Design/Design.md. Tag every heading you add (needs issue).
```

It writes §14 for the panel and §15 for persisting a saved search, both tagged
`(needs issue)`, and commits with a `DESIGN:` prefix. Then go back and add the
**Superseded by** line to the mockup folder's README.

**That tag is what makes the next step possible.** A bare heading is invisible to
the sweep, and the work is then only in someone's memory.

**3 — Analyst, in sweep mode.** It has no issue to read either — the design is its
input:

```
Act as the analyst in sweep mode. Find design sections tagged (needs issue) and
propose issues for them.
```

It greps, finds §14 and §15, runs the size check on each, and opens **issue #512**
for the panel and **#513** for the persistence. Each gets numbered criteria, a
`Model - Effort` line and matching label, `design: ui`, and
`status: needs-scoping`. It rewrites the headings to
`## 14. Saved searches panel (#512)` and `## 15. Saving a search (#513)`.

**One section, one issue.** If the size check splits a section's work across two
issues, split the section to match. A heading carries exactly one tag, and a
section that spawned three issues cannot honestly say which one defines it.

**4 — QA, on the issues.** Before a line of code exists. The unit of review here
is the **feature**, not one issue:

```
Act as QA, reviewing the issues rather than a change. The saved-searches feature
is designed at Documentation/Design/Design.md §14 and §15, and the analyst has logged
issues #512 and #513 against them. There is no code yet — do not look for any.
```

That is the whole brief. `.claude/agents/qa.md` defines the mode and carries the
checklist — coverage, completeness across the feature, size, criteria quality,
heading tags and the `Model - Effort` label — so you name the feature, the
sections and the issues, and say there is no code. Naming the mode matters: the
default is verifying a diff, and it will go looking for one.

**Where a feature needs more than one issue, this step is not optional.** One
section, one issue is a *mechanism* — it makes each section traceable. It is not a
guarantee that the sections between them describe the whole feature. The analyst
sized each issue in isolation, and nothing has yet asked whether the set is
complete. That question is this step's reason to exist.

QA reports two BLOCKING findings:

- §15 covers both saving and deleting a saved search, but #513's criteria only
  cover saving. Deletion is in the design and in no issue.
- Criterion 4 on #512 says the panel "feels responsive", which cannot become a
  test name.

Both route to the analyst (§3). It splits §15 into §15 *Saving a search* `(#513)`
and §16 *Deleting a saved search*, tags §16 `(needs issue)`, sweeps again to open
**#514** for it, and rewrites criterion 4 as something assertable.

**5 — You sign it off.** Read the criteria yourself: QA advises, you decide. Apply
`status: ready-for-dev` to the issue you want built first — #512 here — and leave
#513 and #514 at `status: needs-scoping` until you are ready for them. **That
label is the approval**; the developer refuses an issue without it.

**6 — Developer.** Set the session model and effort to match the issue's
`Model - Effort` label first; nothing does this for you. Fresh session: *"Act as
the developer. Implement issue #512. It carries `status: ready-for-dev`."* It
branches `users/<your-handle>/components-savedsearches-add`, then per criterion
commits `ShouldRenderSavedSearchesPanelAsync -> FAIL` followed by
`ShouldRenderSavedSearchesPanelAsync -> PASS`, and opens a PR titled:

```
COMPONENTS: Add A Saved Searches Panel
```

with `Closes #512` in the body. You move the issue to `status: in-progress`.

**7 — QA, on the work.** A *different* fresh session from step 4 — carrying the
criteria review's context into the code review is exactly what fresh contexts are
for: *"Act as QA. Verify PR #520 against the acceptance criteria on issue #512."*
Move the issue to `status: in-qa`. QA reports two ADVISORY findings and no
BLOCKING ones.

**8 — Merge**, and set `status: done`. Issues #513 and #514 still sit at
`status: needs-scoping` with criteria written and already reviewed — they resume
at step 5, not step 4.

### The same example when an issue already exists

Someone files issue #512 describing the panel in prose before any design exists.
Step 2 then points the architect at the issue rather than the mockup, and its
heading is tagged `(#512)` from the start. Step 3 is the ordinary analyst instead
of sweep mode: *"Act as the analyst. Issue #512's design is at
Documentation/Design/Design.md §14. Write acceptance criteria into the issue."*
Everything from step 4 on is identical.

---

## 9. Asking for it — copy-paste openers

### Ask the architect

```
Act as the architect. Read issue #512 and settle the design.
```

```
Act as the architect. Review PR #520 against the design at
Documentation/Design/Design.md §14 and report structural findings only.
```

```
Act as the architect. Issue #512 has Claude Design mockups at
Documentation/Mockups/saved-searches/. Open the HTML as well as the images, and
write the UI design into Documentation/Design/Design.md tagged (#512). Anything you
cannot state in words is not design — say so rather than citing the picture.
```

```
Act as the architect. Port the design at <path> into Documentation/Design/Design.md in
this document's conventions. It is a source input, not an authority — where it
disagrees with what is already there, this document wins. List what you dropped
and why, and anything you could not verify against the code.
```

§2 has the long forms of those last two, with the reasoning.

### Ask the analyst

```
Act as the analyst. Write acceptance criteria into issue #512.
```

```
Act as the analyst in sweep mode. Find design sections tagged (needs issue)
and propose issues for them.
```

```
Act as the analyst. Issue #512 looks too big — check its size and split it if it
needs splitting, before writing criteria.
```

### Ask the developer

**Set the session model and effort to the issue's `Model - Effort` label first.**
Nothing does this for you.

```
Act as the developer. Implement issue #512. It carries `status: ready-for-dev`.
```

```
Act as the developer. Address the QA findings on PR #520. The criteria are on
issue #512.
```

### Ask QA

```
Act as QA. Verify PR #520 against the acceptance criteria on issue #512.
```

```
Act as QA. PR #520 has had a round of fixes since your last pass. Re-verify.
```

```
Act as QA, reviewing the issues rather than a change. The saved-searches feature
is designed at Documentation/Design/Design.md §14 and §15, with issues #512 and #513
logged against them. No code exists yet.
```

That last one is QA's second mode. Name it explicitly — verifying a diff is the
default, and it will go looking for one. §8 step 4 has the reasoning; the
checklist is in `.claude/agents/qa.md`.

---

## 10. Conventions that bite

**Branches.** `users/<your-github-handle>/<category>-<entity>-<action>`, lowercase
after the handle. Claude Code worktree sessions generate `claude/<slug>-<hex>`
branches of their own; those are accepted in practice and several merged PRs came
from them. Never commit to `main`. Contributors do **not** fork — everything is
an in-repo branch, whatever the vendored branching skill says.

**Commits.** TDD work commits as `{TestName} -> FAIL` then `{TestName} -> PASS`.
Everything else is `CATEGORY: Pascal Case Description`.

**PR titles.** Same `CATEGORY:` prefix. The authoritative list of prefixes is in
`.github/workflows/prLinter.yml` — not in any skill, which carries a shorter and
differently-spelled list. Common ones: `FOUNDATIONS:`, `PROCESSINGS:`,
`ORCHESTRATIONS:`, `COMPONENTS:`, `CONTROLLERS:`, `DOCUMENTATION:`, `DESIGN:`,
`CONFIG:`, `CODE RUB:`, `MINOR FIX:`, `MEDIUM FIX:`, `MAJOR FIX:`. There is no
bare `FIX:`.

Be aware of what is actually enforced: the labelling job **never fails** — an
unrecognised prefix is silently left unlabelled. The convention is real, but
tooling will not catch you breaking it.

**PR body.** Must link an issue or the PR linter fails:

```markdown
Closes #512
```

`fixes` and `resolves` (and their past-tense forms) and `AB#<n>` also match. This
is the one PR-linter job that can fail. `Build` is the only status check the
branch ruleset requires green.

**Issue labels.** Every issue carries a `Model - Effort` line as the first line of
the body and the matching label. The label set is not a tidy matrix — these
eleven exist, and nothing else:

| Model | Efforts available |
| --- | --- |
| Opus 5 | Small, Medium, High, Extra, Max |
| Sonnet 5 | Low, Medium, High |
| Fable 5 | Low, Medium, High |

There is no `Opus 5 - Low`, no `Sonnet 5 - Max`, no `Fable 5 - Extra`.

**Never add AI or assistant attribution** to a commit message or PR description.
It trips the unattributed-changes rule and blocks the merge.

**Skills** live in `.claude/skills/` and are vendored from upstream via
`skills-lock.json`. Treat them as read-only and reference them by name. They
arrived as a frozen copy and nothing pulls upstream fixes in, so refreshing them
is a deliberate act. If a skill is wrong for this repository, say so in the
design rather than patching the skill quietly — a locally edited skill is drift,
and its hash in `skills-lock.json` stops matching what it claims to be.

---

## 11. Commands

`CLAUDE.md` carries the list this repository actually uses. Keep the two in step.

```bash
dotnet build

# one suite
dotnet test <Project>.Tests.Unit

# all of a kind
Get-ChildItem -Filter "*Tests.Unit*.csproj" -Recurse | % { dotnet test $_.FullName }

# a JavaScript app, from its own directory
npm run lint && npm run test && npm run build
```

`.github/workflows/build.yml` is authoritative for what CI runs. It discovers
test projects by glob, so a project named `*Tests.Unit*` or `*Tests.Acceptance*`
runs without anyone editing the workflow. It also passes with nothing to do while
the repository has no projects at all.

---

## 12. What this repository still has to create

The workflow above assumes things a new repository does not have on its first
day. Until they exist, the sections that depend on them are description rather
than mechanism:

- ~~**One run of the label sync.**~~ Done — 134 labels exist, including the
  `status:` lifecycle in §7 and the `design: <area>` labels in §6. GitHub runs no
  workflows on the commit that creates a repository, so if a future repository from
  this template looks label-less, that is why: Actions → Labels → Run workflow,
  once. Note the trap either way: an all-caps `DESIGN` label turns up regardless,
  auto-created by the PR linter from a `DESIGN:` title prefix. That is a category
  label on PRs, not an area label on issues.
- ~~**Section 1 of the design.**~~ Done — `Documentation/Design/` holds four
  area-scoped documents (§4). The architect has sections to extend and the analyst
  has design to derive criteria from; the implementation backlog is the
  work-breakdown table at the foot of each file.
- **`INTENT.md`** — what this system is for, in prose.
- **`Documentation/Mockups/`** — created on first use. Its README explains the
  layout.

`README.md` has the full setup checklist, including the parts of this that are
one-off.

Two things will not exist unless someone builds them, here or anywhere:
**nothing validates design citations**, and **nothing reads the `Model - Effort`
label to configure a session**.
