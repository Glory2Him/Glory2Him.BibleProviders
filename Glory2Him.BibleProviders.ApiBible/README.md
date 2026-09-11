# Glory2Him.BibleProviders.ApiBible

![Glory 2 Him](https://raw.githubusercontent.com/Glory2Him/Glory2Him/main/Resources/Images/Glory2Him-Banner.png)

---

> **Pre-release.** The design is complete and reviewed; the implementation is not
> written yet. Samples describe the designed API, not shipped behaviour.

**API.Bible — operated by the American Bible Society — behind the Glory2Him Bible
provider contract.**

Get an API key at [api.bible](https://api.bible/). The free Starter plan carries
5,000 requests a month and up to three licensed Bibles **for non-commercial use
only**, plus the open-access translations. Read [Compliance](#compliance) before
you ship — this upstream's obligations are the heaviest of the two, and one of them
is a licence condition rather than a nicety.

---

## Getting started

```csharp
var configurations = new ApiBibleConfigurations
{
    ApiKey = "…",                 // required
    DefaultTranslation = "KJV",   // see below
};

using var provider = new ApiBibleProvider(configurations);
using var bibleProvider = new BibleAbstractionProvider(new IBibleProvider[] { provider });

ScriptureResult result = await bibleProvider.GetScriptureByReferenceAsync(
    ApiBibleProvider.ProviderName, "John 3:16", cancellationToken);

if (result.IsFound)
{
    Console.WriteLine(result.Passage.Text);
    Console.WriteLine(result.Passage.Attribution);   // you must display this
}
```

The constructor validates eagerly and throws — bad key, blank default, an
unparseable base URL, a timeout budget that does not close. Construct it at
startup so a misconfiguration fails the host rather than the first user request.

---

## Configuration

| Field | Default | |
|---|---|---|
| `ApiKey` | — | **Required.** From [api.bible](https://api.bible/) |
| `BaseUrl` | `https://rest.api.bible/v1/` | |
| `DefaultTranslation` | `"KJV"` | Fills an unqualified reference. **Safe, not recommended** — see below |
| `TranslationMap` | empty | `"NIV"` → a specific bibleId. Overrides catalogue lookup; wins, because abbreviations are not unique and licensed access varies per key. **Read [Commercial use](#commercial-use) before mapping a licensed translation** |
| `TranslationMetadata` | empty | Backfills copyright and publisher links. **You will want this** — see [Attribution](#attribution) |
| `ParseLanguages` | `["eng"]` | Which languages loose references are read in, ISO 639-3 |
| `UseOrgId` | `false` | Versification scheme. **Never flip against stored keys** — it changes what every USFM key means |
| `MonthlyRequestAllowance` | `null` | Your plan's allowance, if you want quota exhaustion detected — see [Quota](#quota-and-rate-limits) |
| `CatalogueCacheDuration` | 6 hours | |
| `TimeoutSeconds` | 20 | Overall budget for one lookup |
| `PerAttemptTimeoutSeconds` | 5 | |
| `MaxRetryAttempts` | 2 | Retries, not attempts — 2 means 3 attempts |

### Why the default is KJV

An unqualified reference is a first-class input, and the default silently fills the
gap — so the shipped value decides whether a freshly-keyed installation works at
all. **KJV is in the open-access set**, so it resolves on a new key without
spending one of the three licensed-Bible slots.

It is a **safe** default, not a recommended one. `NIV` is the worst candidate:
licensed, non-commercial-only, and absent from a fresh key's catalogue. Set
`DefaultTranslation` explicitly if you hold a licence.

---

## Compliance

**Everything in this section is an obligation on your application.** This package
surfaces what you need; it cannot discharge any of it for you.

### FUMS — usage reporting

FUMS is the American Bible Society's usage reporting. **It is a licence condition,
not analytics** — ABS licenses most of its translations from publishers and must
report how much each is read. Their terms make it mandatory for any web
application, unless prohibited by local law.

**The word that decides everything is *display*, not fetch.** One fetch can produce
a thousand displays — if you store the text and re-render it — or none.

This package puts the token on every passage:

```csharp
// Persist this in a NON-NULLABLE column, beside the text.
string usage = result.Passage.Usage.ToStorageString();
```

Then report **when a person sees the scripture**, by one of two paths ABS
sanctions. Both are yours to implement; the
`Glory2Him.BibleProviders.ApiBible.Fums` package will do the server path for you
once published.

- **Browser (ABS's recommended default).** Load `https://pkg.api.bible/fumsV3.min.js`
  and call `fums('trackView', [token, …])`. The report goes from the viewer's
  browser straight to ABS — not through your server — and needs no API key.
- **Server-side.** `GET https://fums.api.bible/f3?t={token}&dId={deviceId}&sId={sessionId}`.
  GET only, no API key. Supply device and session ids you have an honest basis for;
  values minted per request collapse every viewer into one device.

Four rules that are easy to get wrong:

1. **Batch.** One page showing five passages is one report.
2. **Deduplicate per viewer per session.** Duplicates are *counted*, not rejected —
   over-reporting is as much a misreport as under-reporting, and it is the easier
   mistake to make from a database.
3. **Never fail a render on it.** The endpoint acknowledges nothing, so a `200`
   proves only that the request left your process. Swallow and log.
4. **If you use the browser path**, allow **both** `script-src https://pkg.api.bible`
   and `connect-src https://fums.api.bible` in your CSP. The tracker uses `fetch`,
   not an image, so allowlisting `img-src` blocks it silently.

**A missing token still returns a `Found` result** carrying
`ReportingUnavailable` and logs a Warning. Scripture delivery is never held hostage
to a reporting field — but the gap is visible on the stored row rather than silent.

### Storage and refresh

Their terms make stored content a **refreshable cache, not an archive**:

1. **Check at least every 30 days** for updates. ABS's own guidance recommends
   14 days or less; 14 satisfies both, being the stricter of the two.
2. **Apply an update within 24 hours** of a request from ABS or the rights holder.
   A nightly sweep looks like compliance and is not — you need a forced,
   on-demand refresh path for a named edition.
3. **Delete or modify content when it is deleted or modified upstream.** Refreshing
   is not enough: a verse or edition that disappears upstream must disappear from
   your store too.
4. **Remove everything within 72 hours** when a licence terminates, when ABS
   suspends you, or when a subscription is terminated or deactivated — **an unpaid
   plan counts as deactivated** — and within 72 hours of any removal request.

**Rules 3 and 4 mean you need a delete path, not just a refresh path.** Design it
before you write the first row; nothing in normal operation exercises it.

They also request that cached content stay **under 500 consecutive verses**. The
200-verse cap on any single passage keeps one request inside that; stitching
adjacent passages into a stored book does not.

### Attribution

Their terms ask for more than a copyright string:

1. **A dedicated copyright page** you host, carrying translation names,
   abbreviations, rights-holder details **and website links**.
2. **A citation on each quotation** — `(KJV)` — **hyperlinked to that page**.

`Attribution` arrives on every passage and gives you the notice. It does not give
you the links, because **this upstream exposes no URL field at all** — not on a
passage, not on the catalogue. Supply them yourself:

```csharp
TranslationMetadata = new List<TranslationMetadata>
{
    new() { Abbreviation = "KJV",
            Attribution  = "Public Domain",
            PublisherUrl = "https://…" },
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

### Commercial use

Starter's three licensed Bibles are **non-commercial only**, and their terms define
that broadly — advertising, licensing fees, in-app promotions, sponsorships,
**freemium models**, paid access, "or any other revenue generating activity".

An ad-supported or freemium surface **is** commercial under those terms. Settle
this before configuring a licensed translation such as NIV, ESV or NLT; it is a
business and legal question, not an engineering one.

---

## Quota and rate limits

| Plan | Requests |
|---|---|
| Starter | 5,000 / month |
| Pro | 150,000 / month |
| Enterprise | Negotiated |

**Plans default to no overage protection: past the allowance, service is disrupted
rather than billed.** Exhausting the plan is an *outage*, not a slowdown.

Every lookup is one live request, plus at most one catalogue request per
`CatalogueCacheDuration`. Against Starter that is roughly **165 lookups a day**, so
a surface that re-renders the same passage on every page view will exhaust the
plan. **Caching in your application is not optional** — and bounded by the refresh
rules above.

ABS documents no 429 behaviour anywhere, so this package cannot always tell a
throughput throttle from an exhausted plan. Setting `MonthlyRequestAllowance` gives
it a second signal: it counts its own requests within the calendar month and treats
an ambiguous failure past that allowance as quota exhaustion, warning once at 80%.
The count is a lower bound — other instances share your key — so it only ever
strengthens that conclusion, and it never blocks a request.

---

## What this package will not do

- **Report FUMS.** Reporting needs the viewer's device and session identity, which a
  fetch does not have and must not invent. There is deliberately no "report on
  fetch" flag: it would look like compliance without being it.
- **Cache or persist passages.** Storage and its licence-bounded retention rules
  belong to whoever owns the database and the agreement.
- **Choose between providers.** That is your orchestration layer's job.
- **Map versification between editions.** A USFM key is edition-relative.

---

## Also in this family

| Package | |
|---|---|
| [`…Abstractions`](https://www.nuget.org/packages/Glory2Him.BibleProviders.Abstractions) | The contract, DTOs and parsers. Referenced by this package |
| [`…YouVersion`](https://www.nuget.org/packages/Glory2Him.BibleProviders.YouVersion) | YouVersion Platform — Life.Church |
| `…ApiBible.Fums` | The FUMS reporter. Not yet published |
| `…Abstractions.Conformance` | Contract tests a provider inherits. Not yet published |

Full design and reasoning:
[Glory2Him.BibleProviders](https://github.com/Glory2Him/Glory2Him.BibleProviders)

---

**FREE TO USE TO HELP SHARE THE GOSPEL**

> John 14:6 (NIV) "Jesus answered, 'I am the way and the truth and the life.
> No one comes to the Father except through me.'"
> [john.bible/john-14-6](https://john.bible/john-14-6)
