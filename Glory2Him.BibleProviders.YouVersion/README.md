# Glory2Him.BibleProviders.YouVersion

![Glory 2 Him](https://raw.githubusercontent.com/Glory2Him/Glory2Him/main/Resources/Images/Glory2Him-Banner.png)

---

> **Pre-release.** The design is complete and reviewed; the implementation is not
> written yet. Samples describe the designed API, not shipped behaviour.

**YouVersion Platform — operated by Life.Church — behind the Glory2Him Bible
provider contract.**

Get an app key at [platform.youversion.com](https://platform.youversion.com). The
platform is free.

**Two things to know before anything else**, because both surprise people:

1. **Your key only sees versions whose licence you accepted in the portal.** This
   is the single most common cause of a confusing `TranslationNotSupported`, and it
   looks identical to a translation that does not exist. See
   [the licence trap](#the-licence-acceptance-trap).
2. **Storing scripture is permitted here, and the platform encourages it** — no
   refresh timer and no usage reporting — but **Biblica gives you 48 hours to
   remove content on written request**, so you still need a delete path. See
   [Storage](#storage--permitted-and-encouraged).

---

## Getting started

```csharp
var configurations = new YouVersionConfigurations
{
    AppKey = "…",                            // required, sent as X-YVP-App-Key
    LanguageRanges = new List<string> { "eng" },   // required upstream
    DefaultTranslation = "WEB",              // resolves to WEBUS, id 206
};

using var provider = new YouVersionProvider(configurations);
using var bibleProvider = new BibleAbstractionProvider(new IBibleProvider[] { provider });

ScriptureResult result = await bibleProvider.GetScriptureByReferenceAsync(
    YouVersionProvider.ProviderName, "John 3:16", cancellationToken);

if (result.IsFound)
{
    Console.WriteLine(result.Passage.Text);
    Console.WriteLine(result.Passage.Attribution);   // you must display this
}
```

The constructor validates eagerly and throws — blank key, **empty
`LanguageRanges`** (the upstream rejects the catalogue call without it), an
unparseable base URL, a timeout budget that does not close.

---

## Configuration

| Field | Default | |
|---|---|---|
| `AppKey` | — | **Required.** From [platform.youversion.com](https://platform.youversion.com). Sent as `X-YVP-App-Key` |
| `LanguageRanges` | `["eng"]` | **Required upstream.** ISO 639-3, in preference order. Also the scope loose references are parsed in |
| `BaseUrl` | `https://api.youversion.com/v1/` | |
| `DefaultTranslation` | `"WEB"` | Fills an unqualified reference. Mapped to `WEBUS` (id 206). **Verify your key can see it** — see below |
| `TranslationMap` | `{"WEB": 206}` | `"NIV"` → a numeric version id. Overrides catalogue lookup, and is authoritative. Ships one entry — see below |
| `TranslationMetadata` | empty | Backfills copyright and publisher links where the catalogue has none |
| `IncludeAllAvailable` | `false` | Widens the listing beyond what your key is licensed for. **Diagnostics only** — see below |
| `MaxStitchedVerses` | 30 | Caps the size of a stitched range |
| `CatalogueCacheDuration` | 6 hours | |
| `TimeoutSeconds` | 20 | Overall budget for one lookup |
| `PerAttemptTimeoutSeconds` | 5 | |
| `MaxRetryAttempts` | 1 | Retries, not attempts — 1 means 2 attempts. Low on purpose: a retry spends quota you cannot get back |

### `LanguageRanges` does double duty

It scopes the catalogue call upstream **and** is the scope in which loose
references are read. That is deliberate: the languages you can serve and the
languages you can parse references in are then the same list by construction. A
Spanish deployment sets `["spa"]` and gets both.

### Why the default is WEB — and why it is spelled `WEBUS` here

The World English Bible is public domain and sits in the portal's **Public Domain
and Creative Commons** group, the one licence row with **no agreement to accept**,
so it is the most plausible translation to be available on any key. It also matches
the ApiBible package's default, so one abbreviation means one thing whichever
provider answers.

**The trap: YouVersion does not call it `WEB`.** It publishes version **`206`**,
"World English Bible, American English Edition, without Strong's Numbers",
abbreviated **`WEBUS`**. A bare `"WEB"` would resolve on API.Bible and return
`TranslationNotSupported` here.

**So this package ships one `TranslationMap` entry, `"WEB"` → `206`.** Ask for
`WEB` and it works; ask for `WEBUS` and that works too. If you replace
`TranslationMap` wholesale, re-add the entry or switch the default to `WEBUS`.

**It replaced `KJV`.** Not because of a YouVersion clause — there is none — but
because rights in the King James Version in the UK are vested in the Crown as a
matter of law rather than of either platform's contract, and defaulting the two
providers to different translations would defeat the point of the abstraction.

**The caveat that applied to KJV still applies.** Access is gated per accepted
licence, so set `DefaultTranslation` to a version your key has actually accepted if
the default does not resolve — otherwise every unqualified reference returns
`TranslationNotSupported`. The provider logs at **Error** on its first catalogue
load if the configured default is absent, so you will know.

---

## The licence-acceptance trap

**Your app key only sees versions whose agreements you accepted in the portal.**
A translation you have not accepted behaves exactly like one that does not exist:
`TranslationNotSupported`, with nothing to distinguish the two.

Acceptance is **per publisher**, not per version — one Biblica agreement covers 69
Bibles including NIV, one Lockman agreement covers NASB and AMP. They live under
**Platform → Licensing** in the portal, and each row links the agreement itself.

So `TranslationNotSupported` from this provider means *"not available to this app
key, in its configured language ranges"* — which is broader than "not licensed" and
broader still than "does not exist". **Support guidance should start with "check
the portal."**

`IncludeAllAvailable = true` widens the catalogue listing from "what this key may
fetch" to the broader platform catalogue, which separates *"exists but you are not
licensed"* from *"does not exist"*. **Use it to answer that question, not as your
normal setting** — with it on, a translation resolves to an id your key cannot
actually read, turning a clean `TranslationNotSupported` into a 403 per lookup.

---

## What can I do with YouVersion content

Permissions vary by the **publisher** whose licence you accepted, not by the
endpoint you called. **The API does not tell you which publisher a translation
belongs to** — the portal groups them, the catalogue does not — so classify them in
configuration and default anything that leaves your application to *off*.

| | Public Domain &<br/>Creative Commons<br/><sub>361 Bibles</sub> | Biblica<br/><sub>NIV, NIrV — 69</sub> | Lockman<br/><sub>NASB, AMP — 5</sub> | Other publishers<br/><sub>1,051</sub> |
|---|:---:|:---:|:---:|:---:|
| Look it up and display it in your app | ✅ | ✅ | ✅ | ✅ |
| Store and cache the text | ✅ | ✅ | ✅ | ✅ |
| Use it offline | ✅ | ✅ | ✅ | ✅ |
| **Share the text outside your app** | ⚠️ <sub>per work's own licence</sub> | ❌ | ❌ | ❌ |
| Share a *reference* + link instead | ✅ | ✅ | ✅ | ✅ |
| Print it | ❌ | ❌ | ❌ | ❌ |
| Use commercially | ⚠️ <sub>per work's own licence</sub> | ❓ <sub>unsourced — see below</sub> | ❌ <sub>no access or membership fees</sub> | ✅ <sub>with disclosure</sub> |
| Run third-party advertising | ⚠️ | ⚠️ | ❌ | ⚠️ |
| Display more than 2 chapters / 25 verses at once | ✅ | ❌ | ✅ | ✅ |
| Hide the footnotes | ✅ | ❌ | ❌ | ❌ |
| Use it to personalise content with AI | ❌ | ❌ | ❌ | ❌ |

**And whatever the publisher, all of these apply:**

| | |
|---|:---:|
| Display the attribution | **required** |
| Reproduce the text word-for-word, unaltered | **required** |
| Update stored text when the publisher asks | **required** |
| Remove content within **48 hours** of a written request — Biblica (NIV, NIrV) | **required** |
| A conspicuous clickable link to lockman.org, plus a per-verse tag that itself links | **required** <sub>NASB, AMP only</sub> |
| Encrypt against unauthorised onward-supply | **required** |
| Keep your app key confidential, report its loss | **required** |
| Report annually to Lockman by end of February | **required** <sub>NASB, AMP only</sub> |

**Biblica's 48-hour removal clock is the one deadline on this upstream.** There is
no periodic sweep to build, but there is a delete path — and for NASB and AMP,
Lockman requires the attribution to be a *clickable link to lockman.org* and each
verse tag to link as well, which is stricter than displaying a copyright string.

**There is no refresh timer here** — unlike API.Bible, the duty is to update on
request rather than on a cycle. You still need a forced-refresh path.

---

## Compliance

### Storage — permitted, and encouraged

**You may persist scripture from this provider.** An earlier draft of this README
said otherwise; it was written before the publisher agreements had been read, and
it was wrong.

Three things support it, and the first is the one that grants the right:

1. **The publisher agreements grant storage expressly.** The common licence covers
   the right to "perform, **store**, distribute, and redistribute the Content on
   Your Application", and permits sublicensing to users "both online and
   **offline**". Offline use is not possible without storage.
2. **YouVersion's own SDKs cache scripture locally.**
3. **The platform documentation tells you to.** "Cache responses when possible" is
   the first of the Quick Reference's Best Practices.

**The platform terms themselves grant nothing in the Bible text** — "We are not
providing You rights in biblical works … which You must obtain from their
respective owners and licensors" — which is why the right comes from the publisher
agreement you accepted in the portal, or, for the public-domain versions, from the
work's own dedication.

**What you owe instead of a timer:**

- **Update on request, not on a cycle.** There is no 30-day refresh requirement
  here. Biblica obliges updates "as may be requested by LICENSOR"; the common
  template requires no edits and a duty to *notify* the publisher if text looks
  wrong. **You still need a forced-refresh path** — you just do not need a sweep.
- **Encrypt against unauthorised onward-supply**, and keep all footnotes displayed.
- **Rights cease when the agreement does.** Biblica's term is two years,
  auto-renewing; Lockman's ends on thirty days' notice.

**This package still stores nothing itself.** A passage lives for the duration of
the call; the only thing held is the catalogue, in memory, for six hours. Storage
is yours to build and yours to own.

### Reproduce the text verbatim

The platform terms require scripture "reproduced word-for-word and 100% accurate
to, and unaltered from, the licensed source text." Do not normalise quotation
marks, collapse punctuation, expand abbreviations or "fix" spelling anywhere
between `Text` and the screen.

They also restrict AI use: the tools may not be used with AI for open-ended chat
with a user — retrieving and displaying verbatim scripture is the stated
exception — nor to train or improve any AI technology.

### Attribution

`Attribution` arrives on every passage, sourced from the catalogue rather than the
passage response. This upstream gives you more material than most:

- `copyright` — the short notice
- `promotional_content` — a longer form, where you want it
- `publisher_url` — a link to the publisher's page, surfaced as `PublisherUrl` on
  `GetTranslationsAsync`

**But do not brand it as YouVersion's.** The terms forbid using the YouVersion,
YVP, Life.Church and The Bible App marks unless a Tool's YVP Terms allow it. Name
the *version* and its copyright holder.

### Filling the gaps — `TranslationMetadata`

This upstream carries more attribution material than most, but not for every
version. Where the catalogue returns nothing, supply it:

```csharp
TranslationMetadata = new List<TranslationMetadata>
{
    new() { Abbreviation = "WEB",
            Attribution  = "Public Domain. Courtesy of eBible.org",
            PublisherUrl = "https://ebible.org/web/" },
    // …one per translation you serve. Verified on: 2026-09-11
},
```

The merge is **per field, and the upstream wins where it returned a value** — your
entry fills only what came back empty. So a live copyright notice is never
displaced by a stale one you typed months ago.

**This sample is yours to own and verify.** This package deliberately ships no
prefilled copyright table: it is legal text about third-party intellectual
property, a NuGet package cannot be corrected in place, and a row that went stale
two releases ago looks exactly as authoritative as one that did not.

### Usage reporting — none owed

Every passage carries `Usage.Obligation == NotRequired`: a positive assertion that
nothing is owed, not an absence. There is no FUMS equivalent here.

One narrower duty does exist — "You shall enable and maintain any usage reporting
mechanisms built into YV IP." The REST API ships none, so there is nothing to keep
enabled. **That is scoped to the API**: the YouVersion SDKs are a different tool
with their own terms, and if one embeds reporting, that duty is yours.

### Your app key

It is confidential, may not be shared with any third party, and **YouVersion must
be told if it is lost, stolen or misused**. This package never logs it.

### Commercial use

**Permitted for most publishers, with a disclosure**, and **not** for Lockman.

If your application charges a fee you must "conspicuously and explicitly advise
Users that the YouVersion Bible App is provided at no cost to the User."

**Lockman (NASB, AMP) is the exception and it is absolute**: no third-party
advertising at all, and **no access charges or membership fees**. Stricter than
"free to end users", and it is the one publisher position in this table with a
clause behind it.

**Biblica (NIV, NIrV) is marked ❓ and that is deliberate.** An earlier draft of
this README said "free to end users", and **no clause supporting that has been
found**: the agreements' common royalty-free term is about the licence costing
nothing between the parties, not about what you may charge your users, and
Biblica's own additions concern a display cap and a removal duty. Rather than keep
a confident restriction with no source, or relax it to ✅ on the strength of an
absence, **re-read the Biblica agreement in the portal before charging for an
application that serves NIV**.

**This is markedly more permissive than API.Bible's non-commercial tier**, which
bars advertising, freemium and sponsorship outright. Do not assume one upstream's
commercial position applies to the other.

---

## Request cost

Every lookup is one live request, plus at most one catalogue request per
`CatalogueCacheDuration` **per configured language range**. A whole chapter is one
request. A verse range may be one or two.

No published rate limit was found, but a 429 carries a `Retry-After` and this
package honours it when it fits the remaining budget, throwing a marked exception
when it does not.

---

## What this package will not do

- **Resolve loose references server-side.** This upstream takes USFM only, so
  references are parsed locally. A reference the parser cannot read stays
  `InvalidReference` rather than being guessed at.
- **Cache or persist passages.** The package holds nothing beyond the call — but
  *you* may, and [Storage](#storage--permitted-and-encouraged) says on what terms.
- **Choose between providers.** That is your orchestration layer's job.
- **Map versification between editions.** This upstream exposes no organizational
  id, and a USFM key is edition-relative.

---

## Also in this family

| Package | |
|---|---|
| [`…Abstractions`](https://www.nuget.org/packages/Glory2Him.BibleProviders.Abstractions) | The contract, DTOs and parsers. Referenced by this package |
| [`…ApiBible`](https://www.nuget.org/packages/Glory2Him.BibleProviders.ApiBible) | API.Bible — American Bible Society |
| `…ApiBible.Fums` | The FUMS reporter for that provider. Not yet published |
| `…Abstractions.Conformance` | Contract tests a provider inherits. Not yet published |

Full design and reasoning:
[Glory2Him.BibleProviders](https://github.com/Glory2Him/Glory2Him.BibleProviders)

---

**FREE TO USE TO HELP SHARE THE GOSPEL**

> John 14:6 (NIV) "Jesus answered, 'I am the way and the truth and the life.
> No one comes to the Father except through me.'"
> [john.bible/john-14-6](https://john.bible/john-14-6)
