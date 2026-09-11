# Mockups

Claude Design exports, and any other visual mockup, on their way into a design
section. `DEVELOPERS.md` §5 describes the full flow.

## Layout

```
Documentation/Mockups/<feature-slug>/
  <screen-name>.html     the Claude Design export, self-contained
  <screen-name>.webp     a flattened image of the same screen
  README.md              what it shows, the issue, and the design section it produced
```

Keep the HTML when the interaction matters — hover states, spacing, the real DOM.
Keep the image either way: it is what gets embedded in the GitHub issue, because
a multi-megabyte bundled HTML file does not render in an issue body.

## A mockup is never the spec

Once the architect has written the design section, that section is
authoritative and the mockup is history. Record it in the folder README the
moment it happens:

```markdown
# Saved searches panel
Source: Claude Design export, 2026-09-11. Issue: #512.
Superseded by the design at `Documentation/Design/Design.md` §14 — that section wins
wherever the two disagree.
```

A mockup left without that line is a second source of truth waiting to
contradict the design.

## Referencing an image from an issue

Pin the raw URL to the full 40-character commit SHA, never a branch, so the
picture in the issue cannot change under it later:

```markdown
![Panel](https://raw.githubusercontent.com/<owner>/<repo>/<40-char-sha>/Documentation/Mockups/saved-searches/panel.webp)
```
