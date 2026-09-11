# Design

> **Nothing has been designed yet.** This file is a stub carrying the conventions
> so the first section inherits them instead of inventing them. Delete this block
> when you write section 1.

How this system is built: layer placement, entity boundaries, event contracts,
the security boundary, storage and migration shape. `INTENT.md` says what the
system is for; this says how it is put together.

**This document is authoritative.** An issue that disagrees with it is stale
intent, not an instruction — correct the issue. While this file is still a stub
there is no design to check anything against, so a design question is unsettled
rather than answerable from whatever code happens to exist.

The architect writes here. Nobody else does.

---

## Conventions

**Numbered sections.** Sections are numbered and stay numbered — `## 1.`, `## 2.`
— because code comments cite them by number:

```csharp
// design §14.6 rule 2: either service must be safe when called alone
```

Number rules within a section too, so a citation can be precise about which one
it means.

**Every heading carries exactly one tag, never bare:**

```markdown
## 14. Saved searches panel (#512)
## 15. Search result density (needs issue)
```

`(#N)` names the **most recent** issue that authoritatively defined the section —
not an accumulating list, because `git log` and `git blame` already give the full
history. `(needs issue)` is an explicit, greppable flag for design content nobody
has scheduled yet, and is what the analyst's sweep mode looks for:

```bash
grep -rn "(needs issue)" Documentation/Design.md Documentation/Design/*.md
```

The tag is mandatory rather than inferred, because a bare heading is ambiguous —
deliberately skipped, or just missed? Requiring the tag forces the decision every
time a section is touched.

**If this file is ever split** into `Documentation/Design/*.md`, split by area —
architecture, domain, events, security, UI — never by size, and give each area a
reserved prefix (`ARC`, `DOM`, `EVN`, `SEC`, `UI`). Sections in a split file are
numbered **flat and prefixed** — `§EVN1`, `§EVN2` — rather than restarting at 1,
so a bare citation stays unambiguous with several files side by side. Annotate
every relocated section with `(formerly §10.X)` so code comments citing the old
number still resolve by grep.

Nothing validates citations — no CI step, no script. The annotation convention is
the whole guarantee.

`DEVELOPERS.md` §4 and §6 are the long version of all of this.

---

## 1. *(your first section)* (needs issue)

*What it is, what decides it, and what it rules out. The architect settles seven
things in order: the problem in one paragraph, layer placement, the entity count,
event contracts in `<Subject>-<Verb>` form, storage and migration shape including
the seed consequence, risks split into reversible and not, and an explicit
out-of-scope list.*
