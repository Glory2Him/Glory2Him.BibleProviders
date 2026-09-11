# What this is for

Applications that put scripture in front of people should not be limited by which
Bible API their developer happened to pick first.

That is the whole of it. A church website, a devotional app, a sermon tool — each
needs a verse, in a translation a reader can actually understand, rendered
faithfully, with the copyright the publisher is owed. None of them should have to
learn that API.Bible calls it `de4e12af7f28f599-02` and YouVersion calls it `111`,
or discover on a Sunday morning that a free tier ran out on the Friday.

`Documentation/Design/` says how that is built. This says what it is for, and why
it is shaped the way it is.

---

## Who it serves

**Directly: a .NET developer** building something that displays scripture, who
wants to ask for `John 3:16` and get it — and who should not have to become an
expert in two upstream APIs, two licensing regimes and two failure vocabularies to
do so.

**Indirectly, and this is the part that decides the design: the publishers whose
work is being served, and the readers receiving it.** Neither is a user of this
library. Both are affected by every decision in it.

---

## Why so much of the design is about obligations

Scripture in a modern translation is licensed material. Publishers grant that
licence on terms — report how often a translation is read, display the copyright,
refresh what you store, do not alter a word of it — and those terms are how the
arrangement between publishers and the church stays workable.

A library that made those terms easy to ignore would be actively corrosive. Not
because anyone intends to breach them, but because the breach is silent: nobody
notices an unreported display, a dropped copyright line, or a cached verse that
went stale eighteen months ago. There is no error, no failing test, and no symptom
until a rights holder asks a question nobody can answer.

So this library is built to make the obligations **hard to lose and impossible to
not notice**, and that is the reason behind a set of decisions that would
otherwise look like ceremony:

- **The usage token and the attribution are `required` members**, not optional
  metadata. Dropping one is a compile error rather than a discovery.
- **`Attribution` is required *and* nullable.** You must decide what to do when a
  publisher supplied none. You may not simply not think about it.
- **The library refuses to report usage on your behalf**, even though it easily
  could. Reporting is owed per *display*, and a fetch has no viewer — a
  "report on fetch" switch would look like compliance while being none.
- **No copyright table ships in the package.** It would go stale inside a binary
  nobody can correct without a release, and a stale notice presented as
  authoritative is worse than an honest gap.
- **The text passes through unaltered, character for character.** Not a licence
  clause first and foremost — it is scripture, and it is not ours to tidy.

If you are reading this while deleting one of those, that is the argument you are
arguing with.

---

## What must never go wrong

Three things, in order. Everything else is a preference.

1. **The words must be right.** The correct verse, in the correct translation,
   reproduced exactly. A wrong verse served confidently is worse than an error,
   because nothing downstream will catch it.
2. **The publisher must get what they are owed.** Attribution displayed, usage
   reported where it is owed, retention honoured.
3. **It should keep working.** One upstream being rate limited, out of quota or
   down should not take scripture off a page, which is why failure tells you
   *whether asking someone else would help*.

---

## What this is not for

- **It is not a Bible.** It holds no scripture of its own and stores none. It
  fetches, shapes and hands over.
- **It is not a content management system.** Storing passages, deciding retention,
  and reporting displays belong to the application, because they depend on
  agreements and a database this library does not own.
- **It is not a translation tool.** It serves editions as published, and never
  maps a reference from one edition's numbering to another's.
- **It is not a general-purpose HTTP client** for either upstream. It exposes
  scripture lookup, not the whole of an API surface.

---

## A note on this file

This was written by the architect from what the repository already shows — the
licence it ships under, the priorities visible in the design, and the shape of the
two upstream agreements. It is a reading, not a statement from the people whose
project this is.

**If any of it is wrong, this file is the one to correct**, not the design that
cites it. Particularly: who the first consuming application is, and whether this is
intended for Glory2Him's own use or for anyone who finds it.

---

**FREE TO USE TO HELP SHARE THE GOSPEL**

> John 14:6 (NIV) "Jesus answered, 'I am the way and the truth and the life.
> No one comes to the Father except through me.'"
> [john.bible/john-14-6](https://john.bible/john-14-6)
