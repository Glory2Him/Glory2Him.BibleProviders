# API.Bible provider

**Area prefix:** `APB` · **Sections:** §APB1 – §APB25
**Packages:** `Glory2Him.BibleProviders.ApiBible`, `Glory2Him.BibleProviders.ApiBible.Fums`
**Implements:** the contract in [Abstractions.md](Abstractions.md)
**Solution overview:** [Design.md](Design.md) · **Sibling provider:** [YouVersion.md](YouVersion.md)
**Upstream:** API.Bible, operated by the American Bible Society (ABS)

Conventions, heading tags and provenance tags: [Design.md](Design.md), "Conventions".

---

## APB1. Upstream documentation (#1)

Everything in this document was written against these sources, and re-checked
against them. An implementer should have them open.

| What | URL |
|---|---|
| API documentation (root) | https://docs.api.bible/ |
| Getting started / authentication | https://docs.api.bible/quick-start/authentication |
| **Error codes** — the source of §APB14 | https://docs.api.bible/quick-start/errors |
| **Rate limiting & plan quotas** — the source of §APB15's quota rules | https://docs.api.bible/quick-start/rate-limiting |
| Licensing & access | https://docs.api.bible/quick-start/licensing-and-access |
| **Passages guide** — passage-id rules, the 200-verse cap, parameter defaults | https://docs.api.bible/guides/passages/ |
| **Bibles guide** — catalogue fields, `include-full-details` | https://docs.api.bible/guides/bibles |
| **Search guide** — the source of §APB12 | https://docs.api.bible/guides/search |
| Referencing verses — `id` vs `orgId`, versification | https://docs.api.bible/resources/referencing-verses/ |
| **Fair use / FUMS guide** — the reporting obligation | https://docs.api.bible/guides/fair-use/ |
| Styling scripture (USX classes) | https://docs.api.bible/tutorials/styling-scripture/ |
| OpenAPI / Swagger definition | https://api.scripture.api.bible/v1/swagger.json |
| **Terms & Conditions** — §APB17, §APB19, §APB20 | https://api.bible/terms-and-conditions |
| FAQ (api.bible) — refresh guidance, "30 days" | https://api.bible/faq |
| FAQ (scripture.api.bible) — refresh guidance, "14 days" | https://scripture.api.bible/faq |
| Developer portal / key management | https://api.bible/ |
| FUMS v3 browser tracker | https://pkg.api.bible/fumsV3.min.js |
| FUMS report endpoint | `https://fums.api.bible/f3` |
| `scripture-styles` CSS (USX class vocabulary) | https://github.com/americanbible/scripture-styles |

**The two FAQs disagree with each other** on cache-refresh cadence — 14 days on
one, 30 on the other [contested]. The Terms say 30 and the Terms are what bind
(§APB17).

---

## APB2. What the upstream offers (#1)

**Base URL `https://rest.api.bible/v1/`** [verified] — this is the host the
current passages guide documents. An older host, `api.scripture.api.bible/v1`,
still appears in the Swagger definition's own URL; treat `rest.api.bible` as
correct and the older one as legacy.

| Aspect | Detail |
|---|---|
| Auth | `api-key: {key}` header on every scripture request |
| Plans | **Starter 5,000 requests/month, Pro 150,000, Enterprise negotiated** [verified]. Overage is billed at **$1 per additional 1,000 calls**, and **plans default to *no* overage protection: past the quota the service is disrupted rather than billed** [verified]. Starter carries up to 3 licensed Bibles **non-commercial** plus open-access translations |
| Catalogue | `GET /v1/bibles` → Bibles with an **opaque** `id` — documented as a 16-digit string plus a publication suffix, e.g. `de4e12af7f28f599-02` [verified] — plus `abbreviation`, `abbreviationLocal`, `name`, `language`, `countries`. **Abbreviations are not documented as unique** across the catalogue, so this design does not assume they are (§APB7) |
| **Catalogue copyright** | **`copyright` is not on the plain list response.** It is documented on the single-Bible endpoint, and on the list only when **`include-full-details=true`** is sent [verified]. See §APB7 rule 2 — this corrects an assumption that cost nothing here only because the passage response also carries it |
| Verse | `GET /v1/bibles/{bibleId}/verses/{verseId}` — `JHN.3.16`. **Single verse only** |
| Passage (range) | `GET /v1/bibles/{bibleId}/passages/{passageId}` where **`passageId` is two full verse IDs joined by `-`** [verified] — `JHN.3.16-JHN.3.18`, `1CO.16.1-2CO.1.23`. A bare chapter id is *not* a passage id. Ranges may cross chapters and books, capped at **200 verses**; past the cap the response's `id` reports the range actually returned [verified] |
| Chapter | `GET /v1/bibles/{bibleId}/chapters/{chapterId}` — `PSA.23`. **This, not `/passages`, is the route for a chapter-only key.** No chapter reaches the 200-verse cap (the longest, PSA.119, is 176) |
| Content formats | `content-type=html \| json \| text`, default `html` [verified]. **`json`** returns a structured tree of `para` blocks and nestable `char` runs carrying USX style names — the cleanest source for §ABS22's block/inline model |
| Formatting flags | `include-notes` (default **false**), `include-titles` (default **true**), `include-chapter-numbers` (default **false**), `include-verse-numbers` (default **true**), `include-verse-spans` (default **false**), `parallels` [verified] |
| Versification | `use-org-id` (default `false`) is **not** a formatting flag: it selects which numbering the id **in the request** is resolved against. The response carries both `id` and `orgId` either way. §APB13 |
| Red letter | Content is USX-based; words of Jesus are the char style **`wj`**, deity name `nd`, poetry `q1`–`q4`. Preserved end to end in the `json` tree |
| Loose reference | `GET /v1/bibles/{bibleId}/search?query=John 3:16` — the `query` parameter is documented as accepting **keywords *or* a passage reference** [verified]. §APB12 |
| Compliance | `copyright` on the passage response (must be displayed) and, **when `fums-version=3` is on the request**, `meta.fumsToken`. §APB16 |

**The parameter defaults are why the query string in §APB8 is fully explicit.**
`include-titles` and `include-verse-numbers` both default to **true** [verified],
so a request that omits them gets section headings and verse numbers interleaved
into the text — which would then flow into `Text` and into every stored row. The
defaults are the opposite of what this design wants for three of the six flags, so
none of them are left to the default.

---

## APB3. Identity and configuration (#1)

```csharp
public sealed class ApiBibleProvider : BibleProviderBase
{
    public const string ProviderName = "ApiBible";
    public const string UsageScheme  = "abs.fums.v3";

    public ApiBibleProvider(ApiBibleConfigurations configurations, ILogger<ApiBibleProvider> logger = null)
        : base(ProviderName, configurations.DefaultTranslation, logger) { … }
}
```

```csharp
public sealed class ApiBibleConfigurations
{
    public string ApiKey { get; set; } = string.Empty;                     // required
    public string BaseUrl { get; set; } = "https://rest.api.bible/v1/";
    public string DefaultTranslation { get; set; } = "KJV";                // §APB4
    public Dictionary<string, string> TranslationMap { get; set; } = new();// "NIV" -> bibleId override
    public bool UseOrgId { get; set; } = false;                            // §APB13 — never flip against stored keys
    public IList<string> ParseLanguages { get; set; } = new List<string> { "eng" };  // §ABS42.4
    public int? MonthlyRequestAllowance { get; set; } = null;              // §APB15 rule 5; null = unknown
    public TimeSpan CatalogueCacheDuration { get; set; } = TimeSpan.FromHours(6);
    public int TimeoutSeconds { get; set; } = 20;                          // overall budget for one lookup
    public int PerAttemptTimeoutSeconds { get; set; } = 5;
    public int MaxRetryAttempts { get; set; } = 2;                         // retries, not attempts: 2 ⇒ 3 attempts
}
```

A plain POCO plus an optional logger, chaining `ProviderName` and the configured
default to the base, which owns `Name`, per §ABS5 rule 1 and §ABS14.

1. **Validates eagerly and throws on construction:** non-empty `ApiKey`,
   non-blank `DefaultTranslation`, parseable `BaseUrl`, **non-empty
   `ParseLanguages` with every entry a known book-name table**, and the timeout
   budget inequality in §APB6. A composition root should force construction at startup so
   a bad key fails the host rather than the first user request (§ABS28 trap 1).
2. **The logger is optional and defaults to `null`** — replaced internally with
   `NullLogger<T>.Instance`, per §ABS5 rule 1.
3. **`ParseLanguages` is the loose-reference scope, not a catalogue filter**
   (§ABS42.4). Unlike YouVersion, this upstream's catalogue is not language-scoped
   — a plan may carry editions in many languages at once — so the languages this
   provider *reads references in* are configured separately from the editions it
   can fetch. Set it to the languages the deployment actually serves; a Spanish
   surface that leaves it `["eng"]` gets `InvalidReference` for `"Juan 3:16"` and
   then an unnecessary `/search` round-trip (§ABS21).
4. The provider then builds its **internal** `ServiceCollection` (typed client →
   broker → foundation service, §APB5) and assigns the resulting `IServiceProvider`
   to the base's `InternalServices`, which disposes it.

---

## APB4. Why the default translation is KJV (#1)

An unqualified reference is a first-class input (§ABS20), and the default is what
silently fills the gap — so the shipped value decides whether a freshly-keyed
installation works at all.

On API.Bible, **KJV is in the open-access set on Starter**, so it resolves on a
new key without spending one of the three licensed-Bible slots [unverified —
§APB23 rule 2]. `NIV` is the worst candidate: licensed, non-commercial-only, and
absent from a fresh key's catalogue.

A deployment holding a licence sets `DefaultTranslation` explicitly. **The shipped
constant is a *safe* default, not a recommended one.**

---

## APB5. Layering and the typed client (#1)

Broker → Foundation Service → provider façade, per §SOL2 rule 2. Nothing
HTTP-shaped crosses the public constructor — the provider builds its own container
from the configuration POCO it was handed:

```csharp
serviceCollection
    .AddHttpClient<IApiBibleHttpBroker, ApiBibleHttpBroker>(client =>
    {
        client.BaseAddress = new Uri(configurations.BaseUrl);
        client.DefaultRequestHeaders.Add("api-key", configurations.ApiKey);
        client.Timeout = Timeout.InfiniteTimeSpan;   // the resilience pipeline owns all timing
    })
    .AddResilienceHandler("apibible", …);
```

**The broker must never `new HttpClient()`.** A hand-constructed client held for
the process lifetime gets stale DNS and no handler rotation, and it silently
bypasses the resilience pipeline in §APB6 — the registration above is what makes
the timeouts and retries in this document real rather than decorative. Registering
`AddHttpClient` and then newing a client anyway is the specific mistake to avoid:
the registration becomes vestigial and nothing warns you.

The broker holds no logic (§SOL8 rule 1): it issues the request the foundation
service composed and returns the response. It gets no unit tests.

---

## APB6. Retry and timeout budget (#1)

| Knob | Config | Default | Note |
|---|---|---|---|
| Per-attempt timeout | `PerAttemptTimeoutSeconds` | 5 | each HTTP attempt |
| Retries | `MaxRetryAttempts` | 2 (⇒ 3 attempts) | on 408 / 429-transient / 5xx and transient socket errors |
| Backoff | — | exponential + jitter, base 0.5 s, each delay ≤ 2 s | ≤ 4 s total |
| Overall budget | `TimeoutSeconds` | 20 | 3 × 5 s + ≤ 4 s = ≤ 19 s ⇒ fits |
| `HttpClient.Timeout` | — | `Timeout.InfiniteTimeSpan` | otherwise it pre-empts the pipeline |

1. The constructor validates
   `PerAttemptTimeoutSeconds × (MaxRetryAttempts + 1) + backoffCap ≤ TimeoutSeconds`
   and throws when it does not hold (§ABS5 rule 8).
2. **`Retry-After` is honoured only when it fits in the remaining budget** —
   otherwise the pipeline stops immediately and the provider throws (§APB15),
   carrying the value.
3. **429 semantics are undocumented upstream** [verified absence — the rate-limit
   page documents quotas and says nothing about 429, `Retry-After` or any
   rate-limit header; the error-codes page documents 202, 400, 401, 403 and 404 and
   not 429]. The retry policy above therefore treats a 429 as retryable only when a
   short `Retry-After` says so, and the classification is §APB15's problem.

---

### APB6.1 How the caller's token composes with this budget (#1)

Required by §ABS33 item 8, because this provider has timeout logic and
`the-standard-cancellation-patterns` binds once it does.

1. **Link, never replace.** The foundation service creates a linked source for the
   overall budget and passes *that* token down to the broker:

   ```csharp
   using var timeoutSource = new CancellationTokenSource(
       TimeSpan.FromSeconds(configurations.TimeoutSeconds));

   using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
       cancellationToken, timeoutSource.Token);
   ```

   The caller's token is never dropped and never substituted — rule 6 of the
   skill's Dos, and its Don'ts #3.

2. **Catch the timeout arm first.** Both arms are present, in this order, because
   they are otherwise indistinguishable — both surface as
   `OperationCanceledException`:

   ```csharp
   catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
   {
       throw new ApiBibleUnavailableException(
           message: "API.Bible did not answer within the configured budget.",
           innerException: exception);          // IBibleUnavailableException
   }
   catch (OperationCanceledException)
   {
       throw;                                   // the caller went away — §ABS13 rule 3
   }
   ```

   Reversing them makes every provider timeout look like caller cancellation, which
   is the precise failure §ABS13 rule 3 exists to prevent and §ABS38 rule 7 tests
   for. The skill names the inversion as an anti-pattern (Don'ts #6) and requires
   both blocks whenever timeout logic exists (its 1.3 Defaults).

3. **The per-attempt timeout is the resilience pipeline's, not this code's**
   (§APB6). Only the overall budget is linked here; nesting a second manual source
   per attempt would duplicate what the pipeline already does.

4. **The token reaches the broker and the `HttpClient` call unbroken.** A broker
   that accepts a token and does not pass it to `SendAsync` is the silent-drop
   anti-pattern, and nothing above would catch it.

---
## APB7. Catalogue resolution (#1)

The public surface speaks in abbreviations (`"NIV"`); the upstream wants opaque
bibleIds. Every lookup therefore needs `abbreviation → bibleId` first, so it is
cached.

1. On first use, cached for `CatalogueCacheDuration`: `GET /bibles`, building
   `abbreviation → bibleId` (upper-cased, preferring `abbreviationLocal` matches,
   English first). Explicit `TranslationMap` entries win — abbreviations are not
   documented as unique and licensed access varies per key.
2. **Retain `language.id` and `language.scriptDirection` per Bible — these are
   mandatory.** The catalogue is the only source: the passage response carries no
   language, and `ScripturePassage.Language`/`ScriptDirection` are `required`
   (§ABS42.6). Both live on the `language` object of the **plain** list response
   alongside `script` [verified], so no extra parameter is needed for them.

3. **Retaining `copyright` here is optional for this provider, and the request
   must ask for it if you want it.** `copyright` is documented on the single-Bible
   endpoint and on the list only under `include-full-details=true` [verified]. Since
   the *passage* response carries `copyright` directly, `Attribution` is fillable
   without it (§APB11) — so **do not send `include-full-details=true` merely to
   cache a copyright string you are about to receive anyway.** Contrast §YVN7,
   where the catalogue is the only source and retention is mandatory.
4. Resolution is a map lookup once warm, but always re-derived from the live
   catalogue on expiry rather than hardcoded, so a translation added to or removed
   from the subscription is picked up on refresh. A miss →
   `TranslationNotSupported`.
5. **The holder is not `Lazy<Task<…>>`.** `Lazy<T>` has no expiry, so it cannot
   honour the TTL, and it memoizes a *faulted* task — one transient 503 on
   `GET /bibles` would poison a singleton provider for the process lifetime. Four
   required properties:
   - the TTL is honoured;
   - a failed fetch is **not** memoized;
   - refreshes are **single-flight** (`SemaphoreSlim(1,1)`);
   - a refresh failure with a usable previous catalogue **serves stale** and logs a
     Warning rather than failing every lookup.
6. A refresh or first fetch that fails with **no** usable previous catalogue
   throws `ApiBibleUnavailableException` — it is an availability failure, not a
   catalogue miss, and a consumer must be able to suspend the provider rather than
   read it as "this translation does not exist" (§ABS6).

---

## APB8. Endpoint selection by reference shape (#1)

`UsfmReference` (parsed by `BibleProviderBase`, with `DefaultTranslation` already
applied) → bibleId via the catalogue; miss → `TranslationNotSupported`.

**Pick the endpoint from the *shape* of the parsed reference.** The three content
endpoints are not interchangeable:

| Parsed shape | Example | Request |
|---|---|---|
| Single verse | `JHN.3.16.NIV` | `GET /bibles/{id}/verses/JHN.3.16` |
| Verse range | `JHN.3.16-JHN.3.18.NIV` | `GET /bibles/{id}/passages/JHN.3.16-JHN.3.18` |
| Whole chapter | `PSA.23.KJV` | `GET /bibles/{id}/chapters/PSA.23` |

Same query string for all three:

```
?content-type=json&include-verse-spans=true&include-notes=false&include-titles=false
&include-chapter-numbers=false&include-verse-numbers=false&fums-version=3&use-org-id=false
```

1. **Every flag is sent explicitly**, because three of the upstream defaults are
   the opposite of what this design wants (§APB2).
2. **`fums-version=3` is mandatory on every content request** — omit it and no
   `meta.fumsToken` is returned at all (§APB16). Note that **`fums-version` is not
   listed in the passages guide's own parameter table** [verified absence]; it is
   documented in the fair-use guide instead. Do not let a reader of the passages
   guide alone conclude it is optional.
3. `use-org-id` is sent **explicitly** from `UseOrgId` rather than left to the
   default, so the addressing scheme is visible in the request and in committed
   fixtures; `false` is correct because it is the only scheme both this provider and
   YouVersion can honour (§ABS17).
4. A bare chapter id sent to `/passages` is a client error, not a miss — which is
   why `FetchAsync` receives the parsed reference rather than a flattened key
   (§ABS12).
5. Degenerate ranges (`JHN.3.16-JHN.3.16`) normalize to the single-verse route.
6. **Chapter ranges** (`PSA.23-PSA.24`) are materialized before the call — fetch
   each chapter and stitch, or expand to a verse-id pair using the chapter's verse
   count. The passage id grammar is two *verse* ids [verified], so a chapter-granular
   passage id is not valid and the expansion is required; §APB23 rule 4 settles which
   of the two forms to use.

---

## APB9. The content check — never return `Found` with empty text (#1)

On a 2xx, extract scripture text with verse-number spans, section titles and note
nodes excluded. Then:

1. Every requested verse empty → **`NotFound`**.
2. Some but not all → `Found` with those ids in `MissingVerseIds`.
3. `Text` is never null, empty or whitespace on a `Found` result (§ABS16 rule 4).

**This is a rule, not an optimisation.** Verses omitted by critical-text editions —
`MAT.17.21`, `MAT.18.11`, `MRK.9.44`/`MRK.9.46`, `LUK.17.36`, `JHN.5.4`,
`ACT.8.37`, `ROM.16.24` and similar — are shipped by many publishers as an empty
verse marker carrying only a footnote, and notes are suppressed (§APB8), so the
likely upstream answer is **HTTP 200 with empty content, not 404**.

The documented 404 is narrower: **"the endpoint (or `bible_id` if applicable) you
are accessing doesn't exist"** [verified] — an endpoint or Bible that does not
exist, not a verse an edition omits. That wording makes the 200-with-empty
hypothesis more likely, not less, and it is why this check is the primary
mechanism rather than a safety net.

**Still unverified without a live key** [§APB23 rule 3] — the spike must probe
these verses and commit the real responses as fixtures. **The rule stands either
way:** without it, an omitted verse yields `Found` with `Text = ""` and a consumer
stores blank, attributed scripture.

**One documented status deserves a note:** the error guide lists **202 Accepted**
as "your request was successful, no action needed" [verified]. It is an unusual
status for a content GET. §APB14 maps any 2xx carrying text to `Found`, so 202 is
handled — but a 202 with an *empty* body must fall to rule 1 above and not be
mistaken for a pending async result.

---

## APB10. Truncation (#1)

Passage route only. Compare the response `id` with the passageId sent; API.Bible
reflects a 200-verse truncation in `id` [verified].

When they differ: still `Found`, with `IsTruncated = true`, `RequestedUsfm` = the
caller's key, `Usfm` = the range actually returned **re-suffixed with the resolved
translation** (`GEN.1.1-GEN.8.16.KJV`), and a Warning.

**The provider does not silently re-request the remainder in chunks.** Doing so
would turn one metered request into several without the caller asking, against a
5,000/month plan (§SOL12). `/verses` and `/chapters` cannot truncate.

---

## APB11. Mapping the USX JSON tree (#1)

**Everything in this section is [verified]** against the passages guide and the
styling-scripture tutorial, and the payload below is a real response shape rather
than an illustration: the node names, `attrs.style`, `attrs.verseId`, `verseCount`
and `meta.fumsToken` are all as the upstream emits them.

The `json` tree is an array of `para` nodes whose items are verse markers, text
nodes and nestable `char` nodes:

```json
{"name":"para","type":"tag","attrs":{"style":"q1"},"items":[
  {"name":"verse","type":"tag","attrs":{"number":"1","style":"v","sid":"JHN 10:1"},"items":[{"text":"1","type":"text"}]},
  {"name":"char","type":"tag","attrs":{"style":"wj"},"items":[
    {"text":"Verily, verily, I say unto you, ","type":"text","attrs":{"verseId":"JHN.10.1"}}]}]}
```

1. Each text node carries its own `attrs.verseId`, which is the reliable carrier
   for a segment's `Verse` label.
2. **API.Bible closes and reopens the `wj` `char` node at every verse marker**, so
   a red-letter speech covering five verses arrives as five separate `wj` runs.
   Stitching those back into one visual span is the renderer's job (§ABS23 rule 2),
   not the mapper's.
3. `para` style → `ScriptureBlockKind` + `Indent`: `p`/`m`/`pi` → Paragraph,
   `q1`–`q4` → Poetry with the digit as indent, `s`/`s1`–`s4` → SectionHeading,
   `li1`–`li4` → ListItem, otherwise Other.
4. `char` style → inline flags: `wj` → WordsOfJesus, `nd` → DeityName, `add`/`it` →
   Italic, OR-ing nested `char` ancestors onto each run and tagging it with the
   enclosing verse marker's `number`.
5. Field mapping: response `copyright` → `Attribution`; response `id` → `Usfm`,
   **re-suffixed with the resolved translation** (the upstream returns it
   unsuffixed; the suffix is load-bearing, §ABS17); response `reference` →
   `ProviderReference`; `verseCount` minus `MissingVerseIds` → `VerseCount`;
   `meta.fumsToken` → `Usage` (§APB16).

   From the **cached catalogue entry**, not the passage response (§APB7 rule 2):
   `language.id` → `Language`, `language.scriptDirection` → `ScriptDirection`
   (§ABS42.6). `Reference` comes from `RenderReference(usfmReference, Language)` —
   the two-argument form, rendered in the edition's own language (§ABS42.5).

   From the renderer: `ScriptureMarkup.Generated(ProviderName, "ScriptureHtmlRenderer")`
   → `Markup` when `Html` was produced, `ScriptureMarkup.None(ProviderName)` when it
   was not (§ABS43). This provider never produces `Untrusted` markup — it builds
   `Blocks` from the USX JSON tree and renders from those, so there is no path by
   which upstream markup reaches `Html`.

---

## APB12. Loose-reference fallback (#1)

Reached via `FetchByRawReferenceAsync` (§ABS21) only when the local parser failed,
so there is no parsed translation to resolve a bibleId from: use
`DefaultTranslation`'s bibleId, call `GET /bibles/{id}/search?query={reference}`,
take the first `passages` hit, and re-derive the canonical key from the returned
id.

**This override survives.** The search guide documents a response carrying **two
arrays — `verses` (individual matches, text only) and `passages` (complete passage
objects with `content`, `reference`, `verseCount` and `copyright`)** [verified].
The `passages` array is usable passage content, not a snippet, which is exactly
what this path needs. Read the `passages` array and ignore `verses`; the guide is
explicit that a `verses` entry's `text` carries no footnote references or
formatting [verified].

Other documented parameters — `limit` (default 10), `offset`, `sort`
(relevance/canonical/reverse-canonical), `range`, `fuzziness` (default AUTO)
[verified] — are left at their defaults except `limit=1`: this path wants the best
single hit, not a result page.

**One caution.** `fuzziness` defaulting to AUTO means a garbled reference can still
return a confident-looking hit for something else. §ABS21's rule is what contains
it: re-derive the key from the response and return `InvalidReference` if it will
not parse — never trust the input to have meant what the search engine decided it
meant.

---

## APB13. Versification (#1)

Every verse carries two references: `id` (as numbered by this translation) and
`orgId` (ABS's organizational scheme). They diverge wherever a translation shifts,
merges or splits verses — ABS's own example returns Psalm 3:1 as
`{ "id": "PSA.3.1", "orgId": "PSA.3.2" }`, because the Hebrew scheme counts the
superscription.

`use-org-id` selects which numbering the **request** is resolved against
[verified]. **We pin `false`.** Flipping it changes what every USFM key means, so
it must never be flipped against a store of already-persisted keys (§ABS17,
§SOL2 rule 7).

---

## APB14. Status mapping — returned (#1)

Per §ABS6: scripture outcomes **return**, availability failures **throw**.

| Upstream | Result |
|---|---|
| 2xx with text (including the documented 202) | `Found` |
| 2xx, all requested verses empty | `NotFound` (§APB9) |
| 404 | `NotFound` |
| 400 | `InvalidReference`, with the API's message. Documented as "the request was formatted incorrectly" [verified]. Our parser already validated the book code, so a 400 means the id we built is unacceptable to *this* Bible — e.g. a chapter out of range |
| 403 | `TranslationNotSupported`. Documented as **"you are requesting a Bible that you don't have access to"** [verified] — a *per-Bible* licensing miss, which is an answer rather than an outage. Reachable whenever a `TranslationMap` override names an unlicensed bibleId; log a Warning naming it. **But see §APB15 rule 6 — this mapping carries the single largest risk in the design** |
| catalogue miss (catalogue loaded, abbreviation absent) | `TranslationNotSupported` |

**Contrast 401**, documented as "we couldn't authenticate you" [verified]: the key
itself is rejected, and it is thrown as `ApiBibleAuthorizationException` (§APB15).
The key is bad ⇒ throw; this Bible is not yours ⇒ return.

---

## APB15. Exception family — thrown (#1)

Every type derives `System.Exception` and carries an abstraction marker (§SOL17 rule 6 — no shipped package references `Xeption`), which is what lets
`BibleAbstractionProvider` classify it and a consumer fail over without knowing
this provider exists. Declared per §ABS8, and **public** — a consumer holding this
provider directly may name them, though the normal path is to catch the marker.
The categorization machinery that produces them is `private` (§ABS9, §ABS11).

```csharp
// Glory2Him.BibleProviders.ApiBible/Models/Exceptions/ — PUBLIC
public sealed class ApiBibleValidationException     : Exception, IBibleValidationException { }
public sealed class ApiBibleDependencyException     : Exception, IBibleDependencyException { }
public sealed class ApiBibleServiceException        : Exception, IBibleServiceException { }

/// <summary>429 with a short Retry-After — a throughput throttle. Transient.</summary>
public sealed class ApiBibleRateLimitException      : Exception, IBibleRateLimitException
{
    public TimeSpan? RetryAfter { get; }
}

/// <summary>The plan's request allowance is spent. Persistent until the window resets.</summary>
public sealed class ApiBibleQuotaExceededException  : Exception, IBibleQuotaExceededException
{
    public DateTimeOffset? QuotaResetsOn { get; }
}

/// <summary>401, or a key that has been revoked. An operational defect.</summary>
public sealed class ApiBibleAuthorizationException  : Exception, IBibleAuthorizationException { }

/// <summary>5xx, timeout, or transport failure after the retry budget is spent.</summary>
public sealed class ApiBibleUnavailableException    : Exception, IBibleUnavailableException { }
```

**`ProviderConsole`** (§ABS7.1) is `https://api.bible/` on every dependency
exception this provider throws — the developer portal, where a key, its plan and
its usage live. It is a bare origin: no key, no account id, no query string.

**Accessibility** (§ABS11): these six types are public. `ApiBibleHttpBroker`, the
foundation service, the catalogue holder and the content mappers are `internal`;
the `TryCatch` that classifies an upstream failure into one of the six, and any
intermediate type it uses, are `private` to the provider.

| Upstream | Exception | Consumer action |
|---|---|---|
| 429, short `Retry-After` | `ApiBibleRateLimitException` | Fail over now; this provider is usable again after `RetryAfter` |
| 429, quota exhausted | `ApiBibleQuotaExceededException` | Fail over **and stop asking** until the reset. Retrying spends an allowance that is already gone |
| 401 | `ApiBibleAuthorizationException` | Fail over, log at Error — the key is rejected or revoked |
| 5xx, timeout, socket failure | `ApiBibleUnavailableException` | Fail over. Transient |
| anything unmarked escaping the provider | `ApiBibleServiceException` | A provider bug |

**Through the abstraction these arrive wrapped.** A consumer calling
`IBibleAbstractionProvider` catches `BibleAbstractionProviderDependencyException`
and reads the specific marker, `RetryAfter` and `QuotaResetsOn` off
`InnerException` (§ABS34). Only a caller holding this provider directly as
`IBibleProvider` catches the types above.

### The quota problem, stated at full strength

1. **429 is not documented anywhere on API.Bible** [verified absence]. The
   error-codes page lists 202, 400, 401, 403 and 404 and stops. The rate-limiting
   page describes plan allowances, overage billing and service disruption, and
   mentions no status code, no `Retry-After`, and no rate-limit headers at all.
2. **The rate-limiting page does say what happens at the limit:** plans default to
   no overage protection, so past the quota **"your service will be disrupted"**
   [verified]. Disruption is real and documented; its *wire representation* is not.
3. Until a spike settles it, the provider applies a documented, configurable
   threshold and **treats an absent or very long `Retry-After` as quota
   exhaustion**, because the failure mode of mistaking exhaustion for a throttle —
   hammering a plan that is already spent — is worse than the reverse.
4. `QuotaResetsOn` is set to the start of the next calendar month when nothing
   better is available, since the documented allowance is monthly [verified]. A
   guessed reset that is roughly right is more useful to a consumer's suspension
   logic than a null, and it is recorded as a guess in the message.
5. **Inferring quota locally, because the upstream exposes nothing.** Searched and
   confirmed absent: the error-codes page lists 202/400/401/403/404 and no 429; the
   rate-limiting page names no status code, no `Retry-After` and no headers; and the
   **OpenAPI definition documents only 200, 400, 401, 403 and 404 across every
   endpoint, with no `X-RateLimit-*` header and no usage, plan or quota endpoint**
   [verified]. There is nothing in a response to read.

   What *is* knowable is what this provider has spent. When
   `MonthlyRequestAllowance` is configured — the operator knows their plan; 5,000 on
   Starter, 150,000 on Pro [verified] — the provider keeps an internal count of the
   upstream requests it has issued in the current calendar month (UTC), reset on the
   month boundary, and uses it as a third discriminator:

   | Local spend vs allowance | An ambiguous 403, or a 429 with no usable `Retry-After` |
   |---|---|
   | at or over the allowance | **Quota exhausted**, high confidence |
   | under it | inconclusive — fall back to rules 3 and 5 |

   Four rules keep this honest:

   - **The count is a lower bound, never an estimate of plan usage.** Other
     instances, other applications and other keys on the same plan all spend from the
     same allowance, and the count is not persisted across a restart. So it can only
     ever *strengthen* a quota conclusion, never weaken one — which is exactly the
     asymmetry we want, because §APB15 rule 3 already errs toward quota.
   - **It never gates a request.** The provider does not refuse to call upstream
     because its own count is high; that would fabricate an outage from an estimate.
     Availability is decided by the upstream's actual response, always.
   - **It is not an observability surface**, and this does not reopen §ABS7.1. It is
     never exported, never a metric, never a public property — it is evidence for one
     classification decision, held internally. The upstream's dashboard remains the
     authority on usage, and `ProviderConsole` is how an operator gets to it.
   - **Log a Warning once when the count first crosses 80% of the allowance**, so an
     operator has notice before the plan goes dark rather than discovering it from a
     failed lookup.

   When `MonthlyRequestAllowance` is null the discriminator is simply unavailable
   and rules 3 and 5 stand alone. **Null is the shipped default** — a guessed
   allowance would produce confident wrong answers, which is worse than no answer.

6. **The risk this design cannot yet close** (§SOL16 rule 1): if a disrupted plan
   is signalled as **403** rather than 429, §APB14 maps it to
   `TranslationNotSupported` — a *returned* status. A consumer would then read an
   exhausted plan as "this provider doesn't carry that translation", fail over
   silently, and never suspend the provider or alert anyone. The 403 documentation
   says "a Bible you don't have access to" [verified], which argues against it — but
   "no plan, no access" is not an unreasonable reading of the same sentence, and
   nothing in the documentation excludes it.

   **Interim mitigation, to build now rather than after the spike:** when a 403
   arrives for a bibleId that the *currently cached catalogue contains*, that is
   not a per-Bible licensing miss — the key was told it had that Bible minutes ago.
   Treat that specific combination as an availability failure
   (`ApiBibleQuotaExceededException`), log at Error, and keep the plain
   `TranslationNotSupported` mapping for a 403 on a bibleId the catalogue never
   listed. It is cheap, it is testable against WireMock without a live key, and it
   converts the dangerous silent case into a loud one.

---

## APB16. FUMS — usage reporting (#1)

**These are contractual obligations on the consuming application.** This provider
surfaces what is needed; it cannot discharge any of them. They are stated in full
because a consumer that does not read them will be in breach without ever seeing
an error.

FUMS (Fair Use Management System) is ABS's usage reporting. It is a **licence
condition, not analytics**: ABS licenses most of its translations from publishers
and must report how much each is read. **The Terms make it mandatory** — any webapp
must implement FUMS to use API.Bible (§14), unless prohibited by local law (§3)
[verified].

**How it works.** Send `fums-version=3`; the response carries `meta.fumsToken`.
When the scripture is **displayed to a person**, that view is reported — via the
browser tracker or a server-side GET.

**The word that decides everything is *display*, not fetch.** One fetch can produce
a thousand displays (a consumer that stores the text and re-renders it) or none.

**What this provider does:** sends `fums-version=3` on every content request and
maps `meta.fumsToken` into `Passage.Usage` as
`ScriptureUsage.ReportOnDisplay(ProviderName, UsageScheme, token, bibleId)`. A
**missing** token yields `ScriptureUsage.ReportingUnavailable(...)` on a
still-**`Found`** result, logged at Warning — scripture delivery is never hostage
to a reporting field, and a missing token must be visible on the stored row rather
than become an outage.

**What this provider will never do:** call `fums.api.bible`. There is deliberately
**no `ReportFums` configuration flag**. Reporting requires the viewer's device and
session identity; a provider called at fetch time has no viewer and must not
invent one. A flag that "reports at fetch time" would look like compliance without
being it.

**What the consumer MUST do:**

1. **Persist `Usage.ToStorageString()` alongside the scripture text**, in a
   non-nullable field. If the token is dropped at mapping time, every later display
   is unreportable — with no error, no failing test, and no symptom until ABS asks.
2. **Report on render, not on fetch.** Either path is sanctioned by ABS
   [verified — both are documented in the fair-use guide]:
   - **Browser (ABS's recommended default).** Load
     `https://pkg.api.bible/fumsV3.min.js` and call `fums('trackView', [token, …])`.
     The report goes **directly from the viewer's browser to
     `https://fums.api.bible/f3`** — it does not come back through this library, the
     provider, or the consumer's server. No API key is involved; the token is the
     sole credential, which is why it is safe in page markup (§SOL2 rule 6). The
     script **mints and persists `dId`/`sId` itself**
     (`localStorage["fums.dId"]`, `sessionStorage["fums.sId"]`), so the page
     supplies neither.
   - **Server-side.** `GET https://fums.api.bible/f3?t={token}&dId={deviceId}&sId={sessionId}[&uId={userId}]`.
     GET only; no api-key; `uId` optional and hashed with SHA-256 in the browser
     implementation [verified]. The consumer must supply `dId`/`sId` it has an
     honest basis for — a long-lived cookie and its own session — not values minted
     per request, which would collapse every viewer into one device.
3. **Batch.** `trackView` accepts an array and the server path accepts repeated
   `&t=` params, so a page showing five passages is one report.
4. **Deduplicate per viewer per session.** ABS's own v1/v2 docs warn that
   reporting one token twice **double-counts** — duplicates are counted, not
   rejected. Over-reporting is as much a misreport as under-reporting, and it is the
   easier mistake to make from a store.

**There is also a `trackListen` verb for audio** [verified]. This design ships no
audio support (§SOL13), so it is named here only so nobody assumes `trackView`
covers an audio player added later.

**Four ways the browser path fails silently** — read out of ABS's shipped script,
none documented. A consumer choosing it must handle them:

| Failure | Mitigation |
|---|---|
| `localStorage` access throws (blocked cookies, some embeds, hardened privacy modes) — the script dies at module scope before installing `window.fums`, and every call queues forever with no catchable error | Fall back to the server-side path for this client, with consumer-minted `dId`/`sId`. A `<noscript>` pixel is **not** cover: scripting is *enabled* here, so `<noscript>` never renders |
| The documented shim must be an **array** — the script calls `.forEach` on it before installing `window.fums`. `window.fumsData = window.fumsData \|\| {}` permanently disables the tracker | Use `\|\| []` |
| CSP must allow **`script-src https://pkg.api.bible`** *and* **`connect-src https://fums.api.bible`** — the tracker uses `fetch`, not an image, so allowlisting `img-src` (the obvious reading of ABS's own `<img>` example) blocks it | Both directives |
| The HTTP cache can swallow repeats — `/f3` sends no `Cache-Control` but does send `ETag`, and the online URL is deterministic | Vary the URL per report, or accept the loss |

Separately, a server-rendered
`<noscript><img src="https://fums.api.bible/f3?t=…&dId=…&sId=…"></noscript>` is
worth emitting: it costs nothing and covers **JS-disabled clients only**, with
`dId`/`sId` the server supplies. Emit it **inside `<noscript>`** — a pixel rendered
unconditionally double-reports against the tracker, which rule 4 forbids.

**Neither path is monitorable.** `/f3` is a small GIF behind a CDN returning an
identical `ETag` for a valid token, a garbage token, and no parameters at all —
strong evidence reports are harvested from access logs rather than processed at
request time. A `200` proves the request left the process and nothing more. So:
never fail a render on it, swallow and log at Warning, and **do not write retry
logic that depends on distinguishing acceptance from rejection**, because you
cannot. The only real detection mechanism is a reconciliation query the consumer
owns — tokens stored versus reports sent, per month — which is computable because
every stored usage carries an `IssuedAt` (§ABS29).

---

## APB17. Content recency — Terms §11 (#1)

The Terms require that content stored offline be kept up to date with API.Bible.
Four duties, all [verified], and the last two are **removal** duties that an
earlier draft of this section missed entirely by enumerating only the first two:

1. **Check at least every 30 days for content updates** (§11).
2. **Apply an update as soon as reasonably possible, or within 24 hours of
   receiving a request** from API.Bible or the IP content owner (§11).
3. **Delete or modify any content you hold when it is deleted or modified in
   API.Bible** (§11). Refreshing is not enough on its own: a verse or an edition
   that *disappears* upstream has to disappear downstream too, and a refresh loop
   that only overwrites what it finds will silently keep serving withdrawn content.
4. **Remove all content within 72 hours** when an IP licence terminates, when
   API.Bible suspends you, or when a subscription is terminated or deactivated —
   **an unpaid plan counts as deactivated** (§10.2) — and within 72 hours of any
   removal request from API.Bible or an IP Holder (§10.3).

This is contractual and binding. Five consequences:

1. **Stored scripture is a refreshable cache, not an archive.** A design that
   persists text indefinitely breaches §11 independently of FUMS.
2. **Refresh text and usage atomically.** Never replace the text without replacing
   its `ScriptureUsage`; never report a usage whose text has been refreshed.
   `IssuedAt` makes this decidable (§ABS31).
3. **The 24-hour clause needs a mechanism the 30-day cycle does not provide** — a
   forced, on-demand refresh path for a named edition. §ABS31 states this as a
   contract-level consequence; it is repeated here because this is the provider
   whose terms create it, and because it is the obligation most likely to be missed:
   a nightly sweep looks like compliance and is not.
4. **Storage needs a delete path, not just a refresh path** (duties 3 and 4). A
   consumer that can only overwrite rows cannot honour either: it cannot drop a
   verse withdrawn upstream, and it cannot purge everything within 72 hours of a
   lapsed subscription. **Design the purge before the first row is written** — this
   is the obligation most likely to be discovered only when it is already breached,
   because nothing in normal operation exercises it.
5. **The two FAQs disagree** — 14 days on scripture.api.bible, 30 on api.bible and
   in the Terms [contested]. **The Terms govern at 30.** A consumer may use 14 and
   satisfy both, and that remains the safe recommendation, but this document no
   longer claims 14 is *required*.

A refresh cycle bounded this way also disposes of the token-lifetime question: a
token can never legitimately be older than the cycle, because every refresh mints
a new one (§APB21).

---

## APB18. Cache size and cache age (#1)

Two caching requests, both on API.Bible's own common-questions page [verified]:

1. **"You can cache data, but we request that you limit it to fewer than 500
   consecutive verses."** A real limit on how much contiguous scripture a consumer
   may hold. The per-request 200-verse passage cap [verified] bounds any single
   response this provider produces, so no one lookup can breach it — but a consumer
   stitching adjacent passages into a stored book **can**, and this is the rule that
   says not to.
2. **"We also recommend that you clear your cache every 14 days or less."**

Rule 2 settles the 14-vs-30 disagreement §APB1 flagged [contested] and §APB17
rule 4 rules on: **30 days is the binding minimum from Terms §11; 14 days is ABS's
own recommendation.** They are not in conflict — one is a floor in a contract, the
other is advice in a FAQ — and a consumer refreshing on 14 days satisfies both.

*An earlier draft of this document withdrew the 500-verse figure as unsourced,
having looked only at the Terms and the fair-use guide. It is on the
common-questions page, which also carries the plan figures §APB2 already cites —
so the page was reachable and the withdrawal was an error, not a judgement call.
Recorded because "we could not source it" is exactly the reasoning that should
leave a trail when it turns out to be wrong.*

## APB19. Attribution — Terms §7 (#1)

The Terms require more than a copyright string, and the difference matters to the
DTO. Terms §7 requires [verified]:

1. **A dedicated copyright page** carrying the Bible translation names,
   abbreviations, IP holder details and website links.
2. **A citation on individual quotations** — e.g. `(CEV)` — **hyperlinked to the
   full copyright information.**

This provider always populates `Attribution` from the passage response's
`copyright`, and `Translation` carries the abbreviation the citation needs. A null
`Attribution` on a licensed edition is a mapping or licensing defect and is logged
at Warning by the base class (§ABS32).

**Requirement 2 is the one this library does not fully serve.** `Attribution` is a
bare string; the required hyperlink needs a target, and nothing in
`ScripturePassage` carries one. Two ways to close it, and they should be decided
together with §SOL17 rule 3 because both are additions to a published DTO:

- the consumer holds `translation → copyright page URL` in its own configuration —
  cheap, correct, and duplicated per consumer; or
- `ScripturePassage` grows a nullable `AttributionUrl`, populated from the
  catalogue's per-Bible `info`/`copyright` details (§ABS39 rule 5).

Until one is chosen, **a consumer is responsible for building the link itself**,
and this document says so plainly rather than letting `Attribution` imply
compliance it does not deliver.

---

## APB20. Commercial use — Terms §9.3 (#1)

The Starter plan's three licensed Bibles are **non-commercial only**, and the
Terms define non-commercial broadly [verified]: the designation prohibits
monetization including website advertising, licensing fees, in-app promotions,
sponsorships, freemium models, paid access, "or any other situation that may
reasonably be considered as a revenue generating activity".

That is wider than most readings of "we don't sell it". **An ad-supported or
freemium surface is commercial under these Terms**, and whether a given deployment
qualifies decides whether a licensed translation (NIV, ESV, NLT) may be configured
at all. It is a business and legal question, not an engineering one. Resolve it
before configuring one.

---

## APB21. Token lifetime (#1)

No expiry rule exists in public form **[verified absence]** — and unlike §APB18,
this one was searched exhaustively rather than partially. Verified absent across the FUMS
documentation generations, the Terms, the OpenAPI spec, both trackers, and ABS's
official SDK. Two details make the silence load-bearing: **Terms §14 gives
explicit lifetimes for other FUMS fields and none for the token**, and API.Bible
*does* model expiry where it exists (audio resources carry a presigned URL with a
typed expiry).

There is also no *cryptographic* expiry evident: sampled tokens are opaque
base64url of roughly 180–198 bytes with high entropy, a short plaintext header
then full avalanche, and no readable timestamp. Byte signatures in ABS's older
documentation examples appear unchanged in recently-minted tokens from unrelated
keys, so the encoding and keying have not rotated in years and an old token still
decodes. Anything killing a stale token would be a deliberate age check during log
processing — unfalsifiable from outside, and the honest residual risk.

Supporting evidence that deferred reporting is intended: ABS's tracker keeps an
**offline queue** in `localStorage`, replays it on the `online` event with **no age
check or purge**, and stamps replays with an undocumented `&ts=<epochMs>`
backdating parameter.

**Conclusion:** persist-and-report-later is what the documented semantics
describe, and §APB17's refresh cycle bounds token age anyway. Carry the residual
risk knowingly, and ask (§APB23 rule 8).

---

## APB22. The FUMS reporter package (#1)

`Glory2Him.BibleProviders.ApiBible.Fums` ships a working
`FumsUsageReporter : IScriptureUsageReporter` with `Schemes = ["abs.fums.v3"]`, so
a consumer's residual work is a registration and a call rather than an HTTP client
and a script tag. **The project does not exist yet** (§SOL5).

1. **It does not reference `Glory2Him.BibleProviders.ApiBible`.** That makes "the
   fetching assembly can never reach `fums.api.bible`" a fact about the assembly
   graph, assertable in CI, rather than a rule a reviewer must check (§SOL2 rule 6).
   It also keeps a render tier free of an API key, a catalogue cache and a
   resilience stack to make one credential-free GET. The scheme constant is
   duplicated in both packages and pinned by a test referencing both.
2. Its `HttpClient` is its own: **no `api-key` header**, its own short timeout, and
   `fums.api.bible` allowlisted separately from `rest.api.bible` in any outbound
   firewall.
3. `ReportDisplaysAsync` is fire-and-forget by contract: it reports what was
   **sent**, never what was accepted (§APB16, §ABS30 rule 1). Tokens batch as
   repeated `&t=` params, chunked below an 8192-character URL budget, mirroring the
   tracker's own algorithm.
4. `TryCreateBrowserPayload` returns the script URL and token list for the page to
   emit. **Tokens are charset-validated before interpolation — this is an injection
   sink**, and the value came from an upstream response.

---

## APB23. Spikes — before implementation (#1)

A live API key is required. Each item is something documentation could not settle.
Items 1, 5 and 6 from an earlier draft are now **closed by §APB2 and §APB12** and
are not repeated.

1. **Distinguish a 429 throttle from an exhausted quota, and establish what a
   disrupted plan actually returns** (§APB15). The highest-value item in this
   document — it is the one unknown that can silently disable failover. Exhaust a
   test plan if that is what it takes, or ask ABS directly.
2. **Confirm `KJV` is in a fresh, unconfigured key's catalogue** — it is the
   shipped `DefaultTranslation` (§APB4).
3. **Probe the omitted-verse behaviour** — `MAT.17.21`, `ACT.8.37`, `ROM.16.24`
   against a critical-text translation. Commit the real responses as fixtures.
   §APB9 depends on this.
4. **How is a chapter range best expanded** — per-chapter fetch and stitch, or a
   verse-id pair derived from the chapter's verse count? Both are valid against the
   documented grammar; measure the request cost of each (§APB8 rule 6).
5. **Capture a psalm with a numbered superscription** (`PSA.3.1`) and confirm `id`
   vs `orgId` differ as documented, and that `use-org-id=false` returns the edition's
   own numbering.
6. **Capture fixtures** for the acceptance suite: a catalogue page, a
   `content-type=json` verse, a red-letter passage from a red-letter-capable
   edition, and a `/search` response for a reference-shaped query.
7. **Ask ABS** (support@americanbible.org): is there a consecutive-verse cache cap
   (§APB18)? Which refresh figure governs, 14 or 30 days (§APB17 rule 4)?
8. **Ask ABS:** is an undocumented age cut-off applied to stored tokens during log
   processing (§APB21)? Is `&ts=` honoured from a third-party server?

---

## APB24. Testing (#1)

Four projects, per §ABS35's conventions. Three exist; `…Fums.Tests.Unit` does not
(§SOL5).

**`…ApiBible.Tests.Unit`** — no HTTP. Catalogue mapping as a pure function
including duplicate abbreviations and `TranslationMap` precedence; USX JSON tree →
`Blocks`; status and exception mapping tables as pure functions, including the 429
throttle/quota discriminator **and the §APB15 rule 6 catalogue-aware 403 rule**;
constructor validation (null configurations, empty `ApiKey`, blank
`DefaultTranslation`, a budget that does not close); `Name` equals `ProviderName`
and the literal is unchanged.

**`…ApiBible.Tests.Acceptance`** — `WireMockServer.Start()` per test class,
`BaseUrl` repointed at it, the real provider through its real constructor, driven
only through `IBibleProvider` (§ABS35 rule 3).

1. **Happy path:** `GetScriptureByUsfmAsync("JHN.3.16.NIV")` returns `Found` with
   populated `Text`, `Html`, `Blocks` and `Attribution`; and
   `GetScriptureByReferenceAsync("John 3:16 NIV")` resolves through the local parser
   and **never touches `/search`** — assert no request reaches it.
2. Endpoint routing by reference shape, asserted on the **URL**: `JHN.3.16.NIV` →
   `/verses/…`, a range → `/passages/…`, `PSA.23.KJV` → `/chapters/…`. And
   **assert the full query string** on every content request: `fums-version=3`,
   `use-org-id=false`, and all six formatting flags explicitly present — the three
   whose upstream default is wrong for us (§APB2) are the ones a regression would
   silently restore.
3. Catalogue over the wire: resolved once and cached (one `/bibles` request across
   two lookups); a 503 on the *first* fetch does not poison the provider; a 503 on a
   *refresh* serves stale with a Warning; ten concurrent first calls make one
   request.
4. Content edge cases: 200-with-empty → `NotFound`; footnote-only → `NotFound`; a
   narrower response `id` → `Found` with `IsTruncated` and `RequestedUsfm`; a partial
   range → `MissingVerseIds`; **a 202 with an empty body → `NotFound`, not `Found`**
   (§APB9).
5. **Quota inference** (§APB15 rule 5): with `MonthlyRequestAllowance` set low,
   requests past the allowance turn an ambiguous 403 into
   `ApiBibleQuotaExceededException`; the same 403 *under* the allowance stays
   `TranslationNotSupported` for an uncatalogued bibleId. Crossing 80% logs a
   Warning exactly once. The counter **never** suppresses a request — assert the
   upstream is still called when local spend is over the allowance. With
   `MonthlyRequestAllowance` null the discriminator is inert.
6. Failure mapping: 404 → `NotFound`; 403 on an uncatalogued bibleId →
   `TranslationNotSupported`; **403 on a bibleId the cached catalogue contains →
   `ApiBibleQuotaExceededException` logged at Error** (§APB15 rule 6); 401 →
   `ApiBibleAuthorizationException`; 429-short → `ApiBibleRateLimitException` with
   `RetryAfter`; 429-quota → `ApiBibleQuotaExceededException`; 5xx after the retry
   budget → `ApiBibleUnavailableException`. Every one asserted to carry its
   abstraction marker.
7. `meta.fumsToken` → `Usage` as `ReportOnDisplay`; a response without one →
   `Found` carrying `ReportingUnavailable` plus a Warning; the provider makes **no**
   request to `fums.api.bible`.
8. `/search` returns a `passages` array → the loose fallback produces `Found` with
   a re-derived key; a `passages` array whose id will not parse → `InvalidReference`
   (§APB12).
9. A cancelled token aborts in flight and surfaces `OperationCanceledException`,
   not an exception type; a **provider-side timeout** with the caller's token
   unsignalled surfaces `ApiBibleUnavailableException` (§ABS13 rule 3).
10. **The API key appears in no captured log** (§SOL14 rule 4).

**`…ApiBible.Tests.Integrations`** — the live API. Credentials from
`APIBIBLE_API_KEY` only; every fact guarded so the suite is **skipped, not
failed**, without a key. No secret is committed. Not run by CI (§SOL7 rule 2).
Smoke coverage to catch upstream drift: fetch `JHN.3.16` in an open-access
translation and assert `Found` with non-empty `Text` and `Attribution`; fetch the
live catalogue and assert `DefaultTranslation` resolves; request an unlicensed
translation and assert `TranslationNotSupported`. **Also the permanent regression
guard for the base URL** — `rest.api.bible` is documented today and this is what
notices if it moves.

**`…ApiBible.Fums.Tests.Unit`** — URL construction and batching/chunking; the
browser payload; charset validation rejecting a malformed token; the scheme
constant matching `ApiBibleProvider.UsageScheme`.

Plus the **Conformance** suite (§ABS38), which holds this provider to the
contract.

---

## APB25. Work breakdown (#1)

Every item depends on the abstraction items 1–7 (§ABS40).

| # | Item | Contents | Est. |
|---|---|---|---|
| 1 | **Spikes** | The eight items in §APB23. Produces the fixtures §APB24 is built on, so it cannot be skipped (§SOL9) | 1–1.5 d |
| 2 | **Transport & container** | Internal `ServiceCollection`, typed client, resilience pipeline and budget validation, disposal via `InternalServices` | 0.5–1 d |
| 3 | **Catalogue** | Refreshable holder with the properties in §APB7, `TranslationMap` precedence | 1 d |
| 4 | **Lookup flow** | Shape-based endpoint routing, the explicit query string, content check, truncation check, JSON→`Blocks` mapping, the `/search` fallback | 1.5–2 d |
| 5 | **Failure mapping** | §APB14 and §APB15, including the catalogue-aware 403 rule and the 429 discriminator once the spike settles it | 0.5–1 d |
| 6 | **FUMS** | `ScriptureUsage` population, and the new `.Fums` package: server path, browser payload, batching/chunking, charset validation, scheme-pinning test | 1–1.5 d |
| 7 | **Tests** | The four projects in §APB24 plus the inherited Conformance suite | 1–1.5 d |

Provider total ≈ **6.5–9 dev-days**.
