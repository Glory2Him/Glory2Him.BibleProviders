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
    DefaultTranslation = "WEB",   // World English Bible — see below
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
| `DefaultTranslation` | `"WEB"` | Fills an unqualified reference. World English Bible — public domain, shareable, **safe *and* recommended** |
| `TranslationMap` | empty | `"NIV"` → a specific bibleId. Overrides catalogue lookup; wins, because abbreviations are not unique and licensed access varies per key. **Read [Commercial use](#commercial-use) before mapping a licensed translation** |
| `TranslationMetadata` | empty | Backfills copyright and publisher links. **You will want this** — see [Attribution](#attribution) |
| `ParseLanguages` | `["eng"]` | Which languages loose references are read in, ISO 639-3 |
| `UseOrgId` | `false` | Versification scheme. **Never flip against stored keys** — it changes what every USFM key means |
| `MonthlyRequestAllowance` | `null` | Your plan's allowance, if you want quota exhaustion detected — see [Quota](#quota-and-rate-limits) |
| `CatalogueCacheDuration` | 6 hours | |
| `TimeoutSeconds` | 20 | Overall budget for one lookup |
| `PerAttemptTimeoutSeconds` | 5 | |
| `MaxRetryAttempts` | 1 | Retries, not attempts — 1 means 2 attempts. Low on purpose: a retry spends quota you cannot get back |

### Why the default is WEB

An unqualified reference is a first-class input, and the default silently fills the
gap — so the shipped value decides whether a freshly-keyed installation works at
all.

**The World English Bible is in the open-access set**, so it resolves on a new key
without spending one of the three licensed-Bible slots, and it is **public domain
by dedication**: no licence to accept, no territorial restriction, and it may be
sent on by email or messaging. Unusually for a default in this library, it is
**safe *and* recommended**.

**It replaced `KJV`, and the reason matters if you are upgrading.** API.Bible
grants **no licence for the King James Version** in the United Kingdom, the Isle of
Man, Jersey, Guernsey or thirteen British Overseas Territories — "irrespective of
whether your use is Commercial Use or Non-Commercial Use … **whether the content
is identified as Public Domain**, and irrespective of format" (Terms §9.8) — and
separately bars transmitting it anywhere (§9.9(b)(i)). **The duty follows your
reader's location, not yours.**

`NIV` is the worst candidate: licensed, non-commercial-only, and absent from a
fresh key's catalogue. Set `DefaultTranslation` explicitly if you hold a licence.

---

## What can I do with API.Bible content

Permissions vary by the **rights class** of a translation, not by the endpoint you
called. **The API does not tell you which class a translation is in** — there is no
rights field on the catalogue — so classify them in configuration and default
anything that leaves your application to *off*.

| | Public domain<br/>CC BY · CC BY-SA | CC BY-**NC**<br/>CC BY-**ND** | Licensed<br/>NIV · ESV · NLT |
|---|:---:|:---:|:---:|
| Look it up and display it in your app | ✅ | ✅ | ✅ |
| Store and cache the text | ✅ | ✅ | ✅ |
| **Share the text outside your app**<br/><sub>WhatsApp, X, email, SMS</sub> | ✅ | ❌ | ❌ <sub>unless the rights holder authorises</sub> |
| Share a *reference* + link instead | ✅ | ✅ | ✅ |
| Print more than 100 verses | ❌ | ❌ | ❌ |
| Use commercially | ✅ | ❌ | ❌ <sub>on the free Starter tier</sub> |
| Let users copy or redistribute freely | ✅ <sub>§12's DRM binds "the Property"; §2 excludes public domain from it</sub> | ❌ | ❌ <sub>DRM required</sub> |

### Which public-domain translations may be sent on

**The common case — look a verse up, show it on a page, let a reader send it on —
is fully permitted for public-domain translations, with one important exception.**

| Translation | Rights | Display | **Send on**<br/><sub>email · WhatsApp · SMS</sub> | Print | Commercial |
|---|---|:---:|:---:|:---:|:---:|
| **WEB** — World English Bible | Public domain (dedicated) | ✅ | ✅ | ✅ | ✅ |
| **BSB** — Berean Standard Bible | Public domain (dedicated) | ✅ | ✅ | ✅ | ✅ |
| **ASV** — American Standard Version | Public domain | ✅ | ✅ | ✅ | ✅ |
| **YLT**, **DARBY**, **DRA**, **GNV**, **WBT** | Public domain | ✅ | ✅ | ✅ | ✅ |
| **FBV**, **ULB/UST** | CC BY-SA 4.0 | ✅ | ✅ <sub>share-alike follows it</sub> | ✅ | ✅ |
| **KJV** | Public domain in the US · **Crown copyright in the UK** | ⚠️ | ❌ | ⚠️ | ⚠️ |
| Any CC BY-**NC** / **ND** edition | Restricted CC | ✅ | ❌ | ⚠️ | ❌ |

**⚠️ The King James Version is the exception to all of it.** Terms §9.8 grants **no
licence** for the KJV within the United Kingdom, the Isle of Man, Jersey, Guernsey
or thirteen British Overseas Territories — "irrespective of whether your use is
Commercial Use or Non-Commercial Use … **whether the content is identified as
Public Domain**, and irrespective of format". §9.9(b)(i) separately bars
transmitting it anywhere. **The duty follows your reader's location, not yours**,
and this package cannot know it.

Derived translations are expressly out of scope: NKJV, ESV, NASB, RSV, NRSV, MEV
and **ASV** are named as *not* being the Authorized Version. **For a UK or
Commonwealth audience, or for any share feature, use WEB or BSB.**

**Public domain does not switch the Terms off.** Even for WEB you still owe FUMS
reporting, the 30-day recency check on anything stored, and the deletion duties —
those are contractual duties to ABS, not copyright duties to a rights holder. What
public domain *does* switch off is the copyright-page requirement (§7) and the DRM
requirement on transmission (§9.9(c)).

### Keeping the content and the key secure

**Terms §12 puts three duties on you that no other section of this README covers**,
and they apply to public-domain content as much as licensed:

| | |
|---|:---:|
| Never make your API key available to any third party | **required** |
| Keep stored scripture confidential and secure, with **no less care than you use for similar data you store** | **required** |
| **Notify support@api.bible immediately** on knowing of *or suspecting* a breach or potential vulnerability, then cooperate and remedy it | **required** |

The middle one is a *relative* standard and bites harder than it reads: if you
encrypt your own user data at rest and leave cached scripture in plaintext, you
have failed it by the words of the clause.

The third is triggered by **suspicion**, not confirmation.

**And whatever the class, all of these apply:**

| | |
|---|:---:|
| Display the attribution | **required** |
| Report usage on display — FUMS | **required** |
| Refresh stored text at least every 30 days | **required** |
| Delete content withdrawn upstream | **required** |
| Purge everything within 72 hours of a request or a lapsed plan | **required** |
| Keep fewer than 500 consecutive verses cached | requested |
| DRM restricting copying, printing, territory and device count | **required** |

Detail and the clauses behind every mark:
[Compliance](#compliance) below.

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
