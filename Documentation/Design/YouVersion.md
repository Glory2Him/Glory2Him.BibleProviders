# YouVersion provider

**Area prefix:** `YVN` · **Sections:** §YVN1 – §YVN21
**Package:** `Glory2Him.BibleProviders.YouVersion`
**Implements:** the contract in [Abstractions.md](Abstractions.md)
**Solution overview:** [Design.md](Design.md) · **Sibling provider:** [ApiBible.md](ApiBible.md)
**Upstream:** YouVersion Platform, operated by Life.Church

Conventions, heading tags and provenance tags: [Design.md](Design.md), "Conventions".

---

## YVN1. Upstream documentation, and a caveat this document is explicit about (#1)

| What | URL |
|---|---|
| Developer documentation (root) | https://developers.youversion.com/ |
| **API usage guide** — base URL, auth header, the passages endpoint | https://developers.youversion.com/api-usage |
| **Quick reference** — the endpoint list, status codes, rate limiting | https://developers.youversion.com/quick-reference |
| Interactive API reference | https://developers.youversion.com/api |
| **Developer portal** — app keys, per-version licence acceptance | https://platform.youversion.com |
| Versification specification (Copenhagen Alliance, YouVersion co-authored) | https://github.com/Copenhagen-Alliance/versification-specification |
| **Platform terms** — published, unread, and blocking (§YVN14) | https://platform.youversion.com/terms |

**YouVersion's public documentation is materially thinner than API.Bible's, and it
contradicts itself in three places that matter.** There is no published OpenAPI
definition, no fair-use guide, and the two pages that describe the API describe
different APIs:

| Subject | `api-usage` page | `quick-reference` page | Status |
|---|---|---|---|
| Passages endpoint | `GET /bibles/{bibleId}/passages/{passage}` | **not listed at all** | [contested] — §YVN8 |
| Chapter access | not shown | a verses endpoint that carries **no text** | resolved — §YVN9 |
| Catalogue language filter | `language_ranges` | `language_ranges`, "comma-separated" | vs `language_ranges[]` with literal brackets from practitioner reports — [contested], §YVN7 |
| Pagination request parameter | `page_token` | **`next_page_token`** | [contested] — §YVN7 |
| Page size | up to **100** | not stated | [verified] at 100 |

**Where they conflict this document names both and states which one the code
follows first, with the fallback.** Three of these are §SOL16 items because a
wrong guess is not a degraded feature — it is every call failing.

**The platform terms are published and have not been read.** The page is
client-rendered and returns no content to a fetch; it must be opened in a browser.
Until its clauses are recorded here, §YVN14 blocks persistence. This document will
not state obligations it has not read, and will not treat an unread rule as an
absent one.

---

## YVN2. What the upstream offers (#1)

Base URL `https://api.youversion.com/v1/` [verified].

| Aspect | Detail |
|---|---|
| Auth | App key from the developer portal, sent as the **`X-YVP-App-Key`** header [verified]. Not `Authorization: Bearer`; omitting it yields a generic 401 with little diagnostic value |
| Access model | **Per-version licence agreements, accepted in the portal.** `GET /v1/bibles` returns only the versions the app key is licensed for — the single biggest operational difference from API.Bible and the source of most support questions (§YVN17) |
| **`all_available`** | `all_available=true` widens the listing from "enabled for this app key" to the broader platform catalogue [verified]. This is the flag an earlier draft listed as an unknown; it exists |
| Catalogue | `GET /v1/bibles` → **numeric** ids (e.g. `3034` BSB, `111` NIV, `1` KJV). Scoped by a required language filter (§YVN7). Paginated, `page_size` up to **100** [verified] |
| Copyright | **Not on the passage response.** The Bible resource carries `copyright` (short), `promotional_content` (longer copyright text), **`publisher_url`** — "URL to link to publisher page from the reader's footer" — and `info` [verified]. The catalogue cache must retain them or `Attribution` is unfillable (§YVN7 rule 4) |
| Passage | `GET /v1/bibles/{bibleId}/passages/{usfm}` — e.g. `/v1/bibles/3034/passages/JHN.3.16` [verified on the api-usage page; absent from the quick reference — §YVN8] |
| Chapter navigation | `GET /v1/bibles/{id}/books/{book_usfm}/chapters/{n}/verses` [verified] — returns `{id, passage_id, title}` per verse and **no text**; the reference says to use `/passages` for content. §YVN9 |
| Response | `{ "id": "JHN.3.16", "content": "<p>…</p>", "reference": "John 3:16" }`. Collections wrap as `{ "data": [...], "next_page_token": "…" }` [verified] |
| **Content format** | `format` takes `text` or `html`, and **defaults to `text`** [verified]. HTML is the opt-in, not the default. §YVN10 |
| `reference` | **Localized to the version's language** [verified], and its form is not contractual. §ABS16 rule 2 is why we never use it for `Reference` |
| Red letter | No documented JSON alternative; whatever red-letter markup exists arrives as spans inside the HTML. The class vocabulary is **[unverified]** and must be confirmed against a licensed red-letter version (§YVN19). Treat as best-effort |
| Loose reference | **No server-side reference parsing.** The API takes USFM only, so loose references are parsed locally (§ABS19) and this provider does not override `FetchByRawReferenceAsync` |
| Versification | **No `orgId` equivalent and no `use-org-id`-style parameter.** The `id` echoed back is the version's own numbering. YouVersion co-authors the Copenhagen Alliance versification specification precisely because references do not map across editions — but none of that is exposed through the Platform API. This is why §ABS17 pins edition-native numbering: it is the only scheme both providers can honour |
| Usage reporting | **No FUMS equivalent, and no reporting obligation discovered.** §YVN15 |
| Out of scope | `/v1/verse_of_the_days/{day}` returns a curated verse [verified]. Not a reference lookup; §SOL13 |

---

## YVN3. Identity and configuration (#1)

```csharp
public sealed class YouVersionProvider : BibleProviderBase
{
    public const string ProviderName = "YouVersion";

    public YouVersionProvider(YouVersionConfigurations configurations, ILogger<YouVersionProvider> logger = null)
        : base(ProviderName, configurations.DefaultTranslation, logger) { … }
}
```

There is no usage `Scheme` constant, because this provider declares no reporting
obligation (§YVN15).

```csharp
public sealed class YouVersionConfigurations
{
    public string AppKey { get; set; } = string.Empty;                     // required — header X-YVP-App-Key
    public string BaseUrl { get; set; } = "https://api.youversion.com/v1/";
    public string DefaultTranslation { get; set; } = "KJV";                // §YVN4
    public IList<string> LanguageRanges { get; set; } = new List<string> { "eng" };  // required upstream; also the parse scope (§ABS42.4)
    public bool IncludeAllAvailable { get; set; } = false;                 // §YVN7 rule 6
    public Dictionary<string, int> TranslationMap { get; set; } = new();   // "NIV" -> 111 override
    public TimeSpan CatalogueCacheDuration { get; set; } = TimeSpan.FromHours(6);
    public int MaxStitchedVerses { get; set; } = 30;                       // §YVN9
    public int TimeoutSeconds { get; set; } = 20;
    public int PerAttemptTimeoutSeconds { get; set; } = 5;
    public int MaxRetryAttempts { get; set; } = 2;
}
```

Plain POCO plus optional logger, per §ABS5 rule 1.

1. **Validates eagerly and throws on construction:** non-empty `AppKey`, non-blank
   `DefaultTranslation`, **non-empty `LanguageRanges`** (the upstream rejects the
   catalogue call without it), parseable `BaseUrl`, and the timeout budget
   inequality (§YVN6).
2. The **logger is optional and defaults to `null`**, replaced internally with
   `NullLogger<T>.Instance`.
3. **`LanguageRanges` does double duty**: it scopes the catalogue call upstream
   (§YVN7 rule 1) *and* is the loose-reference parse scope (§ABS42.4). That is
   deliberate and is the reason this provider needs no second setting — the
   languages it can serve and the languages it can read references in are the same
   list by construction, which is the invariant a deployment would otherwise have
   to maintain by hand (contrast §APB3 rule 3).
4. It then builds its internal `ServiceCollection` and assigns the resulting
   `IServiceProvider` to `InternalServices`, which the base disposes.

---

## YVN4. Why the default translation is KJV, and the caveat (#1)

KJV is version id `1` and is public domain, so it is the most plausible
translation to be available on any key. **But YouVersion gates access per accepted
licence agreement**, and whether a *fresh* app key sees KJV without accepting
anything in the portal is **[unverified]** (§YVN19 rule 2).

If it does not, `DefaultTranslation` must be set to a version the deployment's key
has actually accepted — otherwise every unqualified reference returns
`TranslationNotSupported` from this provider. The constructor cannot detect this at
startup without a network call, so **the first catalogue load logs at Error when
the configured `DefaultTranslation` is absent from the resolved catalogue**
(§SOL14 rule 3).

`NIV` is the worst possible default here for the same reason it is on API.Bible:
licence-gated, and the origin of the recurring *"why is NIV `NotSupported`?"*
support question.

---

## YVN5. Layering and the typed client (#1)

Identical in shape to §APB5: Broker → Foundation Service → provider façade, with
the broker registered as a **typed client** on the provider's internal
`ServiceCollection`. Nothing HTTP-shaped crosses the public constructor.

```csharp
serviceCollection
    .AddHttpClient<IYouVersionHttpBroker, YouVersionHttpBroker>(client =>
    {
        client.BaseAddress = new Uri(configurations.BaseUrl);
        client.DefaultRequestHeaders.Add("X-YVP-App-Key", configurations.AppKey);
        client.Timeout = Timeout.InfiniteTimeSpan;   // the resilience pipeline owns all timing
    })
    .AddResilienceHandler("youversion", …);
```

The broker holds no logic and gets no unit tests (§SOL8 rule 1).

---

## YVN6. Retry and timeout budget (#1)

Per §ABS5 rule 8, with this provider's numbers: per-attempt **5 s**, **2** retries
(⇒ 3 attempts), backoff exponential + jitter with base 0.5 s and each delay capped
at 2 s (**≤ 4 s** total), overall budget **20 s**, `HttpClient.Timeout` left
`Timeout.InfiniteTimeSpan` so the pipeline owns all timing.

1. The constructor validates the closure inequality —
   `PerAttemptTimeoutSeconds × (MaxRetryAttempts + 1) + backoffCap ≤ TimeoutSeconds`
   (5 × 3 + 4 = 19 ≤ 20) — and throws when it does not hold.
2. **`Retry-After` is documented here, unlike on the sibling provider.** The quick
   reference states that a 429 response carries a **`Retry-After` header** and
   recommends exponential backoff [verified]. So this provider has a real
   discriminator input where §APB15 has none.
3. `Retry-After` is honoured only when it fits the remaining budget; otherwise the
   provider stops and throws (§YVN13).

---

### YVN6.1 How the caller's token composes with this budget (#1)

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
       throw new YouVersionUnavailableException(                          // IBibleUnavailableException
           message: "YouVersion did not answer within the configured budget.",
           innerException: new TimeoutException(
               $"No response within {configurations.TimeoutSeconds}s."));
   }
   catch (OperationCanceledException)
   {
       throw;                                   // the caller went away — §ABS13 rule 3
   }
   ```

   **The timeout arm wraps a `TimeoutException`, never the
   `OperationCanceledException` itself.** The skill permits wrapping a
   `TimeoutException` as a dependency failure (CP-012) and forbids wrapping an
   `OperationCanceledException` in any service or dependency exception (CP-013),
   which is exactly what an earlier draft of this section did. Discarding the
   caught exception costs nothing: it carries no detail beyond "a linked source
   fired", and `timeoutSource.IsCancellationRequested` in the filter already
   established *which* source. §ABS13 rule 3's outcome is unchanged — a provider
   timeout still reaches the consumer as `IBibleUnavailableException`.

   Reversing them makes every provider timeout look like caller cancellation, which
   is the precise failure §ABS13 rule 3 exists to prevent and §ABS38 rule 7 tests
   for. The skill names the inversion as an anti-pattern (Don'ts #6) and requires
   both blocks whenever timeout logic exists (its 1.3 Defaults).

3. **The per-attempt timeout is the resilience pipeline's, not this code's**
   (§YVN6). Only the overall budget is linked here; nesting a second manual source
   per attempt would duplicate what the pipeline already does.

4. **The token reaches the broker and the `HttpClient` call unbroken.** A broker
   that accepts a token and does not pass it to `SendAsync` is the silent-drop
   anti-pattern, and nothing above would catch it.

---
## YVN7. Catalogue resolution (#1)

1. **Built per configured language range and merged.** `/bibles` returns results
   from the first language range that has any Bibles — the model is "the user speaks
   these languages in this preference order; give me the best one you have"
   [verified] — so a single pass yields one language's versions rather than the
   key's whole catalogue. One call per range in `LanguageRanges`, each paginated to
   exhaustion, merged into `abbreviation → (numeric id, copyright text)`.

2. **The language parameter's spelling is [contested] and the provider must
   tolerate both.** Practitioner reports and the api-usage examples give
   **`language_ranges[]`, with the square brackets as literal characters in the
   parameter name**, and report HTTP **422 "Field required"** when the brackets are
   omitted [verified]. The quick reference calls it `language_ranges`,
   comma-separated [verified]. **Send `language_ranges[]` first**; on a 422 whose
   body names the field, retry once with the bare spelling and log at Warning. The
   fallback is three lines and removes a total-failure mode from a documentation
   contradiction we cannot resolve without a key (§YVN19 rule 5).

3. **Retain the language code and script direction too.** `ScripturePassage.Language`
   and `ScriptDirection` are `required` (§ABS42.6) and the passage response carries
   neither. The catalogue call is already language-scoped so the code is known from
   the range that matched; **the per-version script direction field is [unverified]**
   (§YVN19 rule 10). Where it cannot be read, map from the language code against the
   built-in table and fall back to `Unknown` — never to `LeftToRight`.

4. **Retain the copyright text here — for this provider it is mandatory**, and
   retain `publisher_url` and `promotional_content` with it. This catalogue is the
   only place any of it exists, and `publisher_url` is the one thing either upstream
   offers toward the copyright-page requirement API.Bible's terms impose
   (§ABS44.5). It is
   not on the passage response, so if the catalogue does not keep it, `Attribution`
   cannot be populated at all. Contrast §APB7 rule 2, where the passage response
   carries it and catalogue retention is optional.

5. **Pagination's request parameter is [contested] too.** The response field is
   `next_page_token` [verified]; the api-usage page says to send it back as
   **`page_token`** [verified], and the quick reference lists `next_page_token` as
   the request parameter [verified]. The failure is silent either way — a wrong
   parameter name is ignored and page one is returned again or the loop ends early,
   which publishes a partial catalogue and turns licensed translations into
   `TranslationNotSupported`. **Send `page_token`, and detect the failure rather
   than trusting it:** if a second request returns a first page identical to the
   previous one, or returns the same `next_page_token`, treat the pagination
   parameter as rejected, retry with `next_page_token`, and log at Warning.

6. **`all_available` is a configuration choice with a real trade-off.** Default
   `false` — the listing then means "what this app key may fetch", which is what
   `TranslationNotSupported` should mean. Setting it `true` makes the catalogue
   describe the platform rather than the key, so a translation would resolve to an
   id the key cannot actually read, converting a clean `TranslationNotSupported`
   into a 403 per lookup. **Leave it off unless a deployment specifically wants the
   wider list for diagnostics** — and if it is on, the 403 mapping in §YVN12 is what
   catches the difference.

7. **A catalogue miss may not be the last word** [unverified, §YVN19 rule 7].
   Practitioner reports state that **a passage can still be fetched for a Bible that
   the listing endpoint did not return**. If that holds, "absent from the catalogue"
   is not sound evidence of "not fetchable", and this provider's
   `TranslationNotSupported` is over-eager for any translation named explicitly in
   `TranslationMap`. **Interim rule:** a `TranslationMap` entry is authoritative —
   if the caller mapped `"NIV" → 111`, attempt the fetch even when `111` is absent
   from the resolved catalogue, and let the upstream's own 403/404 decide. A
   catalogue miss for a translation *not* in `TranslationMap` stays
   `TranslationNotSupported`.

8. **Because the list is licence- *and* language-filtered,
   `TranslationNotSupported` from this provider means "not available to this app key
   in its configured language ranges"** — broader than "not licensed" and broader
   still than "does not exist". Surface `LanguageRanges` in diagnostics so the
   ambiguity is resolvable (§YVN18).

9. Same cache-holder requirements as §APB7 rule 5 — TTL honoured, faults **not**
   memoized, single-flight refresh, serve-stale-on-failure — **plus a fifth this
   provider needs and that one does not, because its catalogue is a single call: a
   refresh is atomic.** Build the complete merged map across every range and every
   page, and swap it in only if all of them succeeded. A partial build is discarded,
   never published: serve stale if a previous catalogue exists, otherwise fail the
   lookup as an availability exception. Publishing a partial map would silently turn
   licensed translations into `TranslationNotSupported`.


### YVN7.1 Serving `GetTranslationsAsync` (#3)

§ABS44 is a **projection of this cache**, not a second one and not a second call.
Once warm it is a map over the holder above; on a cold cache it triggers the same
single-flight fetch, and a fetch that fails with no usable previous catalogue
**throws** rather than returning empty (§ABS44.2) — an outage must never read as
"this provider carries nothing".

Field mapping: `abbreviation` → `Abbreviation`, `name` → `Name`, the matched language range → `Language`, the retained script direction → `ScriptDirection`, the numeric id → `ProviderEditionId`, the retained copyright → `Attribution`, and `publisher_url` → `PublisherUrl` — both **are** populated here, because this catalogue is the only place either exists (rule 4).

---

## YVN8. Lookup flow (#1)

1. `UsfmReference` (parsed by `BibleProviderBase`, with `DefaultTranslation`
   already applied) → numeric id via the catalogue, subject to §YVN7 rule 7; miss →
   `TranslationNotSupported`. Reduce to the provider key `JHN.3.16` (translation
   stripped).

2. `GET /bibles/{id}/passages/{usfm}` → `{ id, content, reference }`.

   **The passages endpoint is documented on one page and absent from the other**
   [contested — §YVN1]. It is used in the api-usage guide's own worked example, its
   path parameter is documented as taking a verse or chapter USFM [verified], and
   independent integrations rely on it — so this design treats it as real. But
   §YVN19 rule 1 must confirm it, because the quick reference's silence is equally
   consistent with it being undocumented-but-working or with it being deprecated.

   **If it is gone, this provider has no way to return scripture at all**, and that
   is worth stating rather than glossing: the chapter-verses endpoint returns
   `{id, passage_id, title}` and no text (§YVN9), so it cannot be the fallback — an
   earlier draft said it could, which was circular, since §YVN9's chapter route *is*
   `/passages`. The only remaining shape would be whatever endpoint the quick
   reference intends for content, which this design has not found. **Treat
   §YVN19 rule 1 as an existential spike for this provider**, not a detail: if
   `/passages` is gone the design does not degrade, it stops.

3. **Build the passage.** `Usfm` = the response `id` **re-suffixed with the
   resolved translation** — the translation is stripped for the request only, and
   storing it unsuffixed loses the load-bearing part (§ABS17). `reference` from the
   response → `ProviderReference` **verbatim**.
   `Usage = ScriptureUsage.NotRequired(ProviderName)` (§YVN15).

   From the **cached catalogue entry** (§YVN7 rules 3–4): copyright →
   `Attribution`, language code → `Language`, script direction → `ScriptDirection`,
   falling back to `Unknown` rather than `LeftToRight` where the upstream does not
   supply it (§ABS42.6). `Reference` comes from
   `RenderReference(usfmReference, Language)` — **the two-argument form**, rendered
   in the edition's own language (§ABS42.5), which matters more here than on the
   sibling provider because this upstream's catalogue is language-scoped and a
   non-English deployment is the normal case rather than the exception.

   From the renderer: `ScriptureMarkup.Generated(ProviderName, "ScriptureHtmlRenderer")`
   → `Markup` when `Html` was produced, `None` when it was not (§ABS43). **Note the
   trap specific to this provider:** the upstream returns HTML and it would be
   tempting to pass `content` straight through to `Html`. That path produces
   `Untrusted` markup by definition — it is not ours — so it must either not exist
   or say so. §YVN10 rule 4 is the supported route: parse to `Blocks`, render from
   those.

4. `GetScriptureByReferenceAsync` uses the local `LooseReferenceParser` then the
   USFM path. This provider does **not** override `FetchByRawReferenceAsync`: there
   is no server-side reference parsing to fall back on, so a reference the local
   parser cannot read stays `InvalidReference` (§ABS21).

---

## YVN9. Chapters and ranges — cheaper than assumed (#1)

An earlier reading of this upstream assumed no chapter endpoint existed and that a
whole chapter would cost one request per verse. That was wrong — but so was the
correction that replaced it, and the difference matters.

**The chapter-scoped verses endpoint returns no scripture.**
`GET /v1/bibles/{version_id}/books/{book_usfm}/chapters/{chapter_number}/verses`
exists and returns `{id, passage_id, title}` per verse; the reference states
explicitly that it **"does not include the text content; use the Passages endpoint
for that"** [verified]. It is a *navigation* endpoint. A design that fetched
chapters from it would return `Found` with no text, which §YVN11 exists to catch —
but catching it is not the same as not doing it.

1. **A whole chapter is still one request — through `/passages`, not through
   `/verses`.** The passages endpoint takes a USFM `passage_id`, and a bare chapter
   id (`PSA.119`) is a legal one, so the chapter route is
   `GET /bibles/{id}/passages/PSA.119`. **[verified]** — the reference documents the
   path parameter as "The passage identifier (verse or chapter USFM format)", so a
   chapter id is explicitly in scope and this half of §YVN19 rule 3 is **closed**.
   Range syntax is a separate question and stays open (rule 4).
2. Psalm 119 costs one call, or two, and not 176. Either way this removes the worst
   request-cost figure in the design (§SOL12) — the conclusion survives; only the
   endpoint that delivers it changed.
3. **A chapter range is one request per chapter** — two for `PSA.23-PSA.24`.
4. **A verse range is the open question.** Whether `/passages/{usfm}` accepts
   `JHN.3.16-JHN.3.18` is [unverified] (§YVN19 rule 3). If it does, a range is one
   request. If it does not, **fetch the enclosing chapter once and slice** — not one
   request per verse. A three-verse range inside one chapter is one call either way,
   and a cross-chapter range is one call per chapter spanned.
5. **`MaxStitchedVerses` (default 30) survives as a bound, not as a request
   budget.** With chapter-granular fetching the request count is bounded by chapters
   spanned, not verses requested, so the setting now guards the *size of the result*
   rather than the cost of producing it. Exceeding it sets `IsTruncated`.
6. Verses returning empty go into `MissingVerseIds`.
7. **Whatever the spike settles, record it here**, and log at Debug when a lookup
   costs more than one upstream request so the real cost is visible during tuning
   (§SOL14 rule 2).

---

## YVN10. Content parsing — and the `format=text` correction (#1)

**The default is `format=text`, and `html` is the opt-in** [verified]. An earlier
reading of this upstream had this backwards twice over: first assuming HTML-only,
then assuming HTML-by-default. Neither is right, and the correct default makes the
plain-text path the cheap one.

The design that follows:

1. **`Text` comes from `format=text`.** It is the upstream's own plain-text
   projection, it needs no tag-stripping, and it is not affected by a markup change.
   This also makes §YVN11's content check simpler and far more reliable: a blank
   plain-text response is unambiguous in a way that "HTML that contains no text
   nodes" is not.
2. **`Blocks` and `Html` still need the HTML rendition**, because `format=text`
   discards poetry indentation, section headings and red-letter markup, and those
   are §ABS22's whole point.
3. **That is two requests for one passage, which is not acceptable by default.**
   So: **send `format=html` explicitly and derive `Text` from it** through
   `ScriptureHtmlRenderer` (§ABS23 rule 3), exactly as the sibling provider does —
   with `format=text` held as a **diagnostic and fallback** path rather than the
   normal one. Sending `format` explicitly rather than relying on the default is
   the same discipline §APB8 rule 1 applies to API.Bible's flags: a default that is
   wrong for us is a default we name in the request. Specifically:
   - **Fallback:** when HTML parsing yields empty or whitespace text but the
     response body was non-empty, re-request once with `format=text` before
     concluding `NotFound`. That converts a parser failure — the risk created by rule
     5 below — from a wrong answer into a correct one at the cost of one extra
     request in a rare case.
   - **Diagnostic:** the integration suite fetches both renditions for the same
     reference and asserts the extracted text matches, which is how a silent upstream
     markup change gets caught (§YVN20).
4. **Parse `content` with AngleSharp** — a real HTML parser, never a regex. Block
   elements → `ScriptureBlockKind` + `Indent` by class (`q1`/`q2` → Poetry with the
   indent); spans → inline flags. Trim the stray whitespace the API is known to leave
   in.
5. **Unknown classes degrade to `Paragraph`/`None`: text stays correct, style is
   lost** (§ABS22 rule 4). That graceful degradation matters more here than anywhere
   else in the solution, because the class vocabulary is [unverified] (§YVN19
   rule 4) — this provider ships against a guess, and the guess is designed to fail
   quietly in the right direction.
6. **The AngleSharp dependency is therefore justified for `Blocks` only**
   (§SOL6). If the spike finds the markup too thin to yield useful blocks, revisit:
   a provider that returns `format=text` with empty `Blocks` and null `Html` is a
   legitimate, contract-compliant provider, and would drop a dependency.

---

## YVN11. The content check (#1)

Apply the same rule as §APB9: after parsing, a passage whose extracted text is
empty or whitespace is `NotFound`, never `Found` with an empty `Text`.

**This provider is *more* exposed than the sibling and the rule matters more:** the
response carries no `verseCount`, so a verse omitted by the edition returning
`<p></p>` or footnote-only markup is indistinguishable from a hit on status code
alone. The same critical-text omissions apply — `MAT.17.21`, `MRK.9.44`,
`JHN.5.4`, `ACT.8.37`, `ROM.16.24` and similar.

**One documented status may make this easier than expected. `204 No Content` is in
the quick reference's status list** [verified]. If an omitted verse returns 204
rather than 200-with-empty, that is a clean, unambiguous signal and the content
check becomes a safety net rather than the primary mechanism. §YVN19 rule 8 must
determine which it is. **Map 204 to `NotFound` regardless** — a successful response
with no content is not a `Found` result under any reading (§ABS16 rule 4).

---

## YVN12. Status mapping — returned (#1)

Per §ABS6: scripture outcomes **return**, availability failures **throw**.

**Status codes are [verified]** from the quick reference's documented list — 200,
204, 400, 401, 404, 406, 429. **What each one *means* for scripture is ours**, not
the upstream's: the reference lists codes without per-endpoint semantics, so the
403 row's reading as an unaccepted per-version licence is inference from the
access model (§YVN2, §YVN17) and is **[unverified]** until §YVN19 rule 2 exercises
a real unlicensed version.

| Upstream | Result |
|---|---|
| 2xx with text | `Found` |
| **204 No Content** [verified status] | `NotFound` (§YVN11) |
| 2xx, extracted text empty | `NotFound` (§YVN11) |
| 404 | `NotFound` |
| 403 | `TranslationNotSupported` — an unaccepted *per-version* licence, which is an answer rather than an outage. **Contrast 401**, which means the app key itself is rejected and is thrown (§YVN13) |
| 400 | `InvalidReference`, with the API's message — our parser validated the book code, so a 400 means the id we built is unacceptable to *this* version |
| catalogue miss, translation not in `TranslationMap` | `TranslationNotSupported` (meaning: not available to this app key in its configured language ranges) |

**`406 Not Acceptable` is documented** [verified] and has no natural scripture
meaning. It indicates a content-negotiation failure — a wrong `Accept` header or an
unsupported `format` value — which is a defect in this provider, not an answer.
Map it to `YouVersionServiceException` (§ABS8's "the provider malfunctioned"), not
to a status, and log at Error. It is the status most likely to appear if §YVN10's
`format=text` fallback is built wrong.

---

## YVN13. Exception family — thrown (#1)

Every type derives `System.Exception` and carries an abstraction marker (§SOL17 rule 6 — no shipped package references `Xeption`) (§ABS7, §ABS8).
Declared **public**; the categorization machinery that produces them is `private`
(§ABS9, §ABS11).

```csharp
// Glory2Him.BibleProviders.YouVersion/Models/Exceptions/ — PUBLIC
public sealed class YouVersionValidationException    : Exception, IBibleValidationException { }
public sealed class YouVersionDependencyException    : Exception, IBibleDependencyException { }
public sealed class YouVersionServiceException       : Exception, IBibleServiceException { }

/// <summary>429 with a short Retry-After — a throughput throttle. Transient.</summary>
public sealed class YouVersionRateLimitException     : Exception, IBibleRateLimitException
{
    public TimeSpan? RetryAfter { get; }
}

/// <summary>The app key's allowance is spent. Persistent until the window resets.</summary>
public sealed class YouVersionQuotaExceededException : Exception, IBibleQuotaExceededException
{
    public DateTimeOffset? QuotaResetsOn { get; }
}

/// <summary>401, or an app key that has been revoked or suspended.</summary>
public sealed class YouVersionAuthorizationException : Exception, IBibleAuthorizationException { }

/// <summary>5xx, timeout, transport failure, or 422 from a malformed catalogue request.</summary>
public sealed class YouVersionUnavailableException   : Exception, IBibleUnavailableException { }
```

**`ProviderConsole`** (§ABS7.1) is `https://platform.youversion.com` on every
dependency exception this provider throws — the portal where app keys live and
where per-version licences are accepted. That makes it unusually load-bearing here:
§YVN17's licence-acceptance trap is the most common cause of a confusing failure,
and the portal is the first place support guidance sends someone.

**Accessibility** (§ABS11): these seven types are public. `YouVersionHttpBroker`,
the foundation service, the catalogue holder and the AngleSharp mapper are
`internal`; the `TryCatch` and any intermediate type it uses are `private`.

| Upstream | Exception | Consumer action |
|---|---|---|
| 429 with a short `Retry-After` | `YouVersionRateLimitException` | Fail over now; usable again after `RetryAfter` |
| 429 with an absent or very long `Retry-After` | `YouVersionQuotaExceededException` | Fail over **and stop asking** until reset |
| 401 | `YouVersionAuthorizationException` | Fail over, log at Error — the app key is rejected, revoked or suspended |
| 5xx, timeout, socket failure | `YouVersionUnavailableException` | Fail over. Transient |
| **422 on the catalogue call** | `YouVersionUnavailableException`, logged at **Error**, after §YVN7 rule 2's one retry | A malformed language filter — a configuration or spelling defect surfacing as unavailability. Fail over, but this needs fixing |
| 406 | `YouVersionServiceException` | A provider bug — §YVN12 |
| anything unmarked escaping the provider | `YouVersionServiceException` | A provider bug |

**This provider's 429 story is better documented than its sibling's.** The quick
reference documents **429 with a `Retry-After` header** [verified], so the
throttle case has a real input rather than a guess. What remains [unverified] is
whether the platform distinguishes a throughput throttle from an exhausted
allowance at all, and whether any published quota exists — no limit figures and no
`X-RateLimit-*` header set are documented, though such headers are reported
informally (§YVN19 rule 6).

Until the spike settles it the provider applies the same conservative rule as
§APB15 rule 3: **an absent or very long `Retry-After` is treated as quota
exhaustion**, because hammering an allowance that is already spent is the worse
failure. **Both exception types ship regardless** — §ABS8 requires a provider
either to define them or to state explicitly that the upstream has no such
concept, and here the honest answer is "not yet known", not "none".

**Through the abstraction these arrive wrapped** as
`BibleAbstractionProviderDependencyException` (§ABS34).

---

## YVN14. Terms — read, and they move the blocker rather than lifting it (#1)

**Read in a browser on 2026-09-11; the published version is dated 17 August 2026**
[verified]. The page is client-rendered and returns nothing to a fetch, which is
why it stayed unread for so long.

**The headline is not what this section expected.** It assumed the platform terms
were the instrument governing stored scripture, and that reading them would settle
a retention figure. They are not, and it does not:

> "This Agreement is limited to the YV IP. We are not providing You rights in
> biblical works or works other than YV IP, which You must obtain from their
> respective owners and licensors."

**The platform terms grant no rights in the Bible text at all.** "YV IP" is the
platform and the developer tools — the API, the SDKs — and scripture is explicitly
outside it. So the terms contain no retention clause, no refresh cadence and no
consecutive-verse cap **because they are not the agreement that would carry one**
[verified absence, and now a meaningful one].

### YVN14.1 Where the storage question actually lives (#1)

Two instruments, neither of them this one:

1. **The per-version licence agreements** accepted in the portal (§YVN17). These
   are with the publishers, and the terms say YouVersion passes the developer's own
   details to them to check eligibility — "those third parties require that we
   collect from You and share with them certain personally identifiable information
   ('Developer PII') to ensure that You meet and maintain the standards by the
   third-party license".
2. **"YVP Terms"** — per-Tool terms published in the platform, incorporated by
   reference, and which **override this agreement where they conflict**: "in the
   event of a conflict among the terms of this Agreement and the YVP Terms, the YVP
   Terms shall govern for the Tool to which they apply."

**So rule 1 of the old version of this section stands, for a better reason.** Do
not persist scripture from this provider yet — not because a figure is unread, but
because the agreement that would set one has not been identified. What changed is
what closes it: reading the platform terms was never going to, and §YVN19 rule 9
was aimed at the wrong document. **Whoever accepts a version in the portal must
record what that agreement says about retention**, and the YVP Terms for the Bible
tool must be located and read.

### YVN14.2 What the platform terms *do* impose (#1)

All [verified], all inherited by the consuming application, and none of them
previously in this design:

1. **Scripture must be reproduced verbatim.** The AI clause permits retrieving and
   displaying scripture "provided that the biblical text is reproduced
   word-for-word and is 100% accurate to, and unaltered from, the licensed source
   text". **This binds §YVN10 and §ABS23 directly**: the renderer regenerates
   `Text` from `Blocks` and strips markup, and that pipeline must not alter a
   character of the scripture itself. Whitespace normalisation (§YVN10 rule 4) is
   the place to be careful — trimming the upstream's stray whitespace is fine;
   "tidying" punctuation or quotation marks is not.
2. **Two AI prohibitions that reach the product, not the library.** The Tools may
   not be used with AI for open-ended chat with a user — verbatim scripture
   retrieval is the stated exception — and may not be used to train, develop,
   refine or improve any AI technology. Anything beyond that needs YouVersion's
   prior written approval.
3. **Built-in usage reporting must be left enabled:** "You shall enable and
   maintain any usage reporting mechanisms built into YV IP." See §YVN15 — it does
   not create a per-display obligation, but it does forbid disabling one.
4. **Commercial use is permitted, with a disclosure.** If the application charges a
   fee, it "will conspicuously and explicitly advise Users that the YouVersion Bible
   App is provided at no cost to the User." **Markedly more permissive than
   API.Bible's Terms §9.3** (§APB20), which bars advertising, freemium and
   sponsorship outright on its non-commercial tier. The two upstreams are not
   interchangeable on this point, and an application that is commercial may be able
   to serve YouVersion editions while being unable to serve API.Bible's licensed
   ones.
5. **The app key is confidential and a loss is notifiable.** It may not be shared
   with any third party, and YouVersion must be told if it is "lost, stolen, or
   misused". §SOL2 rule 6 and §SOL14 rule 5 already keep it out of logs; the
   notification duty is new and belongs to whoever operates the deployment.
6. **The YouVersion marks may not be used** — "YouVersion", "YVP", "Life.Church",
   "The Bible App" — unless a Tool's YVP Terms allow it. This constrains §YVN16:
   attribution must name the *version* and its copyright holder, not brand the
   feature as YouVersion's.
7. **Termination is 30 days either way, or immediate for breach**, after which the
   licence ceases. Note this covers the *tools*, not the text; the text is the
   per-version agreement's problem (§YVN14.1).
8. Governing law is Oklahoma, with a class-action waiver.

### YVN14.3 What they still do not say (#1)

No retention period, no refresh cadence, no caching cap, and no attribution
*format* — and after §APB18, "absent" is a claim this design makes carefully. Here
it is a sound absence for items that would live in a different agreement
(§YVN14.1), and an open question for attribution format, which could plausibly sit
in the YVP Terms.

## YVN15. Usage reporting — none found (#1)

This provider declares `ScriptureUsage.NotRequired(ProviderName)` on every passage:
a **positive assertion that nothing is owed**, not an absence (§ABS29). No FUMS
equivalent, no per-display reporting mechanism and no tracking token appear in the
platform's documentation **[verified absence]**, and the platform terms — now read
(§YVN14) — create no per-display reporting duty either.

**They do create a narrower one, and `NotRequired` survives it:** "You shall enable
and maintain any usage reporting mechanisms built into YV IP" [verified]. That is a
duty **not to disable** reporting that a tool ships with, not a duty to report. The
REST API this provider uses ships none — no token, no beacon, no callback — so
there is nothing to keep enabled and nothing for the provider to carry.

**Two consequences worth stating.** First, the assertion is now positive on
evidence rather than on absence of evidence: the agreement was read and does not
ask for per-display reporting. Second, it is **scoped to the REST API**. The
YouVersion *SDKs* are a different Tool with their own YVP Terms (§YVN14.1), and if
one of those embeds a reporting mechanism, clause 3 binds whoever ships it. A
consumer swapping this provider for an SDK inherits a duty this provider does
not.

That assertion is only as good as the search behind it, so it carries a caveat: if
the spike or a per-version agreement reveals a reporting obligation, the provider
switches to `ReportOnDisplay` with its own scheme constant and a reporter package,
exactly as §APB16 does. §ABS29's model already accommodates that without a
contract change — which is why the obligation is modelled generically rather than
as "FUMS".

---

## YVN16. Attribution (#1)

Version licensing terms typically require displaying the version abbreviation and
copyright. Because the copyright is **only** on the Bible resource and never on the
passage response, the catalogue cache must retain it (§YVN7 rule 4); if it does
not, `Attribution` is null on every passage and the consumer is silently
non-compliant. A null `Attribution` on a licensed edition is a defect and is logged
at Warning by the base class (§ABS32).

**The required *form* of attribution is still unknown** (§YVN14.3) — it is not in
the platform terms and would sit in a version's own licence or in the YVP Terms.
The sibling provider's terms specify a copyright page and a hyperlinked citation
(§APB19); nothing says YouVersion's are the same.

**This provider has more attribution material than the sibling**, and it is worth
using: `copyright` for the short notice, `promotional_content` where a fuller form
is wanted, and `publisher_url` for the link (§ABS44.5). All three are on the
catalogue and none on the passage, which is why §YVN7 rule 4 retains them.

**What the terms do constrain is the opposite direction.** The YouVersion marks —
"YouVersion", "YVP", "Life.Church", "The Bible App" — may not be used unless a
Tool's YVP Terms allow it (§YVN14.2 rule 6). So attribution names **the version and
its copyright holder**, and a UI must not label the feature as YouVersion's or imply
partnership. That is a restriction on attribution, not a specification of it, and
the two should not be confused.

---

## YVN17. Per-version licence acceptance — the operational trap (#1)

The app key only sees versions whose agreements were accepted in the portal. This
is the most common cause of a confusing `TranslationNotSupported`, and it looks
identical to a translation that does not exist.

Three things follow:

1. The package README must say so, and support guidance should start with "check
   the portal".
2. `all_available=true` is the diagnostic that separates "exists on the platform
   but this key is not licensed" from "does not exist" (§YVN7 rule 6) — use it to
   *answer the support question*, not as the normal listing.
3. The Error log on a missing `DefaultTranslation` (§YVN4) exists precisely because
   this trap is otherwise discovered by a user rather than by an operator.

---

## YVN18. Consumer notes (#1)

Nothing in this provider requires the consumer to do anything at display time —
there is no token to carry and no report to send. What a consumer does inherit:

1. **Attribution** must be displayed (§YVN16).
2. **Storage is still not sanctioned** (§YVN14), though the reason has changed:
   the platform terms grant no rights in the Bible text at all, so the retention
   question lives in the per-version licence and the per-Tool YVP Terms (§YVN14.1).
   Display-time only until one of those is read.
3. **Scripture must be reproduced word-for-word and unaltered** (§YVN14.2 rule 1).
   Anything a consumer does between `Text` and the screen — normalising quotes,
   collapsing whitespace, truncating with an ellipsis — is its own risk to assess.
4. **Commercial use is permitted with a disclosure** (§YVN14.2 rule 4), which is
   not true of API.Bible's licensed editions (§APB20). Do not assume one upstream's
   commercial position applies to the other.
5. **`TranslationNotSupported` is ambiguous here** — unlicensed, or outside the
   configured language ranges. Surface the configured `LanguageRanges` in
   diagnostics so the ambiguity is resolvable.

---

## YVN19. Spikes — before implementation (#1)

A live app key with at least one accepted version is required. This provider has
more unknowns than its sibling, and they are load-bearing. Items 1–3 **change what
gets built**, not merely how it is configured.

1. **Does `/bibles/{id}/passages/{usfm}` still exist?** Documented on the
   api-usage page, absent from the quick reference [contested — §YVN1].
   **Existential, not cosmetic:** it is the only endpoint in this upstream known to
   return scripture text, so if it is gone §YVN8 is not rewritten — the provider is
   (§YVN8). Run this spike first.
2. **Does a fresh app key see KJV (id 1) without accepting an agreement?** Decides
   whether the shipped `DefaultTranslation` works out of the box (§YVN4).
3. **Does the passages endpoint accept a verse range** (`JHN.3.16-JHN.3.18`)?
   Decides whether a range is one request or a chapter fetch plus a slice (§YVN9
   rule 4). **Half of this is already closed**: a bare *chapter* id is documented as
   accepted (§YVN9 rule 1) [verified]; no *range* syntax is documented anywhere, so
   only the range half needs a live key.
4. **Confirm the red-letter and poetry class vocabulary** against a licensed
   red-letter version. §YVN10 rule 4's mapping is guesswork until this is done.
5. **Settle the catalogue parameters** — `language_ranges[]` with literal brackets
   versus comma-separated `language_ranges`; `page_token` versus `next_page_token`;
   `page_size` bounds; and the first-range-wins merge behaviour. Both contradictions
   are §SOL16 items. **Build §YVN7's tolerant fallbacks regardless** — they are
   cheap and they survive the upstream changing its mind.
6. **Establish rate-limit and quota semantics** (§YVN13) — published limits, and
   whether an `X-RateLimit-*` header set exists (reported informally, not
   documented). 429 + `Retry-After` is already confirmed.
7. **Is a passage fetchable for a Bible absent from the listing?** Reported to be
   [unverified]. Decides whether §YVN7 rule 7's `TranslationMap`-is-authoritative
   rule is a workaround or the correct model.
8. **What do critical-text omitted verses return — 204, 200-with-empty, or 404?**
   (§YVN11.)
9. ~~**Read and record the platform terms.**~~ **Done** — §YVN14. It did not
   unblock storage, because the platform terms explicitly grant no rights in the
   Bible text. **The spike was aimed at the wrong document**, and its replacement is:
   **locate and read the YVP Terms for the Bible tool, and record what a per-version
   licence agreement says about retention** (§YVN14.1). That is what storage is
   actually blocked on. It needs portal access, not a browser.
10. **Does the Bible resource expose a script direction** (or a script code we can
    map from)? §ABS42.6 needs it and §YVN7 rule 4 falls back to a built-in table
    without it. Low cost to check, and it decides whether a Hebrew or Arabic edition
    renders correctly by default.
11. **Capture fixtures** for the acceptance suite: a two-page catalogue, a passage
    response in both `html` and `format=text`, a red-letter passage, a chapter-verses
    response, and a passage with the known stray whitespace.

---

## YVN20. Testing (#1)

Three projects, per §ABS35's conventions. All three exist.

**`…YouVersion.Tests.Unit`** — no HTTP. Catalogue mapping as a pure function:
accumulation across pages, merging of multiple `LanguageRanges`, first-range-wins
behaviour, `TranslationMap` precedence **including the §YVN7 rule 7 case where a
mapped translation is absent from the catalogue**, and copyright retention per
version. AngleSharp HTML → `Blocks`: recognised classes map, unknown degrade to
`Paragraph`/`None` with text intact, stray whitespace normalised away. Status and
exception mapping tables including 204 → `NotFound` and 406 →
`YouVersionServiceException`. Constructor validation including **empty
`LanguageRanges`**. `Name` equals `ProviderName` and the literal is unchanged.

**`…YouVersion.Tests.Acceptance`** — `WireMockServer.Start()` per test class,
`BaseUrl` repointed at it, the real provider through its real constructor, driven
only through `IBibleProvider` (§ABS35 rule 3).

1. **`X-YVP-App-Key` present on every outbound request, and the key value absent
   from every log the test captures** (§SOL14 rule 4).
2. Catalogue: a two-page response is exhausted and merged; the language filter is
   sent as `language_ranges[]` with literal brackets; **a 422 naming the field
   triggers exactly one retry with the bare spelling and a Warning** (§YVN7 rule 2);
   **a second page identical to the first triggers exactly one retry with
   `next_page_token` and a Warning** (§YVN7 rule 5); **a 503 on the second page
   discards the partial build, leaves any previous catalogue in place, and does not
   publish a partial map** (§YVN7 rule 9).
3. `GetScriptureByReferenceAsync` parses locally then takes the USFM path — assert
   no server-side reference query is ever attempted.
4. Routing by shape, asserted on the **URL** and on **request count**: a chapter
   key (`PSA.119.KJV`) reaches `/passages/PSA.119` in **one** request, and the
   chapter-verses endpoint is **not** called — it returns no text (§YVN9 rule 1).
   Whichever range strategy the spike settles is asserted the same way.

   *This criterion previously required the opposite — that a chapter key reach the
   chapter-verses endpoint — while citing §YVN9 as its authority. A developer
   working criterion-first would have built the defect §YVN9 exists to prevent and
   had a green test proving it.*
5. Content: `<p></p>` → `NotFound`; footnote-only → `NotFound`; **204 →
   `NotFound`**; a range with one empty verse → `Found` with that id in
   `MissingVerseIds`; **HTML that parses to empty but had a non-empty body triggers
   exactly one `format=text` re-request before `NotFound`** (§YVN10 rule 3).
6. Failure mapping: 404 → `NotFound`; 403 → `TranslationNotSupported`; 401 →
   `YouVersionAuthorizationException`; 429 with a short `Retry-After` →
   `YouVersionRateLimitException` carrying it; 429 without one →
   `YouVersionQuotaExceededException`; 5xx after the retry budget →
   `YouVersionUnavailableException`; 422 on the catalogue after the retry →
   `YouVersionUnavailableException` logged at Error; 406 →
   `YouVersionServiceException`. Every one asserted to carry its abstraction marker.
7. Every `Found` passage carries `Usage.Obligation == NotRequired` and a non-null
   `Attribution` sourced from the cached catalogue copyright.
8. A cancelled token aborts in flight and surfaces `OperationCanceledException`; a
   **provider-side timeout** with the caller's token unsignalled surfaces
   `YouVersionUnavailableException` (§ABS13 rule 3).

**`…YouVersion.Tests.Integrations`** — the live API. Credentials from
`YOUVERSION_APP_KEY` only; every fact guarded so the suite is **skipped, not
failed**, without a key. No secret is committed. Not run by CI (§SOL7 rule 2).

Smoke coverage: fetch `JHN.3.16` in a licensed version and assert `Found` with
non-empty `Text` and `Attribution`; fetch the live catalogue and assert
`DefaultTranslation` resolves; request an unlicensed version and assert
`TranslationNotSupported`.

**This project is also the permanent regression guard for this provider's
behavioural unknowns**, and given §YVN1 it carries more weight here than anywhere
else in the solution: whether `/passages` still exists, whether it accepts ranges,
which catalogue parameter spelling the server honours, the red-letter class
vocabulary, and — per §YVN10 rule 3 — **that the HTML and `format=text` renditions
of the same reference still agree**. A silent upstream change should be caught by a
failing test, not discovered by a user.

Plus the **Conformance** suite (§ABS38), which holds this provider to the
contract — including that `NotRequired` still serializes to a non-empty value.

---

## YVN21. Work breakdown (#1)

Every item depends on the abstraction items 1–7 (§ABS40). **Item 1 gates more here
than it does for API.Bible** — several behaviours are unverified, and four of them
change what gets built.

| # | Item | Contents | Est. |
|---|---|---|---|
| 1 | **Spikes** | The ten items in §YVN19, including reading the platform terms. Endpoint existence and range support decide item 4's shape; the terms decide whether consumers may store at all | 1–1.5 d |
| 2 | **Transport & container** | Internal `ServiceCollection`, typed client with `X-YVP-App-Key`, resilience pipeline and budget validation, disposal | 0.5 d |
| 3 | **Catalogue** | Per-range merge, pagination with the §YVN7 rule 5 detection, the rule 2 parameter fallback, copyright retention, **atomic refresh**, and the §YVN7.1 projection | 1–1.75 d |
| 4 | **Lookup flow** | Shape-based routing, all content through `/passages` (§YVN9), the range strategy the spike settles, AngleSharp HTML→`Blocks`, the `format=text` fallback, content check | 1.5–2 d |
| 5 | **Failure mapping** | §YVN12 and §YVN13, including 204, 406 and the 429 discriminator | 0.5 d |
| 6 | **Tests** | The three projects in §YVN20 plus the inherited Conformance suite | 1–1.5 d |

Provider total ≈ **5–7 dev-days**.
