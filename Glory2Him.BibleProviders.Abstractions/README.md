# Glory2Him.BibleProviders.Abstractions

![Glory 2 Him](https://raw.githubusercontent.com/Glory2Him/Glory2Him/main/Resources/Images/Glory2Him-Banner.png)

---

> **Pre-release.** The design is complete and reviewed; the implementation is not
> written yet. Samples describe the designed API, not shipped behaviour.

**The contract every Glory2Him Bible provider implements.**

This package holds the interface, the DTOs, the reference parsers and the HTML
renderer. It contains **no HTTP and no provider-specific knowledge**, and depends
on exactly one package — `Microsoft.Extensions.Logging.Abstractions`. A domain or
application layer can reference it without acquiring an HTTP stack.

You do not use this package on its own. Pair it with at least one provider:
[ApiBible](https://www.nuget.org/packages/Glory2Him.BibleProviders.ApiBible) or
[YouVersion](https://www.nuget.org/packages/Glory2Him.BibleProviders.YouVersion).

---

## Getting started

```csharp
using var apiBible = new ApiBibleProvider(new ApiBibleConfigurations { ApiKey = "…" });
using var bibleProvider = new BibleAbstractionProvider(new IBibleProvider[] { apiBible });

ScriptureResult result = await bibleProvider.GetScriptureByReferenceAsync(
    ApiBibleProvider.ProviderName, "John 3:16", cancellationToken);

if (result.IsFound)
{
    string text = result.Passage.Text;              // never empty on a Found result
    string? notice = result.Passage.Attribution;    // display this
}
```

Both lookup forms are accepted. A translation-qualified USFM key —
`JHN.3.16.NIV`, `JHN.3.16-JHN.3.18.ESV`, `PSA.23.KJV` — or a loose human
reference, `"John 3:16 NIV"`, `"1 Cor 13:4-7 (ESV)"`, `"Jude 5"`.

---

## The two channels

**This is the central rule, and a consumer that handles only one of them has a
bug that will not show up until an outage.**

| The provider… | Channel | You get |
|---|---|---|
| understood and answered | **returns** | `ScriptureResult` with a status |
| could not answer at all | **throws** | an exception carrying a marker interface |

"Answered" means: here is the passage; that passage is not in this edition; I do
not carry this translation; that reference does not parse. **"Could not answer"
means: I am rate limited, my quota is spent, my credentials are rejected, the
upstream is down, the request timed out.**

The distinction exists so you can tell *"this passage isn't there"* — where asking
another provider is pointless — from *"this provider is unavailable"*, where asking
another is exactly right.

```csharp
public enum ScriptureLookupStatus
{
    Unknown = 0,                  // never returned; treat as a defect
    Found = 1,
    NotFound = 2,                 // the provider has the translation; the passage is absent
    TranslationNotSupported = 3,  // the provider does not carry this translation
    InvalidReference = 4,         // the input could not be parsed
}
```

### Catch markers, never concrete types

```csharp
catch (Exception exception) when (exception is IBibleQuotaExceededException quota)
{
    // Stop asking until quota.QuotaResetsOn. Retrying spends an allowance already gone.
}
catch (Exception exception) when (exception is IBibleRateLimitException limited)
{
    // Transient. Honour limited.RetryAfter, then this provider is usable again.
}
catch (Exception exception) when (exception is IBibleAuthorizationException)
{
    // The key is rejected or revoked. Log at Error; it will not recover on its own.
}
catch (Exception exception) when (exception is IBibleDependencyException)
{
    // Any other unavailability.
}
```

Every marked exception also exposes `ProviderConsole` — the upstream's own
dashboard or key portal — so an alert raised from a `catch` block says where to go,
not only what broke.

**Through the abstraction these arrive wrapped**, with the provider's own
exception preserved as `InnerException`. Read the specific marker, `RetryAfter`
and `QuotaResetsOn` off that.

---

## What you get back

```csharp
public sealed class ScripturePassage
{
    public required string Usfm { get; init; }          // edition-relative, translation-suffixed
    public required string Reference { get; init; }     // rendered by us, in the edition's language
    public required string Translation { get; init; }
    public required string Text { get; init; }          // never empty on a Found result
    public required string Language { get; init; }              // ISO 639-3
    public required ScriptDirection ScriptDirection { get; init; }
    public required ScriptureUsage Usage { get; init; }         // what you owe the rights holder
    public required string? Attribution { get; init; }          // display this
    public required ScriptureMarkup Markup { get; init; }       // whether Html is ours
    public string? Html { get; init; }
    public IReadOnlyList<ScriptureBlock> Blocks { get; init; }
    // …VerseCount, IsTruncated, RequestedUsfm, MissingVerseIds, Notes
}
```

Four things worth knowing before you store any of it:

**`Usfm` is edition-relative.** Verse numbering is a property of the edition, not
of scripture — `MAL.4.1` in KJV is `MAL.3.19` in Hebrew-versified editions. The
`.TRANSLATION` suffix is load-bearing: `PSA.3.1` does not identify a verse,
`PSA.3.1.NIV` does. **Never re-resolve a stored key against a different
translation** to "get the same verse in another version". That is a versification
mapping and this library does not do one.

**A `Found` result may carry less than you asked for, but never silently.** `Usfm`
is the range actually returned, `RequestedUsfm` what you asked for when they
differ, `MissingVerseIds` the verses the upstream acknowledged but returned no text
for, and `IsTruncated` says the upstream capped the range. Check them before you
store.

**`Text` is never empty on a `Found` result.** Some editions ship verses omitted by
critical texts as an empty marker carrying only a footnote. Those return
`NotFound`, so you cannot end up storing blank, attributed scripture.

**`ScriptDirection` is not decoration.** Hebrew, Arabic, Farsi and Urdu scriptures
are right-to-left, and `Html` carries `dir="rtl"` for them. Store it — it is not
recoverable from `Text` later.

---

## What can I do with the scripture?

**That depends on the provider and the translation, not on this package.** This
contract carries the obligations (`Usage`, `Attribution`, `ScriptDirection`) and
performs none of them.

The short version, and both provider READMEs carry the full table:

| | |
|---|:---:|
| Display it in your application | ✅ always |
| Store and cache it | ✅ both providers |
| **Share the text outside your application** | ❌ **except public-domain and permissively-licensed translations** |
| Share a *reference* and a link instead | ✅ always |
| Print it | ❌ mostly |

**Neither upstream exposes a rights class**, so a share feature must classify
translations from configuration and default to *not shareable*. See
[ApiBible](https://www.nuget.org/packages/Glory2Him.BibleProviders.ApiBible) and
[YouVersion](https://www.nuget.org/packages/Glory2Him.BibleProviders.YouVersion).

---

## What you must do to comply

**These are obligations on your application, not on this library.** The figures
are each provider's; the mechanics are here.

### Display the attribution

`Attribution` is `required` and nullable — you must decide what to do, and null
means the upstream supplied none. Display it wherever the scripture appears. A
null on a licensed edition is a compliance event and is logged at Warning; the fix
is a `TranslationMetadata` entry in that provider's configuration.

### Report usage where it is owed

Every passage carries a `ScriptureUsage` stating a position, including "nothing is
owed". Where reporting **is** owed, it is owed **per display, not per fetch** — one
fetch can produce a thousand displays or none, so this library cannot discharge it
for you and never tries.

```csharp
// Store this in a NON-NULLABLE column, alongside the text.
string stored = result.Passage.Usage.ToStorageString();
```

It serializes to one non-empty value even when nothing is owed, so a blank stored
value is provably a bug rather than an absence. **If you drop it at mapping time,
every later display is unreportable — with no error and no symptom until the rights
holder asks.**

### This library stores nothing

**No package here caches, stores or writes scripture anywhere.** A passage exists
for the lifetime of the call and whatever reference you keep. The only thing a
provider holds is its catalogue — abbreviation to upstream id, plus names,
languages and copyright — in memory, for six hours by default, never on disk.

So every retention obligation below is **yours, and only once you choose to
persist**. A display-only application inherits none of them. What it does inherit
regardless are the display-time duties: attribution, usage reporting where it is
owed, and reproducing the text unaltered.

### Store the right things

- **`Text` is the canonical stored value** — searchable and safe everywhere.
- **`Html` goes in a separate nullable field**, sanitized on write. Store
  `Markup.ToStorageString()` beside it: when it parses back as `Generated`, this
  library built that markup itself from a closed vocabulary with every text node
  escaped, and you can render it directly.
- **Do not store `Blocks`.** Keeping the stored shape to the two derived renditions
  means adding a block kind later is a code change, not a data migration.
- **Store `Translation` and `Usfm` together** — the pair is the row's identity.
- **Store `Language` and `ScriptDirection`.**

### Do not alter the words

Dropping markup and section headings is structural. Touching the text is not. Do
not normalise quotation marks, collapse punctuation, or "fix" spelling — a
17th-century edition is meant to read like one, and at least one upstream makes
verbatim reproduction contractual.

---

## Failing over between providers

This library never chooses a provider — that is deliberate, because which
providers exist and under what subscription changes independently of any contract
here. A loop that inspects only `ScriptureResult.Status` will miss every
availability failure, because those are exceptions.

| Outcome | Channel | Do |
|---|---|---|
| `Found` | status | Return it. Check `IsTruncated`/`MissingVerseIds` first |
| `TranslationNotSupported` | status | **Try the next provider** — a different subscription may carry it |
| `NotFound` | status | **Stop.** It is not in that edition; asking again wastes quota |
| `InvalidReference` | status | **Stop.** The input is bad |
| `IBibleRateLimitException` | exception | Try the next. Transient; honour `RetryAfter` |
| `IBibleQuotaExceededException` | exception | Try the next, and **stop asking this one** until `QuotaResetsOn` |
| `IBibleAuthorizationException` | exception | Try the next, log at Error |
| `IBibleUnavailableException` | exception | Try the next. Transient |
| `OperationCanceledException` | exception | **Let it bubble.** The caller went away |

**One constraint binds any such policy:** qualify the reference with a translation
before you start. Each provider applies its *own* default to an unqualified
reference, so the same string can resolve to different translations — and in
Psalms, Joel and Malachi those editions may number it differently.

---

## Wiring it up

No package here ships an `IServiceCollection` extension. A provider is
constructed, not registered-and-resolved.

```csharp
services.AddSingleton<IBibleAbstractionProvider>(sp =>
    new BibleAbstractionProvider(new List<IBibleProvider>
    {
        new ApiBibleProvider(apiBibleConfig, sp.GetRequiredService<ILogger<ApiBibleProvider>>()),
        new YouVersionProvider(youVersionConfig, sp.GetRequiredService<ILogger<YouVersionProvider>>()),
    }));

// Then force it at startup, so a bad key fails the host and not the first user request.
var abstraction = app.Services.GetRequiredService<IBibleAbstractionProvider>();
```

Three traps you cannot derive from the types:

1. **`AddSingleton(factory)` is lazy.** Without resolving it at startup, providers
   are constructed — and their configuration validated — on the first request that
   touches scripture. A 500 instead of a boot failure.
2. **A container disposes only what it creates.** Construct *inside* the factory.
   Registering a pre-built instance leaks every provider's HTTP handler pool.
3. **Duplicate provider names throw at construction**, not at first use.

Disposing the abstraction disposes the providers it was handed.

---

## Also in this family

| Package | |
|---|---|
| [`…ApiBible`](https://www.nuget.org/packages/Glory2Him.BibleProviders.ApiBible) | API.Bible — American Bible Society |
| [`…YouVersion`](https://www.nuget.org/packages/Glory2Him.BibleProviders.YouVersion) | YouVersion Platform — Life.Church |
| `…ApiBible.Fums` | The FUMS usage reporter. Not yet published |
| `…Abstractions.Conformance` | Contract tests a provider inherits. Not yet published |

Full design and reasoning:
[Glory2Him.BibleProviders](https://github.com/Glory2Him/Glory2Him.BibleProviders)

---

**FREE TO USE TO HELP SHARE THE GOSPEL**

> John 14:6 (NIV) "Jesus answered, 'I am the way and the truth and the life.
> No one comes to the Father except through me.'"
> [john.bible/john-14-6](https://john.bible/john-14-6)
