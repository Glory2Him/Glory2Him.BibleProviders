# {{REPOSITORY_NAME}}
![Glory 2 Him](https://raw.githubusercontent.com/Glory2Him/Glory2Him/main/Resources/Images/Glory2Him-Banner.png)
---

## ✅ After creating this repository

Work through this, then **delete this whole section**. Everything here is
something the template cannot do for you — GitHub copies files, not settings,
and no file can know what this repository is for.

**First — run the label sync**

GitHub runs **no workflows at all** on the commit that "Use this template" creates.
Nothing fires. Until you do this by hand, this repository has no label set and the
PR linter, the analyst and the status lifecycle all have nothing to work with.

- [ ] Actions → Labels → Run workflow.

**Read first**

- [ ] `DEVELOPERS.md` — how work moves from an idea to merged code here, and how to
      drive the four agents. Its §12 lists what this repository still has to create
      before the workflow it describes is real rather than aspirational.

**Names and placeholders**

- [ ] Replace `{{REPOSITORY_NAME}}` in this file, in `CLAUDE.md` and in `LICENSE.txt`.
- [ ] Replace `{{YEAR}}` in `LICENSE.txt` with the year this repository was created.

**Say what this repository is**

- [ ] Fill in *What is {{REPOSITORY_NAME}}?*, *Key Features* and *Getting started* below.
- [ ] Write `INTENT.md` — what this system is for, in prose, before any of it exists.
- [x] ~~Fill in the design.~~ Done: `Documentation/Design/` holds four area-scoped
      documents — `Design.md` (`SOL`), `Abstractions.md` (`ABS`), `ApiBible.md`
      (`APB`) and `YouVersion.md` (`YVN`). The architect records layer placement and
      boundaries there, and every agent treats it as outranking any issue that
      disagrees with it.

**`CLAUDE.md`**

- [ ] Fill in the one-line description at the top.
- [ ] Trim **Commands** to what actually exists here.
- [ ] Delete any rule under **Non-negotiables** this repository genuinely cannot
      have — a rule kept for a thing that does not exist is noise. Deleting one
      because it is inconvenient is an architect decision, not a setup step.
- [ ] Delete its *Before this repository is real* section once the above are done.
- [ ] Keep **Commands** in step with `DEVELOPERS.md` §11 — they describe the same
      thing and will drift if only one is updated.

**CI**

- [ ] Check Issues → Labels shows the full set. Around twenty base category labels
      arrive with any new repository in this org — it has default labels configured —
      so the sync reports most of its work as created and a handful as already
      correct. `documentation` is renamed to `DOCUMENTATION`.
- [ ] Delete GitHub's other stock labels (`bug`, `duplicate`, `enhancement`, `good
      first issue`, `help wanted`, `invalid`, `question`, `wontfix`) if you do not
      want them. The sync never deletes, so they stay until you remove them.
- [ ] Trim the `design: <area>` labels to the areas this repository will actually
      have. All five reserved areas ship in `labels.json` and the sync creates
      them; it never deletes, so dropping one means editing the manifest *and*
      deleting the label by hand.
- [ ] Confirm the **Build** check reports on your first pull request. It passes with
      nothing to do while the repository has no projects; the org ruleset requires it,
      so a check that never reports blocks the merge.
- [ ] Grow `.github/workflows/build.yml` as code arrives — integration tests and their
      LocalDB start, a JavaScript app's npm steps, the EF migration drift check. The
      comment at the foot of the file lists them.

**GitHub settings — none of these come from a template**

- [ ] Visibility, description and topics.
- [ ] Branch protection or rulesets, if the org-level ruleset does not already cover
      this repository.
- [ ] Secrets, variables and Actions permissions, if the repository needs them.

**Skills**

- [ ] `.claude/skills/` is a frozen copy of the six packs in `skills-lock.json`, taken
      from `hassanhabib/the-standard-skills`. Nothing pulls upstream fixes in — refresh
      deliberately when you want them.

## ✝️ Introduction

**Glory 2 Him** creates software to connect people with God, offering digital tools and resources
that bring faith into *everyday life*.

Our mission is to **encourage and equip every believer** on their journey of faith through
open-source software, tools, and libraries that we develop.

Join our *community of developers and designers*—or, if you don't have technical skills but
see a **digital need**, share it with us. Together, we can discover new ways to serve the
**body of Christ** in meaningful and lasting ways.

---

## 🌄 What is {{REPOSITORY_NAME}}?

*One paragraph on what this repository does and who it is for.*

**Key Features:**
- ✨ *Feature one*
- ✨ *Feature two*

---

## 🛠️ Getting started

*How to build and run this repository.*

```bash
dotnet build
dotnet test
```

---

## 🤝 Contributing

Pull request titles must start with one of the category prefixes listed in
`.github/workflows/prLinter.yml` (for example `MINOR FOUNDATIONS:` or `DOCUMENTATION:`),
and the description must link an issue with `Closes #<n>`. Both are enforced by CI.

---

## 📜 License

Licensed under the Glory 2 Him Software License (G2HSL). Two files, deliberately:
[LICENSE.txt](LICENSE.txt) is this repository's copy — the licence with the
repository's own name and copyright line on top — and [G2HSL.md](G2HSL.md) is the
licence on its own, unmodified. The canonical copy lives at
[Glory2Him/Glory2Him](https://github.com/Glory2Him/Glory2Him/blob/main/G2HSL.md).

**FREE TO USE TO HELP SHARE THE GOSPEL**

> John 14:6 (NIV) "Jesus answered, 'I am the way and the truth and the life.
> No one comes to the Father except through me.'"
> [john.bible/john-14-6](https://john.bible/john-14-6)

If Jesus is who He said He is, what does that mean for you, today?
