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
2. **Storing scripture from this provider is not yet sanctioned.** See
   [Storage](#storage--not-yet-sanctioned). Display-time only for now.

---

## Getting started

```csharp
var configurations = new YouVersionConfigurations
{
    AppKey = "…",                            // required, sent as X-YVP-App-Key
    LanguageRanges = new List<string> { "eng" },   // required upstream
    DefaultTranslation = "KJV",
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
| `DefaultTranslation` | `"KJV"` | Fills an unqualified reference. **Verify your key can see it** — see below |
| `TranslationMap` | empty | `"NIV"` → a numeric version id. Overrides catalogue lookup, and is authoritative |
| `TranslationMetadata` | empty | Backfills copyright and publisher links where the catalogue has none |
| `IncludeAllAvailable` | `false` | Widens the listing beyond what your key is licensed for. **Diagnostics only** — see below |
| `MaxStitchedVerses` | 30 | Caps the size of a stitched range |
| `CatalogueCacheDuration` | 6 hours | |
| `TimeoutSeconds` | 20 | Overall budget for one lookup |
| `PerAttemptTimeoutSeconds` | 5 | |
| `MaxRetryAttempts` | 2 | Retries, not attempts — 2 means 3 attempts |

### `LanguageRanges` does double duty

It scopes the catalogue call upstream **and** is the scope in which loose
references are read. That is deliberate: the languages you can serve and the
languages you can parse references in are then the same list by construction. A
Spanish deployment sets `["spa"]` and gets both.

### Why the default is KJV, and the caveat

KJV is version id `1` and public domain, so it is the most plausible translation to
be available on any key. **But access is gated per accepted licence**, and whether a
*fresh* key sees KJV without accepting anything in the portal is unverified.

If it does not, set `DefaultTranslation` to a version your key has actually
accepted — otherwise every unqualified reference returns `TranslationNotSupported`.
The provider logs at **Error** on its first catalogue load if the configured default
is absent, so you will know.

---

## The licence-acceptance trap

**Your app key only sees versions whose agreements you accepted in the portal.**
A translation you have not accepted behaves exactly like one that does not exist:
`TranslationNotSupported`, with nothing to distinguish the two.

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

## Compliance

### Storage — not yet sanctioned

**Do not persist scripture from this provider yet.** Not because a figure is
unread, but because of what the platform terms actually say:

> "This Agreement is limited to the YV IP. We are not providing You rights in
> biblical works or works other than YV IP, which You must obtain from their
> respective owners and licensors."

The platform terms cover the *tools* — the API and SDKs — and grant no rights in
the Bible text at all. They therefore carry no retention clause, no refresh cadence
and no caching cap, **because they are not the agreement that would carry one.**

The retention question lives in two places instead, and neither is this one:

1. **The per-version licence agreements** you accept in the portal, which are with
   the publishers.
2. **The per-Tool "YVP Terms"** published in the platform, incorporated by
   reference, and which **override the platform terms where they conflict**.

Until one of those is read and recorded, treat results from this provider as
**display-time only**.

**To be clear about what this is and is not:** nothing anyone has read forbids
caching. The restriction exists because the permission is unestablished, not
because a refusal was found — and YouVersion's own SDKs cache scripture locally,
so the likely answer is that storage is permitted under conditions nobody has
written down yet. That is a reason to go and read the agreement you accepted, not
a reason to assume either way.

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

Permitted, with a disclosure: if your application charges a fee, you must
"conspicuously and explicitly advise Users that the YouVersion Bible App is
provided at no cost to the User."

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
- **Cache or persist passages.** See [Storage](#storage--not-yet-sanctioned).
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
