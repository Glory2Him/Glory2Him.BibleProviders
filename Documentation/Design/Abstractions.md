# Abstractions — the provider contract

**Area prefix:** `ABS` · **Sections:** §ABS1 – §ABS45
**Package:** `Glory2Him.BibleProviders.Abstractions`
**Solution overview:** [Design.md](Design.md) · **Providers:** [ApiBible.md](ApiBible.md) · [YouVersion.md](YouVersion.md)

Conventions, heading tags and provenance tags: [Design.md](Design.md), "Conventions".

---

## ABS1. Purpose and scope (#1)

This package defines the contract every Bible scripture provider implements, the
shared parsing and rendering code they all use, and the abstraction that routes a
call to one of them by name. It contains **no HTTP and no provider-specific
knowledge**.

**The pattern**, stated once here because every other document in this solution
binds to it: a single **abstraction provider** aggregates every registered
provider and forwards each call to one of them **by name**. Providers are
constructed by the consuming application and handed to the abstraction as a
collection; the abstraction resolves the name through an internal foundation
service and forwards. It never chooses between providers, never retries, and
never inspects a result. Failure classification crosses package boundaries
through **marker interfaces** (§ABS7), so the abstraction can handle a provider it
has no reference to. Layering inside each package is Broker → Foundation Service →
the package's public face (§SOL2 rule 2), and exception handling follows the
Validation / Dependency / Service split.

The common case:

```csharp
ScriptureResult result = await bibleAbstractionProvider.GetScriptureByReferenceAsync(
    ApiBibleProvider.ProviderName, "John 3:16", cancellationToken);

if (result.IsFound)
{
    string text = result.Passage.Text;              // IsFound flow-analyses Passage to non-null
    string notice = result.Passage.Attribution ?? result.Passage.Translation;
}
```

---

## ABS2. What this package does not do (#1)

| Not done here | Where it belongs |
|---|---|
| Passage caching, persistence, retention | The consuming application. §ABS31 states the contractual limits it must meet. **No package here ever caches, stores or writes scripture anywhere** (§SOL2 rule 5) — a passage lives for the call and no longer, so this library is never a party to a retention obligation |
| Usage reporting (e.g. FUMS `trackView`) | The consuming application, at render time. This library surfaces the obligation and the token (§ABS29), and never reports, because reporting needs the viewer's device and session identity and a fetch has no viewer |
| Cross-provider fallback | The consuming application's orchestration layer. §ABS34 gives the decision table and a worked loop |
| Cross-edition versification mapping | Out of scope entirely (§ABS17) |
| A domain entity | Providers return a DTO. The consumer maps it onto whatever it stores |

---

## ABS3. Project layout and dependencies (#1)

Four projects, of which three exist (§SOL5):

```
Glory2Him.BibleProviders.Abstractions
Glory2Him.BibleProviders.Abstractions.Conformance      (to create — §ABS38)
Glory2Him.BibleProviders.Abstractions.Tests.Unit
Glory2Him.BibleProviders.Abstractions.Tests.Acceptance
Glory2Him.BibleProviders.Abstractions.Tests.Integrations
```

`…Tests.Integrations` exists in the repository and this package has no upstream to
integrate with. **Leave it empty rather than inventing work for it** — it is not
run by CI (§SOL7 rule 2), and a project that exists for symmetry is cheaper than
one filled with tests that duplicate the acceptance suite.

The only dependency is `Microsoft.Extensions.Logging.Abstractions`, and nothing else — the justification and the singular/plural package-id trap are in
§SOL6. `IBibleAbstractionProvider` is constructed directly
(`new BibleAbstractionProvider(providers)`); **this package ships no
`IServiceCollection` extension method** (§ABS28).

Unit tests reach `internal` types through `InternalsVisibleTo`.

---

## ABS4. `IBibleProvider` (#1)

```csharp
public interface IBibleProvider : IDisposable
{
    /// <summary>Stable, case-insensitive key this provider is addressed by (§ABS14). Carried onto
    /// ScriptureResult.Provider and used as the {Provider} logging scope. Must return the
    /// provider's own ProviderName constant.</summary>
    string Name { get; }

    /// <summary>Lookup by translation-qualified USFM key: "JHN.3.16.NIV", "JHN.3.16-JHN.3.18.ESV",
    /// or "PSA.23" with the translation defaulted (§ABS20).</summary>
    Task<ScriptureResult> GetScriptureByUsfmAsync(string usfm, CancellationToken cancellationToken = default);

    /// <summary>Lookup by loose human reference: "John 3:16 NIV", "1 Jn 1:9 (ESV)", "Rom 8:28".</summary>
    Task<ScriptureResult> GetScriptureByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>What this provider's catalogue currently carries. Served from the cached catalogue,
    /// so it costs an upstream request only on a cold cache. A snapshot, never a guarantee — §ABS44.</summary>
    Task<IReadOnlyCollection<TranslationSummary>> GetTranslationsAsync(
        CancellationToken cancellationToken = default);
}
```

---

## ABS5. Rules every implementation is bound by (#1)

1. **A provider takes everything it needs to work through its constructor, and
   nothing else.**

   ```csharp
   public ApiBibleProvider(ApiBibleConfigurations configurations, ILogger<ApiBibleProvider> logger = null)
   ```

   - **The configuration is a plain POCO** carrying every value the provider needs
     to reach its upstream — base URL, credentials, translation-map overrides, the
     default translation, timeouts. Never `IOptions<T>`, never an injected
     `HttpClient` or `IHttpClientFactory`, never an `IServiceCollection` from the
     caller. A provider must not read connection details from ambient or static
     state, and it must not require the consuming application to register anything
     on its behalf. Construct it, and it works.
   - **The logger is optional and defaults to `null`**, substituted internally for
     `NullLogger<T>.Instance`. It exists so a provider's diagnostics reach *the
     consumer's* logging pipeline — a consumer that wires one gets provider logs in
     its own sinks, correlated with its own scopes; a consumer that does not gets a
     provider that still runs. The provider never configures logging itself, and
     never holds a static logger.
   - **There is deliberately no shared configuration type, interface or base class
     across the provider POCOs.** A common `IBibleProviderConfigurations` would look
     tidy and would drag every provider's settings onto one surface that has to
     change whenever any upstream gains a knob.

2. **Implementations must be safe for concurrent use.** Providers are singletons
   and will be called from many requests at once.

3. **`IDisposable` is load-bearing.** A provider builds an internal
   `ServiceCollection`/`ServiceProvider` for its own broker and foundation service,
   and through it an `IHttpClientFactory` handler pool. Something has to dispose
   that container, and only the provider knows it exists — so `BibleProviderBase`
   implements `Dispose()` once (§ABS12) and the abstraction disposes the providers
   it was handed (§ABS25). A provider that builds a container and is not disposable
   leaks it, along with every transient `IDisposable` its root scope captured, for
   the lifetime of the process.

4. **No pre-flight "do you support translation X" method.** Availability is
   subscription-driven and changes without a redeploy; a provider attempts the
   lookup and answers `TranslationNotSupported`.

   **`GetTranslationsAsync` is not that method, and the distinction is the whole of
   §ABS44.** It reports what the catalogue *said*, for populating a list; it does
   not answer "will this succeed". A caller that branches on its result instead of
   attempting the lookup has reintroduced exactly the pre-flight check this rule
   forbids, and will be wrong the first time a subscription changes underneath it.

5. **Two channels, and they do not overlap** — §ABS6.

6. **Provider exceptions must implement the marker interfaces** — §ABS7. This is
   what makes a provider's failures handleable by the abstraction and by a consumer
   that knows nothing about that provider.

7. **Argument validation at this boundary returns; it does not throw.**
   `GetScriptureByUsfmAsync(null)`, `""` or whitespace returns
   `ScriptureResult.InvalidReference` with a reason — consistent with §ABS6's
   premise that a provider *answers* rather than throws for anything the caller can
   cause. A provider's own `…ValidationException` is for a marked
   validation failure raised inside its own internals, never for a bad reference
   string; and the abstraction's hierarchy is for the *abstraction's* failures — an
   unknown provider name — not for the caller's input to a provider.

8. **A provider's retry/timeout budget must close, and the provider must validate
   that it does.** One per-attempt timeout, a bounded retry count, and an overall
   budget large enough to contain them, with `HttpClient.Timeout` left infinite so
   the resilience pipeline owns all timing. The constructor throws when
   `perAttemptSeconds × (retries + 1) + backoffCap > overallSeconds`. An upstream
   `Retry-After` is honoured only when it fits the remaining budget; otherwise the
   provider stops and throws `IBibleRateLimitException`/`IBibleQuotaExceededException`
   carrying the value, because sleeping out a multi-minute `Retry-After` inside a
   request is not an option. Each provider document states its own numbers
   (§APB6, §YVN6).

---

## ABS6. Two channels: answered vs unavailable (#1)

This is the central rule of the contract.

| The provider… | Channel | Shape |
|---|---|---|
| understood the request and answered it | **returns** | `ScriptureResult` with a `ScriptureLookupStatus` |
| could not answer at all | **throws** | an exception carrying a marker interface (§ABS7) |

"Answered" covers: here is the passage; that passage is not in this edition; this
translation is not available to me; that reference does not parse. **"Could not
answer" covers: I am rate limited, my quota is exhausted, my credentials are
rejected, the upstream is down, the request timed out.**

The distinction exists so a consumer can tell *"this passage isn't there"*
(asking another provider is pointless) from *"this provider is unavailable"*
(asking another provider is exactly right). Folding both into one `ProviderError`
status makes automatic fallback impossible to write correctly.

```csharp
public enum ScriptureLookupStatus
{
    Unknown = 0,                  // never returned by this library — treat as a defect
    Found = 1,
    NotFound = 2,                 // the provider has the translation; the passage is absent from it
    TranslationNotSupported = 3,  // the provider does not carry this translation
    InvalidReference = 4,         // the input could not be parsed
}
```

There is deliberately no `ProviderError` member. Availability failures are
exceptions.

**The rule has a sharp edge, and §SOL16 rule 1 is where it cuts.** Classifying an
upstream status into the wrong channel is not a cosmetic error: a `returned`
status that should have been an exception makes a provider look healthy while it
is unusable, and a consumer's fallback loop will keep choosing it forever.

---

## ABS7. Marker interfaces — the only vocabulary that crosses a boundary (#1)

These are the entire public exception vocabulary of the solution. Every exception
that may leave the package that raised it carries at least one.

```csharp
// Glory2Him.BibleProviders.Abstractions — Models/Exceptions/, PUBLIC

// Classification. Exactly one per exception, and it answers "whose fault is this?".
public interface IBibleValidationException { }   // the caller passed something invalid
public interface IBibleServiceException { }      // this component itself malfunctioned

// An upstream this component depends on failed it. The one marker carrying a member, because
// every availability failure is something an operator may have to go and look at. See §ABS7.1.
public interface IBibleDependencyException
{
    /// <summary>Where a human goes to inspect or fix this — the upstream's own usage dashboard,
    /// key management or licence portal. Null when the provider has nothing useful to point at.</summary>
    Uri? ProviderConsole { get; }
}

// Availability semantics, for the fallback decision. Each derives IBibleDependencyException, so a
// consumer catches broadly ("unavailable — try another provider") or narrowly ("rate limited, and
// here is for how long") without ever naming a concrete type.
public interface IBibleRateLimitException : IBibleDependencyException
{
    /// <summary>How long to wait before this provider is worth trying again. Null when unknown.</summary>
    TimeSpan? RetryAfter { get; }
}

public interface IBibleQuotaExceededException : IBibleDependencyException
{
    /// <summary>When the exhausted allowance resets. Null when unknown.</summary>
    DateTimeOffset? QuotaResetsOn { get; }
}

public interface IBibleAuthorizationException : IBibleDependencyException { }  // credentials rejected or revoked
public interface IBibleUnavailableException : IBibleDependencyException { }    // outage, timeout, transport failure
```

**Why markers rather than a shared base class.** A base class would force every
provider to inherit from a type in this package and would fix the inheritance
chain for every exception type in the solution. A marker composes instead: an
exception is an ordinary `System.Exception` first, and is *classified* by the
interfaces it carries. That is also what let §SOL17 rule 6 drop the `Xeption`
base class from shipped packages without touching a single line of this contract. It also lets one exception carry both a classification and
a semantic — `IBibleRateLimitException` is an `IBibleDependencyException` by
derivation, so a consumer handling only the broad case still catches it.

### ABS7.1 The exceptions are the observability, and there are deliberately no counters (#1)

This library exposes **no request counters, no metrics and no telemetry hooks**,
and that is a decision rather than an omission. Both upstreams already meter usage
and both publish a dashboard for it — a counter here would be a second, lagging,
disagreeing copy of a number the provider already owns, and a consumer chasing a
discrepancy would have to reconcile the two.

What a consumer cannot get from a dashboard is *the moment it happened, in process,
with something to do about it*. That is what the exception channel is for, and it
already carries the actionable parts: which condition (§ABS7's markers), how long
(`RetryAfter`), until when (`QuotaResetsOn`). `ProviderConsole` completes it with
**where to look**, so an alert fired from a `catch` block is self-contained and an
operator is not left mapping a provider name to a URL from memory at 2am.

Three rules follow:

1. **`ProviderConsole` is a constant per provider, not per failure** — a licence
   portal, a plan or usage page. It carries no key, no account id and no query
   string built from configuration (§SOL14 rule 4).
2. **It is a pointer, never a promise.** The library does not know the consumer's
   quota, plan or remaining allowance and must not imply it does. Nothing reads it
   programmatically; it exists to end up in a log line or an alert.
3. **Null is a legitimate answer.** `IBibleUnavailableException` for a transport
   failure has nothing useful to point at, and inventing a status-page URL would be
   worse than admitting it.

**Consumers catch markers, never concrete types.** This is the rule the whole
design rests on:

```csharp
catch (Exception exception) when (exception is IBibleQuotaExceededException quota)
{
    suspendUntil[providerName] = quota.QuotaResetsOn;   // stop asking; the allowance is gone
}
catch (Exception exception) when (exception is IBibleDependencyException)
{
    continue;                                           // any other unavailability — try the next
}
```

---

## ABS8. What each provider package must declare (#1)

**Every provider ships its own exception family**, in its own package, and tags
each type with a marker. Abstractions declares no concrete exceptions — it cannot,
because a useful exception carries provider-specific detail.

```csharp
// Glory2Him.BibleProviders.ApiBible — Models/Exceptions/, PUBLIC
public sealed class ApiBibleRateLimitException : Exception, IBibleRateLimitException
{
    public TimeSpan? RetryAfter { get; }

    public ApiBibleRateLimitException(string message, TimeSpan? retryAfter, Exception innerException)
        : base(message, innerException) => RetryAfter = retryAfter;
}
```

**The mandatory minimum.** A provider MUST declare, at least:

| Marker | Why it is mandatory |
|---|---|
| `IBibleValidationException` | Something the caller got wrong that the provider detected |
| `IBibleServiceException` | The provider malfunctioned — a bug, not an upstream problem |
| `IBibleRateLimitException` | A consumer cannot implement a correct backoff without it |
| `IBibleQuotaExceededException` | Distinguishing "wait a moment" from "wait a month" is the whole point of failing over |
| `IBibleAuthorizationException` | A revoked key must be loud, and must not be retried |
| `IBibleUnavailableException` | The catch-all upstream failure |

Plus `IBibleDependencyException` on a general upstream-failed type — and note that
it is **the one marker carrying a member**: every type implementing it, directly or
through one of the four availability markers, must supply `ProviderConsole`
(§ABS7.1). A constant per provider, or a deliberate null.

If an upstream genuinely has no quota concept, the provider **still declares the
type and documents why it is never thrown** — silence is indistinguishable from an
oversight, and a consumer reading the provider document needs to know which of its
own branches are dead. Both provider documents currently say "not yet known"
rather than "none", which is the honest answer and not the same thing.

**A provider must never let an unmarked exception escape.** An
`HttpRequestException`, a `JsonException`, a `NullReferenceException` reaching a
caller is a contract violation: the abstraction cannot classify it, so it falls to
the catch-all and the consumer learns only that *something* broke. Everything a
provider throws is caught and re-thrown as a marked type.

---

## ABS9. Categorization is private (#1)

Turning an upstream failure into a marked exception is **the private business of
the component doing it**. Nothing outside names the machinery:

```csharp
// ApiBibleProvider.Exceptions.cs, a partial of the provider. PRIVATE.
private delegate Task<ScriptureResult> ReturningScriptureResultFunction();

private async Task<ScriptureResult> TryCatch(ReturningScriptureResultFunction returningFunction)
{
    try
    {
        return await returningFunction();
    }
    catch (ApiBibleRateLimitInnerException inner)         // private nested type — see rule 2
    {
        throw new ApiBibleRateLimitException(
            message: "API.Bible is rate limiting this key; retry after the stated interval.",
            retryAfter: inner.RetryAfter,
            innerException: inner);
    }
    catch (HttpRequestException httpRequestException)
    {
        throw new ApiBibleUnavailableException(
            message: "API.Bible could not be reached; contact support if this persists.",
            innerException: httpRequestException);
    }
    // …one arm per condition, ending in a catch-all that produces ApiBibleServiceException.
}
```

Three rules govern it:

1. **The `TryCatch` method and its delegate are `private`.** They are a
   partial-class member of the component that owns them, never shared, never part
   of any surface.
2. **Intermediate exception types are `private` nested types where one class owns
   them, `internal` only where they genuinely cross files inside the same
   assembly.** They are *never* public. A caller has nothing to gain from naming the
   step between "a 429 arrived" and "this is `IBibleRateLimitException`".
3. **Exactly one exception type escapes per failure**, and it is public and
   marked. C# happily throws a non-public type from a public method, but that
   leaves a consumer able to catch it only by base type — so the escaping type is
   always public, and the consumer catches it by marker anyway.

**Categorization preserves evidence.** Every thrown exception passes the original
as `innerException`, and any structured detail the consumer might act on —
`RetryAfter`, `QuotaResetsOn` — is lifted onto the public exception as a typed
property rather than left buried in a message string. A message is for a human
reading a log; a property is for code deciding what to do next.

---

## ABS10. How the abstraction classifies (#1)

`BibleAbstractionProvider` sits between the consumer and a provider it knows
nothing about. Its own `TryCatch` (§ABS25) has four ordered arms:

| Order | Catches | Becomes |
|---|---|---|
| 1 | `OperationCanceledException`, caller's token signalled | rethrown untouched |
| 2 | `ProviderServiceValidationException` — by type | a validation exception carrying `IBibleValidationException` |
| 3 | any exception carrying `IBibleValidationException` / `IBibleDependencyException` — **by marker** | a dependency exception carrying `IBibleDependencyException`, **with the provider's exception preserved as `InnerException`** |
| 4 | everything else | a service exception carrying `IBibleServiceException` |

Arm 3 is why the marker interfaces exist. The abstraction has no reference to any
provider package and cannot name `ApiBibleRateLimitException` — it matches the
marker, wraps, and preserves. The consumer then reads the specific marker and its
properties off `InnerException`:

```csharp
catch (Exception exception) when (exception is IBibleDependencyException)
{
    var rateLimited = exception.InnerException as IBibleRateLimitException;
    Suspend(providerName, rateLimited?.RetryAfter ?? DefaultBackoff);
}
```

**A dependency's validation failure is still a dependency failure here.** A
provider reporting "you gave me something invalid" is, from the abstraction's
position, its dependency failing — never re-badged as the abstraction's own
validation error, which is reserved for arguments the abstraction itself
validated (a blank or unknown provider name).

---

## ABS11. Accessibility (#1)

The rule across every package in this solution:

| Accessibility | What |
|---|---|
| **`public`** | Only what a consumer compiles against: `IBibleProvider`, `IBibleAbstractionProvider`, `BibleProviderBase`, the DTOs (`ScriptureResult`, `ScripturePassage`, `ScriptureBlock`, `ScriptureSegment`, `ScriptureUsage`), the marker interfaces, each provider's localised exception family, the reference surface — `UsfmReference`, `BibleReference`, `ReferenceSuggestion` (§ABS41) — and `ScriptureMarkup` (§ABS43) |
| **`internal`** | Everything else that does real work: HTTP brokers, foundation services, catalogue holders, content mappers, `ProviderService` |
| **`private`** | The categorization machinery — `TryCatch` methods, their delegates, and any intermediate exception types a single class owns |

**One exception family spans the whole package, rather than one per layer.** The
Standard puts a categorization boundary between a foundation service and the
component above it, so each would carry its own family. These packages do not: a
provider's six or seven public types (§ABS8) serve the foundation service and the
public face alike, and the intermediate types between them are `private` (§ABS9
rule 2).

That is deliberate, and the reason is §ABS7: **the marker interfaces are the entire
public exception vocabulary.** A consumer classifies by marker and never by
concrete type, so a second family would add types nobody names, published forever
(§SOL7 rule 4), to express a boundary that is invisible from outside the assembly.
The layering itself is unaffected — the broker still cannot be reached from the
façade (§SOL8) — only the exception taxonomy is flattened. Recorded here in the
§SOL17 rule 6 style, because an unrecorded divergence from a vendored skill is
indistinguishable from not having read it.

The test is simple: **if a consumer cannot name it, it is not public.** A consumer
names the contract, the DTOs, the markers, the reference surface, and (rarely,
when holding a provider directly) that provider's exception types. It never names
a broker, a service, or a categorization step.

**`UsfmReference` was public by compiler necessity before it was public by
decision, and the two disagreed for a while.** `BibleProviderBase` is a public
abstract class whose `protected abstract FetchAsync(UsfmReference, …)` (§ABS12)
takes it as a parameter, and C# requires a protected member's parameter types to
be at least as accessible as the member — so the type could never have been
`internal`, whatever this table said. §ABS41 settles it deliberately instead of
incidentally, and adds `BibleReference` alongside it so a consumer can normalize a
reference without owning a provider.

---

## ABS12. `BibleProviderBase` (#1)

The shipped implementation of the contract and the base class for every provider
in this solution. A third-party provider that declines it is still bound by
§ABS5's rules and must satisfy them itself.

```csharp
public abstract class BibleProviderBase : IBibleProvider
{
    protected BibleProviderBase(string name, string defaultTranslation, ILogger? logger = null);

    public string Name { get; }
    protected string DefaultTranslation { get; }
    protected ILogger Logger { get; }

    /// <summary>The provider's whole job: resolve the translation against its own catalogue, call its
    /// upstream, and return a finished ScriptureResult. Returns for scripture outcomes; THROWS a marked
    /// marked exception for availability failures (§ABS6, §ABS7).</summary>
    protected abstract Task<ScriptureResult> FetchAsync(
        UsfmReference reference, CancellationToken cancellationToken);

    /// <summary>Optional second hook, called only when the loose parser failed — see §ABS21.</summary>
    protected virtual Task<ScriptureResult> FetchByRawReferenceAsync(
        string reference, CancellationToken cancellationToken);

    /// <summary>The one renderer behind ScripturePassage.Reference (§ABS16). Providers must use it
    /// rather than echoing the upstream's own reference string. The language is the RESOLVED
    /// EDITION'S, known by the time the passage is built — never ambient culture (§ABS42.5).</summary>
    protected string RenderReference(UsfmReference reference, string languageCode);

    /// <summary>Assigned by the subclass after it builds its internal container. Disposed for it.</summary>
    protected IServiceProvider? InternalServices { get; set; }

    public void Dispose() { (InternalServices as IDisposable)?.Dispose(); DisposeCore(); }
    protected virtual void DisposeCore() { }
}
```

**On `= default` for the `protected` members.** `FetchAsync` and
`FetchByRawReferenceAsync` take `CancellationToken cancellationToken` with **no
default**, while the public methods in §ABS4 and §ABS24 default it. That is
deliberate, not an oversight, and it is a knowing divergence from
`the-standard-cancellation-patterns` 1.0 rule 2 for one specific reason: C#
resolves optional arguments **at the call site, by static type**, so a default on
a `virtual` or `abstract` member is silently ignored when the call comes through a
derived reference — the classic footgun. These members have exactly one caller,
`BibleProviderBase` itself, which always holds a token. Requiring it makes
forgetting to pass it a compile error instead of a silent `CancellationToken.None`.

The rule generalises: **default the token on surfaces a consumer calls; require it
on surfaces only this library calls.** A third-party provider declining this base
class (below) is still bound by §ABS4's defaulted public signature, which is the
one its consumers see.

**Division of labour.** The base parses the input (applying `DefaultTranslation`
when it was omitted — §ABS20), then calls `FetchAsync`. `FetchAsync` returns a
*finished* `ScriptureResult`: it resolves the translation against the provider's
catalogue, answers `TranslationNotSupported` on a miss, and maps upstream content
onto `ScripturePassage`. The base does not re-map content. The base owns: the
parse and translation fill-in; the `{Provider}`/`{Usfm}` logging scope; the
compliance warnings in §ABS32; and the exception discipline in §ABS13.

**`Reference` rendering is shared, and the split is worth stating exactly.** The
base owns *the renderer* — `RenderReference` is the one way a `Reference` is ever
produced (§ABS16 rule 2) — but it cannot call it, because rendering needs the
resolved edition's language and only the provider has consulted the catalogue
(§ABS42.2). So the provider calls it, passing the language it resolved. The base
owning the renderer is what keeps the output identical across providers; the
provider supplying the language is what makes it correct for the edition.

---

## ABS13. Exception discipline in the base (#1)

Arms in this order:

```csharp
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
catch (Exception exception) when (exception is IBibleValidationException
                             || exception is IBibleDependencyException
                             || exception is IBibleServiceException) { throw; }
catch (Exception exception) { throw CreateProviderServiceException(exception); }
```

1. A marked provider exception passes through unchanged — that is how the
   abstraction (§ABS10) and the consumer classify it.
2. Anything unmarked is a provider bug and becomes that provider's
   `…ServiceException`, carrying `IBibleServiceException`.
3. **One distinction is specific to this contract and must not be lost:** a
   provider's own timeout is *not* cancellation. An `OperationCanceledException`
   raised while the caller's token is unsignalled means the upstream did not answer
   in time — it becomes the provider's `IBibleUnavailableException`, so a consumer
   fails over. Only a genuinely caller-cancelled token propagates untouched. This
   is the subtlest rule in the contract and is invisible without a test
   (§ABS38 rule 7).

The general handling of cancellation tokens and timeouts otherwise follows
`the-standard-cancellation-patterns`, which this document does not restate.

---

## ABS14. Provider names are constants (#1)

`Name` is the only thing that routes a call, and both parameters of every
abstraction method are `string` — so `GetScriptureByUsfmAsync(name, usfm)` and
`GetScriptureByUsfmAsync(usfm, name)` differ only by a runtime exception.

1. **Every provider exposes its name as a public constant and passes it to the
   base constructor** — `Name` is a non-virtual base property fed by that argument,
   so a provider does not (and cannot) `override` it:

   ```csharp
   public sealed class ApiBibleProvider : BibleProviderBase
   {
       public const string ProviderName = "ApiBible";

       public ApiBibleProvider(ApiBibleConfigurations configurations, ILogger<ApiBibleProvider> logger = null)
           : base(ProviderName, configurations.DefaultTranslation, logger) { … }
   }
   ```

   The literal appears once, in the package that owns it. Callers write
   `ApiBibleProvider.ProviderName`.
2. **Convention:** a short PascalCase token matching the distinguishing segment of
   the package name. Compared case-insensitively, written in canonical case.
3. **Not `GetType().FullName`.** The name is typed by hand at every call site and
   persisted by consumers, so it must be a stable public identifier deliberately
   decoupled from the CLR type — a namespace refactor must not silently split
   already-stored values. This matters more here than usual: the assembly prefix is
   `Glory2Him.BibleProviders` while the provider name is `ApiBible`, and the two are
   not derived from each other on purpose.

---

## ABS15. `ScriptureResult` (#1)

```csharp
public sealed class ScriptureResult
{
    public required ScriptureLookupStatus Status { get; init; }
    public required string Provider { get; init; }      // always populated, success or failure
    public ScripturePassage? Passage { get; init; }
    public string? Message { get; init; }               // detail for TranslationNotSupported / InvalidReference

    [MemberNotNullWhen(true, nameof(Passage))]
    public bool IsFound => Status == ScriptureLookupStatus.Found && Passage is not null;

    public static ScriptureResult Found(string provider, ScripturePassage passage) => …;
    public static ScriptureResult NotFound(string provider) => …;
    public static ScriptureResult TranslationNotSupported(string provider, string translation) => …;
    public static ScriptureResult InvalidReference(string provider, string message) => …;
}
```

1. **`Unknown = 0` so `Found` is not the default.** A result that never had
   `Status` set — hand-built, faked in a test, deserialized from a payload missing
   the field — must not read as success with a null `Passage`.
2. **`required` on `Status` and `Provider`** makes the factories an enforcement
   rather than a convention, and makes `System.Text.Json` throw on a payload
   omitting either.
3. **`IsFound` with `[MemberNotNullWhen]`** encodes "non-null iff Found" for the
   compiler, so consumers branch on `IsFound` and never write `!`.

---

## ABS16. `ScripturePassage` (#1)

```csharp
public sealed class ScripturePassage
{
    public required string Usfm { get; init; }         // edition-relative, translation-suffixed; the range returned
    public required string Reference { get; init; }    // canonical English display form, rendered by us
    public required string Translation { get; init; }  // "NIV"
    public required string Text { get; init; }         // plain text; never empty on a Found result

    /// <summary>The edition's language, ISO 639-3. Drives Reference rendering and display. §ABS42</summary>
    public required string Language { get; init; }                    // "eng", "spa", "heb"

    /// <summary>Never guessed to LeftToRight when unknown — a wrong guess renders RTL scripture
    /// backwards with no error. §ABS42.6</summary>
    public required ScriptDirection ScriptDirection { get; init; }

    /// <summary>Whether displaying Text must be reported to the rights holder, and what is needed to
    /// report it. Required — a provider states a position, including NotRequired. See §ABS29.</summary>
    public required ScriptureUsage Usage { get; init; }

    /// <summary>Copyright/attribution text. Required and nullable: you must decide. Null is a
    /// compliance event, not a normal case — see §ABS32.</summary>
    public required string? Attribution { get; init; }

    public string? Html { get; init; }                             // normalized rich HTML (§ABS22)

    /// <summary>Whether Html was generated by this library or is markup we did not produce.
    /// Required — a provider states a position, including None. §ABS43</summary>
    public required ScriptureMarkup Markup { get; init; }
    public IReadOnlyList<ScriptureBlock> Blocks { get; init; } = Array.Empty<ScriptureBlock>();
    public string? ProviderReference { get; init; }                // the upstream's own reference string, verbatim
    public int VerseCount { get; init; }                           // verses carrying text
    public bool IsTruncated { get; init; }                         // upstream capped the range
    public string? RequestedUsfm { get; init; }                    // the caller's key when it differs from Usfm
    public IReadOnlyList<string> MissingVerseIds { get; init; } = Array.Empty<string>();

    /// <summary>Footnotes and cross-references. Reserved and always empty today — providers
    /// request notes suppressed. Populating it later is additive, not breaking. §ABS22</summary>
    public IReadOnlyList<ScriptureNote> Notes { get; init; } = Array.Empty<ScriptureNote>();

    /// <summary>Diagnostics only — rawJson/rawHtml. Nothing a consumer is obliged to act on may live
    /// here; that is how usage tokens get lost.</summary>
    public IReadOnlyDictionary<string, string> ProviderMetadata { get; init; }
        = ImmutableDictionary<string, string>.Empty;
}
```

1. **`Usfm` is the upstream's returned id, re-suffixed with the resolved
   translation.** Both upstreams are addressed with an unsuffixed key (`JHN.3.16`)
   and return an unsuffixed id; the provider re-attaches `.NIV` before building the
   passage. It is never stored unsuffixed — §ABS17 explains why the suffix is
   load-bearing.
2. **`Reference` is rendered by us, never copied from the provider.** Both
   upstreams return a `reference` string and neither promises a stable form — one
   follows the Bible's own language, and YouVersion's is **localized to the
   version's language** [verified]. So `Reference` always comes from
   `RenderReference`, and the upstream's string is kept verbatim in
   `ProviderReference`. `Usfm`, `Reference`, `Translation` and `Text` are therefore
   identical for a given lookup whoever answered.

   **It is rendered in the resolved edition's own language** — `Juan 3:16` for a
   Spanish edition, `Johannes 3,16` for a German one, separator included. The
   invariant above survives that because rendering is a pure function of
   (`UsfmReference`, language) and the language comes from the edition, never from
   the provider or from ambient culture: §ABS42.5.
3. **The short-count channel.** A `Found` result may carry less scripture than was
   asked for, but never silently: `Usfm` is the range actually returned,
   `RequestedUsfm` what was asked for when they differ, `MissingVerseIds` the verses
   the response acknowledged but returned no text for.
4. **Two invariants every provider upholds:** `Text` is never null, empty or
   whitespace on a `Found` result, and `VerseCount` counts only verses carrying
   text.
5. **`ProviderMetadata` is for diagnostics and nothing else.** If a consumer is
   obliged to act on a value, it gets a typed member. A stringly-typed bag is how
   obligations get dropped without a compile error.

---

## ABS17. `Usfm` is edition-relative (#1)

Verse numbering is a property of the edition, not of scripture: `MAL.4.1` in
KJV/NIV is `MAL.3.19` in Hebrew-versified editions, `JOL.2.28` is `JOL.3.1`, and a
numbered psalm superscription shifts everything after it. So the `.TRANSLATION`
suffix is **load-bearing, not decoration** — `PSA.3.1` does not identify a verse;
`PSA.3.1.NIV` does.

1. Providers are addressed in the target edition's own numbering.
2. **A stored USFM key must never be re-resolved against a different translation**
   to "get the same verse in another version". That is a versification mapping, and
   this library does not do it.
3. `Usfm` is the id of the verse actually served, not the string the caller passed
   in.

Edition-native numbering is the only scheme both upstreams can honour: API.Bible
exposes both its own `id` and an organizational `orgId` and lets the request pick
(§APB13), while YouVersion exposes no equivalent at all. Pinning to
edition-native is therefore not a preference — it is the intersection. It is also
**irreversible** (§SOL2 rule 7): it decides what every persisted key means.

---

## ABS18. `UsfmReference` (#1)

The canonical key parser:

```
BOOK.CHAPTER[.VERSE][-BOOK.CHAPTER[.VERSE]][.TRANSLATION]
JHN.3.16.NIV              single verse
JHN.3.16-JHN.3.18.NIV     verse range
PSA.23.KJV                whole chapter
PSA.23-PSA.24.KJV         chapter range
JUD.1.5.ESV               single-chapter book — the chapter segment is mandatory
```

1. Validates the book code against a built-in canonical table (66 protestant +
   deuterocanon). The table validates the **book code only** — never to decide which
   segment is the translation.
2. **Tokenizing is positional, by arity and numeric-ness alone.** Split on `-` (at
   most one), then on `.`. Segment 0 is the book; following segments are consumed as
   `CHAPTER` then `VERSE` while numeric; a single trailing non-numeric on the
   **last** part is the translation. Legal per-part arities: `BOOK.CHAPTER`,
   `BOOK.CHAPTER.VERSE`, `BOOK.CHAPTER.TRANSLATION`, `BOOK.CHAPTER.VERSE.TRANSLATION`.
3. **Never test the trailing segment for book-table membership.** Real
   abbreviations collide with real book codes: `TOB` is Tobit *and* the Traduction
   Œcuménique de la Bible; `JUB` is Jubilees *and* the Jubilee Bible 2000. A
   membership test reads `PSA.23.TOB` as "no translation", substitutes the default,
   and silently serves the wrong translation with the wrong attribution.
4. Ranges must be the same granularity at both ends; only the end part may carry
   the translation; cross-book ranges are legal.
5. **Single-chapter books** require the explicit chapter segment: `JUD.1.5`, never
   `JUD.5`. The set is `OBA`, `PHM`, `2JN`, `3JN`, `JUD`, plus the single-chapter
   deuterocanonicals `LJE`, `S3Y`, `SUS`, `BEL`, `MAN`, `PS2`, `LAO`. It is not
   decoration — it drives §ABS19's third production, so an incomplete set silently
   misreads "Jude 5" as a chapter.
6. Exposes `Book`, `Chapter`, `Verse?`, `EndBook`/`EndChapter`/`EndVerse?`,
   `Translation`, `ToProviderKey()` (no translation suffix) and `ToDisplayString()`.

---

## ABS19. `LooseReferenceParser` (#1)

Human references, three productions:

```
<book> <chapter>[:<verse>[-[<chapter>:]<verse>]] [translation]   "John 3:16 NIV", "1 Cor 13:4-7 (ESV)", "Psalm 23"
<book> <chapter>-<chapter>                       [translation]   "Matthew 5-7", "Ps 1-2"
<book> <verse>[-<verse>]                         [translation]   single-chapter books: "Jude 5" → JUD.1.5
```

1. **Book-name tables are per language**, keyed by ISO 639-3 code, each carrying
   full names, common abbreviations (`Jn`, `Jhn`, `1 Cor`, `Ps`, `Psa`, `Song`, …)
   and that language's chapter/verse separator convention, all matched
   **case-insensitively**. English ships; others are additive data. The parse is
   scoped to a declared, ordered language list and **never searched across every
   loaded table** — §ABS42 owns this, and §ABS42.4 explains why the bound is a
   safety rule rather than a performance one.
2. A `-` with no `:` is a chapter range.
3. For single-chapter books a lone number is a **verse**, not a chapter — without
   that rule "Jude 5" returns `NotFound`, a wrong answer rather than a parse error.
4. Comma/semicolon multi-part references are out of scope: one call, one passage.
5. Failures return the offending token, not a bare `false`. That is a
   **diagnostic, not a correction** — it says which token could not be read, never
   what it probably meant. Correction is opt-in, separate, and never applied
   automatically: §ABS41.
6. This parser is reachable publicly through `BibleReference` (§ABS41), so a
   consumer can normalize `Mrk 3:1` to `Mark 3:1` without a network call.

---

## ABS20. The default-translation fill-in rule (#1)

**No untranslated reference reaches an upstream API.** Unqualified input is legal
and common. Filling the gap is part of the `IBibleProvider` contract, not a
courtesy of one base class. Abstractions cannot see provider configuration
(§ABS5 rule 1), so the default is passed as a plain `string`:

```csharp
public static bool TryParse(string input, string? defaultTranslation, out UsfmReference reference, out string? failureReason);
protected BibleProviderBase(string name, string defaultTranslation, ILogger? logger = null);
```

**Consequence to state plainly:** each provider applies *its own* default, so the
same unqualified input can resolve to different translations from different
providers — and a consumer's fallback loop calls two providers with the same
string. `ScripturePassage.Translation` and `Usfm` are the authoritative record of
what was fetched, never the input. A consumer that fails over should qualify the
reference at the call site, or keep the providers' defaults identical (§ABS34).

---

## ABS21. Parse failure is not always the end (#1)

A failed parse would otherwise end in `InvalidReference` before a provider is
consulted, which makes server-side reference resolution unreachable where an
upstream offers it. `FetchByRawReferenceAsync` is called only from
`GetScriptureByReferenceAsync`, only when the loose parser failed, and only for
providers that override it; the default returns `InvalidReference`.

A provider overriding it must **re-derive the canonical key from the response, not
the input**: parse the returned id with `UsfmReference`, suffix it with the
resolved translation, and render `Reference` from that. If the returned id will
not parse, return `InvalidReference` — a `Found` result whose `Reference` cannot
round-trip is worse than a miss.

Only API.Bible overrides it (§APB12). YouVersion has no server-side reference
parsing, so an unparseable reference stays `InvalidReference` there (§YVN8).

**State the consequence plainly, because it surprises people:** the same typo
behaves differently per provider. `"Marrk 3:1"` returns `InvalidReference` from
YouVersion, and on API.Bible reaches `/search`, which runs with `fuzziness=AUTO`
[verified] — so it spends a metered request and may return a confident-looking hit
for something else, contained only by this section's re-derive-from-the-response
rule. This is the sibling of §ABS20's warning about per-provider defaults: **the
input is never the record of what was fetched**, `ScripturePassage.Usfm` and
`Translation` are.

A consumer that wants typo handling to be consistent across providers should do it
**above** the library, with `BibleReference.Suggest` (§ABS41), before dispatching —
not by relying on one upstream's search engine.

**The gap widens for a language with no book-name table.** `"Juan 3:16"` under a
parse scope of `["eng"]` fails locally, so API.Bible falls through to `/search`
and may well resolve it server-side while YouVersion returns `InvalidReference`
(§YVN8 rule 4). Configuring the parse scope to match the editions actually served
(§ABS42.4) is what closes it; relying on one upstream's search to cover a missing
table is not a multilingual strategy.

---

## ABS22. Content model (#1)

Scripture is a sequence of blocks, each holding inline runs, because that is the
shape both upstreams return — `q1` and `q2` are different indent levels of a
*block*, and one `Poetry` flag cannot tell them apart.

```csharp
public sealed record ScriptureBlock(
    ScriptureBlockKind Kind,
    int Indent,                                    // 1–4 for Poetry/ListItem; 0 otherwise
    IReadOnlyList<ScriptureSegment> Segments);

public enum ScriptureBlockKind { Paragraph, Poetry, SectionHeading, ListItem, Other }

public sealed record ScriptureSegment(
    string Text,
    ScriptureStyle Style,     // inline only, and genuinely combinable
    string? Verse);           // "16", "3-4", "1a"; null in headings

/// <summary>Reserved (§ABS39 rule 3). No provider populates this yet.</summary>
public sealed record ScriptureNote(
    ScriptureNoteKind Kind,
    string? Caller,        // the marker in the text: "a", "1", "*"
    string? Verse,         // the verse it hangs off; null when it belongs to the block
    string Text);          // the note's own text, already plain

public enum ScriptureNoteKind { Unknown = 0, Footnote = 1, CrossReference = 2, Other = 3 }

[Flags]
public enum ScriptureStyle
{
    None         = 0,
    WordsOfJesus = 1 << 0,  // USX char style "wj" → red letter
    DeityName    = 1 << 1,  // "nd"
    Italic       = 1 << 2,  // "it" / "add"
}
```

1. Block structure lives at the block level. A paragraph boundary is the boundary
   between blocks, not a flag on a run.
2. Inline flags must combine: real payloads nest `char` inside `char` (a `wj` run
   containing `add` runs flattens to `WordsOfJesus | Italic`).
3. **`Verse` is a string, not `int?`** — `"3-4"` (merged) and `"1a"` are legal and
   real editions use them.
4. Unknown upstream styles degrade to `Paragraph`/`None`: text stays correct,
   style is lost. That graceful degradation is what lets §YVN10 ship against an
   unverified class vocabulary.

---

## ABS23. `ScriptureHtmlRenderer` (#1)

**Why `Html` is regenerated rather than passed through.** Each upstream emits its
own markup, and that markup drifts. Rendering from `Blocks` into a small, stable
class vocabulary means a consumer writes a handful of CSS rules once —
`.scripture .wj { color: #c00; }`, `.scripture .q1 { margin-left: 1em; }` — and is
insulated from an upstream changing its span structure. The raw upstream payload
is kept in `ProviderMetadata` for debugging, never for rendering.

1. One element per block (`<p class="q1">`, class = `"q" + Indent`) wrapping one
   `<span>` per styled run (`<span class="wj it">` for combined flags).
2. **Adjacent runs with identical flags are merged first**, so a speech crossing a
   verse boundary yields one span rather than one per verse. This is not cosmetic —
   API.Bible closes and reopens the `wj` node at every verse marker (§APB11), so
   without merging a five-verse red-letter speech renders as five spans.
3. `Text` is regenerated too: drop `SectionHeading` blocks entirely, drop markup,
   join blocks with `"\n"`.

   **The scripture itself passes through unaltered, character for character.**
   Dropping markup and headings is structural; touching the words is not. Do not
   normalise quotation marks, collapse internal punctuation, expand or contract
   abbreviations, or "fix" spelling — a 17th-century edition is meant to read like
   one. YouVersion's terms make this contractual for that provider, requiring text
   "reproduced word-for-word and 100% accurate to, and unaltered from, the licensed
   source text" [verified, §YVN14.2 rule 1], and no publisher licence is likely to
   be looser. Trimming whitespace an upstream left around a run is the one
   permitted liberty, because it is markup residue rather than text.
4. **Verse numbers never appear in either** — a consumer wanting numbered output
   renders its own from `Blocks` using each segment's `Verse`.
5. Scripture text is HTML-escaped on the way in. It comes from an upstream, and an
   upstream is not a trusted markup source. **That is why §SOL11 rule 2 still tells
   a consumer to sanitize on write** — not because this renderer is untrusted, but
   because a stored `Html` column will outlive this library and may be fed from
   somewhere else one day. Defence in depth, stated rather than implied.
6. **A right-to-left passage carries `dir="rtl"` on the root element**, from
   `ScripturePassage.ScriptDirection` (§ABS42.6). Without it a Hebrew or Arabic
   edition renders backwards and nothing errors.
7. **The renderer is what asserts `ScriptureMarkup.Generated`** (§ABS43). Nothing
   else in this solution may set it: a provider that produced `Html` by any other
   route says `Untrusted`, and one that produced none says `None`. Keeping the
   assertion in the same component that does the escaping is what makes it true by
   construction rather than by convention.

---

## ABS24. `IBibleAbstractionProvider` (#1)

```csharp
/// <summary>Aggregates registered providers and forwards each call to one of them by name. Owns the
/// providers passed to its constructor: disposing the abstraction disposes them.</summary>
public interface IBibleAbstractionProvider : IDisposable
{
    IReadOnlyCollection<IBibleProvider> BibleProviders { get; }

    Task<ScriptureResult> GetScriptureByUsfmAsync(
        string providerName, string usfm, CancellationToken cancellationToken = default);

    Task<ScriptureResult> GetScriptureByReferenceAsync(
        string providerName, string reference, CancellationToken cancellationToken = default);

    /// <summary>Forwards to the named provider. Never merged across providers — §ABS44.4.</summary>
    Task<IReadOnlyCollection<TranslationSummary>> GetTranslationsAsync(
        string providerName, CancellationToken cancellationToken = default);
}
```

---

## ABS25. `BibleAbstractionProvider` (#1)

```csharp
public sealed partial class BibleAbstractionProvider : IBibleAbstractionProvider
{
    private readonly IProviderService providerService;
    public IReadOnlyCollection<IBibleProvider> BibleProviders { get; }

    /// <param name="bibleProviders">Names must be unique, case-insensitively.</param>
    /// <exception cref="AlreadyExistsProviderServiceException">Two providers share a Name.</exception>
    public BibleAbstractionProvider(IEnumerable<IBibleProvider> bibleProviders)
    {
        bibleProviders ??= Array.Empty<IBibleProvider>();
        BibleProviders = bibleProviders.ToImmutableArray();
        this.providerService = new ProviderService(BibleProviders);   // also validates name uniqueness
    }

    public Task<ScriptureResult> GetScriptureByUsfmAsync(
        string providerName, string usfm, CancellationToken cancellationToken = default) =>
        TryCatch(() => providerService.GetProviderByName(providerName)
            .GetScriptureByUsfmAsync(usfm, cancellationToken), cancellationToken);

    public void Dispose()
    {
        foreach (IBibleProvider bibleProvider in BibleProviders) { bibleProvider.Dispose(); }
    }
}
```

```csharp
// BibleAbstractionProvider.Exceptions.cs (partial)
private delegate Task<T> ReturningFunction<T>();

private async Task<T> TryCatch<T>(ReturningFunction<T> returningFunction, CancellationToken cancellationToken)
{
    try
    {
        return await returningFunction();
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;                                                    // the caller went away
    }
    catch (ProviderServiceValidationException providerServiceValidationException)
    {
        throw CreateValidationException(providerServiceValidationException);
    }
    catch (Exception exception)
        when (exception is IBibleValidationException || exception is IBibleDependencyException)
    {
        throw CreateDependencyException(exception);               // a provider's failure — §ABS10
    }
    catch (Exception exception)
    {
        throw CreateServiceException(exception);
    }
}
```

`BibleAbstractionProvider` is deliberately thin: it resolves which provider
handles the call, forwards, and classifies failures. It does not retry, compare
results, or inspect `ScriptureResult`.

**`TryCatch` must be `async` and must `await` inside the `try`.** A synchronous
wrapper over a `Task`-returning call compiles silently with `T` bound to
`Task<ScriptureResult>` — and is wrong: the `try` exits the moment the provider
hands back its not-yet-completed task, so *every* exception from an `async`
provider lands on that task rather than in a `catch`, including ones thrown before
its first `await`. The whole of §ABS27 would then be unreachable, and a unit test
using a synchronously-throwing fake would still pass. Making only the lambda
`async` does not help — the exceptions land on the same task; `TryCatch` itself
must await.

---

## ABS26. Foundation `ProviderService` (#1)

An `internal` foundation service — nothing outside this assembly names it:

```csharp
internal interface IProviderService
{
    IBibleProvider GetProviderByName(string providerName);
}
```

1. Validates `providerName` non-blank and that at least one provider is
   registered.
2. The type is `internal`; the Unit test project reaches it via
   `InternalsVisibleTo`. Its **exception types are public** where they surface from
   a public constructor and reach the composition root.
3. Resolves by `Name`, case-insensitively.
4. **A miss throws rather than returning null.** `NotFoundProviderServiceException`
   names the provider that was asked for, so the failure reads as "no provider named
   X is registered" rather than surfacing later as an opaque null-reference failure
   somewhere further up.
5. **Duplicate names are rejected at construction.** A plain first-match lookup
   would let a duplicate silently shadow the second provider, which then never
   receives a call and raises no error. The constructor groups providers by `Name`
   (`OrdinalIgnoreCase`) and throws `AlreadyExistsProviderServiceException`, naming
   the offender, if any group holds more than one — so a mis-wired application fails
   at startup rather than routing half its traffic into a provider that is never
   reached.

---

## ABS27. Abstraction exception hierarchy (#1)

Two tiers, classified per §ABS7, with accessibility following §ABS11 exactly:
**what a consumer catches is a marker**, so nothing here needs a public name
except the markers themselves.

| Tier | Type | Accessibility | Marker | Raised when |
|---|---|---|---|---|
| Foundation — inner | `NullProviderServiceException` | `internal` | — | No providers registered at all |
| | `InvalidProviderServiceException` | `internal` | — | `providerName` null, empty or whitespace |
| | `NotFoundProviderServiceException` | `internal` | — | No registered provider matches `providerName` |
| | `AlreadyExistsProviderServiceException` | **`public`** | — | Two providers share a `Name`, at construction |
| Foundation — categorization | `ProviderServiceValidationException` | `private` | — | `ProviderService.TryCatch` wraps any of the four above |
| | `ProviderServiceException` | `private` | — | `ProviderService.TryCatch` wraps an unexpected internal failure |
| Abstraction — escaping | `BibleAbstractionProviderValidationException` | `internal` | `IBibleValidationException` | **Arm 2:** the caller's own argument was bad — blank or unknown provider name, or no providers registered |
| | `BibleAbstractionProviderDependencyException` | `internal` | `IBibleDependencyException` | **Arm 3:** a marked exception from the resolved provider, **preserved as `InnerException`** |
| | `BibleAbstractionProviderServiceException` | `internal` | `IBibleServiceException` | **Arm 4:** anything else — an unmarked throw out of a provider, or a foundation failure |

1. **Why the escaping types are `internal` rather than public.** They are thrown
   from public methods, which C# permits, and a consumer catches them by marker
   (§ABS7) — so a public name would buy nothing and would commit this package to
   three more types forever. The marker interfaces are the public contract; these
   are the objects that carry it.
2. **The one exception to "consumers catch markers": construction.**
   `AlreadyExistsProviderServiceException` surfaces from
   `BibleAbstractionProvider`'s constructor, wrapped as
   `BibleAbstractionProviderValidationException` carrying
   `IBibleValidationException`. A composition root that wants to handle it catches
   the marker like anything else — but it should not: a duplicate provider name is a
   wiring bug, and failing the host is the correct outcome.
3. **Categorization builds and throws — it does not pass through.** Each
   `Create…Exception` helper constructs the type above, preserving the caught
   exception as `innerException` and carrying its `Data` forward. A helper that
   returns its argument unchanged makes this whole table fiction: the types are
   declared, never thrown, and every failure arrives at the consumer as whatever the
   upstream library raised.
4. **`ScriptureResult` statuses are a different channel.** `NotFound`,
   `TranslationNotSupported` and `InvalidReference` are *answers* and are returned
   (§ABS6). Nothing in this table is reachable for them.
5. **`ProviderService` has no dependency category, deliberately.** The Standard's
   foundation-service shape gives each service validation, dependency and service
   categorizations; this one has only the first and the last. That is because it has
   **no dependency to fail** — it resolves a name against an in-process
   `ImmutableArray` handed to a constructor. There is no I/O, no broker and nothing
   that can be unavailable, so a dependency category here would be a type that is
   declared and never thrown, which §ABS8 forbids for provider quota types and
   should equally forbid here.

   The *providers'* failures are dependency failures, but they are classified one
   level up, by the abstraction's own `TryCatch` arm 3 (§ABS10) — not by this
   service, which never sees them. Recorded because a reader comparing this against
   the skill will otherwise read an omission where there is a decision.

---

## ABS28. Composition root (#1)

Wiring is explicit, in the consuming application. **No package in this solution
ships an `IServiceCollection` extension method.** A provider is constructed, not
registered-and-resolved: its configuration is bound by the application, handed to
its constructor, and the resulting instance goes into the list the abstraction is
built over. That keeps the dependency direction one-way — this solution never
reaches into the consumer's container — and it means a provider can be constructed
in a test, a console app or a background worker with no DI container at all.

```csharp
// Bind each provider's configuration section to its own POCO. This runs before Build(), so a missing
// or unbindable section stops the host here.
ApiBibleConfigurations apiBibleConfig = configuration
    .GetSection("ApiBibleConfigurations").Get<ApiBibleConfigurations>()
    ?? throw new InvalidOperationException("ApiBibleConfigurations is missing or invalid.");

// Construct INSIDE the factory: a container disposes what a factory returns, and the abstraction
// disposes the providers it was handed (§ABS25). Registering a pre-built instance looks tidier and
// silently leaks every provider's HTTP stack.
services.AddSingleton<IBibleAbstractionProvider>(sp =>
    new BibleAbstractionProvider(new List<IBibleProvider>
    {
        new ApiBibleProvider(apiBibleConfig, sp.GetRequiredService<ILogger<ApiBibleProvider>>()),
        new YouVersionProvider(youVersionConfig, sp.GetRequiredService<ILogger<YouVersionProvider>>()),
    }));

// Usage reporters, where a provider has one (§ABS30). Registered separately: the reporter package does
// not reference the provider package, which is what keeps the fetching assembly unable to reach the
// reporting endpoint.
services.AddSingleton<IScriptureUsageReporter, FumsUsageReporter>();

// ...after the host is built:
WebApplication app = builder.Build();

// Force the singleton factory to run NOW. AddSingleton(factory) is lazy, so without this the providers
// are constructed — and their configuration validation runs — on the first request that touches
// scripture: a 500 on a user request rather than a startup failure.
var abstraction = app.Services.GetRequiredService<IBibleAbstractionProvider>();

// And assert the wiring is complete: every provider that declares a usage scheme has a reporter for it.
// A wiring bug should fail at boot; only data from an old row is allowed to degrade (§ABS30).
AssertEveryUsageSchemeHasAReporter(abstraction, app.Services.GetServices<IScriptureUsageReporter>());
```

Three traps an implementer cannot derive from the types alone:

1. **`AddSingleton(factory)` is lazy.** Eager constructor validation buys nothing
   unless something resolves the singleton at startup.
2. **A container disposes only what it creates.** Construct inside the factory;
   registering an instance leaks every provider's handler pool.
3. **Duplicate provider names fail here**, not at first use — §ABS26 rule 5.

---

## ABS29. `ScriptureUsage` (#1)

Some rights holders require each **display** of scripture to be reported. That is
a per-display licence condition, and a library called at fetch time cannot
discharge it: reporting needs the viewer's device and session identity, which a
fetch does not have and must not invent. So this package models the obligation,
carries what is needed to discharge it, and never reports.

```csharp
public sealed class ScriptureUsage
{
    public required ScriptureUsageObligation Obligation { get; init; }
    public required string Provider { get; init; }
    public string? Scheme { get; init; }              // e.g. "abs.fums.v3" — the reporter routing key
    public string? Token { get; init; }               // the per-fetch token the scheme issued
    public string? ProviderEditionId { get; init; }   // the upstream edition the text came from
    public DateTimeOffset IssuedAt { get; init; }     // when the token was minted — §ABS31
    public string? UnavailableReason { get; init; }   // diagnostics; logged, not persisted

    [MemberNotNullWhen(true, nameof(Token), nameof(Scheme))]
    public bool RequiresReportingOnDisplay => Obligation == ScriptureUsageObligation.ReportOnDisplay;

    public static ScriptureUsage NotRequired(string provider) => …;
    public static ScriptureUsage ReportOnDisplay(string provider, string scheme, string token, string editionId) => …;
    public static ScriptureUsage ReportingUnavailable(string provider, string scheme, string reason) => …;

    /// <summary>Round-trips through ONE storage field. Never null and never empty — a NotRequired usage
    /// still serializes, so a blank stored value is provably a mapping bug rather than an absence.
    /// Carries no endpoint and no script URL: the reporter package owns those, and freezing a host into
    /// every stored row buys a backfill the day it moves.</summary>
    public string ToStorageString();
    public static bool TryParse(string? storedValue, out ScriptureUsage usage);
}

public enum ScriptureUsageObligation
{
    Unknown = 0,              // never produced — treat as ReportingUnavailable
    NotRequired = 1,          // the provider positively asserts nothing is owed
    ReportOnDisplay = 2,      // every display must be reported
    ReportingUnavailable = 3, // the scheme applies but no token arrived — a compliance event
}
```

It is **`required`** on `ScripturePassage`, so a provider must state a position.
It **serializes to one non-empty value**, so a consumer's storage column is
`NOT NULL` and a blank is detectable — "usually null" is the shape in which
obligations get silently dropped.

**The obligation is modelled generically rather than as "FUMS" on purpose.** Today
one upstream requires reporting and the other declares none; if YouVersion's
per-version agreements turn out to carry one (§YVN15), it switches to
`ReportOnDisplay` with its own scheme constant and a reporter package — with no
change to this contract.

---

## ABS30. The reporting contract (#1)

Abstractions defines it and holds no HTTP. Routing is by **`Scheme`**, not
provider name: a scheme survives a rename and is what gets persisted.

```csharp
public sealed class ScriptureViewerContext   // private ctor; Create/TryCreate validate both ids non-blank.
{                                            // Needed only where the caller reports server-side. No
    public string DeviceId { get; }          // "unknown viewer" sentinel exists: inventing one defeats
    public string SessionId { get; }         // the reason this library does not report at all.
    public string? UserId { get; }
}

public interface IScriptureUsageReporter
{
    IReadOnlyCollection<string> Schemes { get; }

    Task<ScriptureUsageReportResult> ReportDisplaysAsync(
        IReadOnlyCollection<ScriptureUsage> usages,
        ScriptureViewerContext viewer,
        CancellationToken cancellationToken);

    bool TryCreateBrowserPayload(
        IReadOnlyCollection<ScriptureUsage> usages,
        out ScriptureUsageBrowserPayload? payload);
}

public sealed record ScriptureUsageReportResult(
    ScriptureUsageReportStatus Status,
    int Reported,                       // how many were SENT, never how many were accepted
    string? Detail);

public enum ScriptureUsageReportStatus { Unknown = 0, Reported = 1, NotSupported = 2, Failed = 3 }

/// <summary>What a page emits to report in the browser: the script to load and the tokens to pass.</summary>
public sealed record ScriptureUsageBrowserPayload(string ScriptUrl, IReadOnlyList<string> Tokens);
```

1. `Reported` counts what was **sent**. A reporting endpoint that acknowledges
   nothing — as ABS's does (§APB16) — makes "accepted" unknowable, so no
   implementation may claim it.
2. An overload takes `IEnumerable<string?> storedValues` and parses them, because
   the real call site re-renders rows stored earlier, not passages just fetched.
3. An implementation ships **with the provider package that needs one**, in a
   separate assembly that does not reference the provider itself (§SOL2 rule 6).
4. An unknown scheme returns `NotSupported`; it never throws, because a telemetry
   gap must not fail a scripture page.

---

## ABS31. Content recency (#1)

Where an upstream requires stored content to be refreshed on a cycle, the consumer
must treat **text and usage as one atomic unit with one expiry**: never refresh the
text without replacing its `ScriptureUsage`, and never report a usage whose text
has been refreshed. `ScriptureUsage.IssuedAt` is what makes that decidable.

Each provider document states its upstream's actual figure: API.Bible's is
**30 days, plus a 24-hour response to a takedown or correction request**
[verified] (§APB17); YouVersion's is unread and storage is blocked until it is
(§YVN14).

**The 24-hour clause has a design consequence the cycle does not.** A periodic
refresh satisfies the 30-day rule on its own, but it cannot satisfy "within 24
hours of a request" — that needs a **forced-refresh path the consumer can invoke
for a specific edition on demand**. A consumer whose only refresh mechanism is a
nightly sweep is non-compliant with §APB17 no matter how often the sweep runs.

---

## ABS32. Attribution (#1)

`Attribution` is `required` and nullable — you must decide, and null means the
upstream did not supply it. A consumer must still attribute: display the
translation abbreviation and log a warning.

`BibleProviderBase` logs a Warning when a `Found` passage carries a null
`Attribution`, and when its `Usage` is `Unknown` or `ReportingUnavailable`. Those
two warnings are the only compliance behaviour in the base class, and they exist
because both conditions are otherwise silent.

**`Attribution` is a string, and at least one upstream wants more than a string.**
API.Bible's terms require a linked copyright page and a per-quotation citation
(§APB19). The link lives on `TranslationSummary.PublisherUrl` rather than on the
passage, because no upstream puts one on a passage (§ABS44.5).

**A null `Attribution` now has a remedy, which it did not when this section was
written.** `TranslationMetadata` (§ABS45) lets a deployment supply the copyright
text the upstream omitted, merged per field. The Warning still fires when both are
empty — and that is the point of it: it is how a consumer learns which translation
needs a config entry, rather than a defect with nowhere to go.

---

## ABS33. What a provider document must contain (#1)

A provider's upstream imposes conditions on the *consuming application*, not on
this library. Every `Documentation/Design/<Provider>.md` states, explicitly:

1. Its `ProviderName` and, where it has one, its usage `Scheme` constant.
2. Its configuration model, and which fields are mandatory.
3. Its concrete exception family and which marker each type carries — **including
   rate-limit and quota types** (§ABS8).
4. Its rate limits and quota semantics: what the upstream returns, and which
   condition is transient versus persistent.
5. **Every contractual obligation the consumer inherits**, in plain terms, with
   the actual figures: reporting requirements, content-refresh cycles, caching
   caps, attribution requirements, and any commercial-use restriction. Where a
   figure cannot yet be established from the upstream's published terms, the
   document must say so explicitly, name the spike that will settle it, and state
   the interim restriction it imposes on consumers — **an unstated figure is never
   an absent obligation.**
6. Its status mapping: which upstream response becomes which
   `ScriptureLookupStatus`, and which becomes which exception.
7. A reserved area prefix registered in [Design.md](Design.md)'s header table, and
   every claim about the upstream carrying a provenance tag.
8. **How a caller's `CancellationToken` composes with its own timeout budget.**
   Mandatory for any provider that has one — which is every provider in this
   solution (§ABS5 rule 8). `the-standard-cancellation-patterns` only bites once
   timeout logic exists, and that is exactly the point at which this contract stops
   being able to specify it for you: the linked source, the catch order and the
   pair of catch blocks are all per-provider code. §ABS13 gives the *semantics*
   every provider must produce; the provider document gives the *mechanism* that
   produces them — §APB6.1 and §YVN6.1 are the worked instances.

---

## ABS34. Consumer guidance: selecting and failing over (#1)

This library never chooses a provider. A consumer that wants "try A, fall back to
B" builds that above the abstraction. Two channels must be handled, not one — a
loop inspecting only `ScriptureResult.Status` will miss every availability
failure, because those are exceptions.

**Read the exception rows correctly.** A row naming a marker interface describes
the **inner** exception. Through `IBibleAbstractionProvider` every provider failure
arrives wrapped as `BibleAbstractionProviderDependencyException` (§ABS27), so catch
that — or `IBibleDependencyException`, which it carries — and read the specific
marker, `RetryAfter` and `QuotaResetsOn` off `InnerException`. Only a provider
called directly as `IBibleProvider` throws the marked type itself.

| Outcome | Channel | Action |
|---|---|---|
| `Found` | status | Return it. Check `IsTruncated`/`MissingVerseIds` before storing |
| `TranslationNotSupported` | status | **Try the next provider** — a different subscription may carry it |
| `NotFound` | status | **Stop.** The passage is not in that edition; asking again wastes quota |
| `InvalidReference` | status | **Stop.** The caller's input is bad |
| `Unknown`, or a status added later | status | **Treat as unavailable**, log at Error |
| `IBibleRateLimitException` | exception | **Try the next provider.** Transient; honour `RetryAfter` before using this one again |
| `IBibleQuotaExceededException` | exception | **Try the next provider, and stop asking this one** until `QuotaResetsOn`. Retrying burns quota you do not have |
| `IBibleAuthorizationException` | exception | **Try the next provider**, log at Error — a key is revoked or a licence was never accepted |
| `IBibleUnavailableException` | exception | **Try the next provider.** Transient |
| `BibleAbstractionProviderServiceException` (`IBibleServiceException`) | exception | **Try the next provider**, log at Error — that provider is defective. Do not suspend it on a duration; it will not self-heal |
| `BibleAbstractionProviderValidationException` | exception | **Stop and let it bubble** — a blank or unknown provider name is a wiring bug |
| `OperationCanceledException` | exception | **Let it bubble.** The caller went away |

```csharp
private static readonly string[] ProviderOrder = { /* from the app's own configuration */ };

public async ValueTask<ScriptureResult> GetScriptureAsync(
    string reference, CancellationToken cancellationToken)
{
    ScriptureResult? last = null;
    var failures = new List<Exception>();

    foreach (string providerName in ProviderOrder)
    {
        if (suspended.IsSuspended(providerName)) { continue; }     // backoff state, app-owned

        try
        {
            ScriptureResult result = await bibleAbstractionProvider
                .GetScriptureByReferenceAsync(providerName, reference, cancellationToken);

            if (result.IsFound) { return result; }

            last = result;

            if (result.Status is ScriptureLookupStatus.NotFound
                              or ScriptureLookupStatus.InvalidReference)
            {
                break;                                             // no provider will do better
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;                                                 // the caller went away
        }
        catch (Exception exception) when (exception is IBibleDependencyException)
        {
            failures.Add(exception);
            suspended.Record(providerName, exception);             // reads the marker, RetryAfter and
            continue;                                              // QuotaResetsOn off InnerException
        }
        catch (Exception exception) when (exception is IBibleServiceException)
        {
            failures.Add(exception);
            logger.LogError(exception, "Provider {Provider} is defective", providerName);
            continue;                                              // no duration to suspend on
        }
    }

    if (last is not null) { return last; }

    // Nothing answered and nothing was returned: every provider was unavailable. Do NOT synthesise a
    // NotFound here — "the passage isn't there" and "nobody could look" are the two things §ABS6 exists
    // to keep apart, and collapsing them hides an outage behind an empty result.
    throw new AggregateException("No provider could answer; all were unavailable.", failures);
}
```

**Put `ProviderConsole` in the alert, not just the log** (§ABS7.1). A quota or
authorization failure needs a human, and the exception already knows where that
human should go. The library deliberately ships no usage counters — the upstream's
own dashboard is the authority, and this is the pointer to it.

**Suspension durations.** Two markers carry one: `RetryAfter` and
`QuotaResetsOn`. For the other two the consumer chooses —
`IBibleUnavailableException` warrants a short fixed backoff (it is transient),
`IBibleAuthorizationException` a suspension until someone reconfigures the key,
with an alert, since it will not recover on its own.

**One constraint binds any such policy:** the key handed to the second provider
must carry the same `.TRANSLATION` suffix as the first call. An unqualified key is
not a retry at all — each provider fills in its own default (§ABS20), and in
Psalms, Joel and Malachi those editions may number the reference differently
(§ABS17). Resolve the translation once, up front.

---

## ABS35. Testing conventions (#1)

Per `the-standard-testing`: xUnit + FluentAssertions + Moq, randomised inputs via
Tynamix.ObjectFiller with DeepCloner, Given/When/Then comments,
`Should{Action}Async` naming, `VerifyNoOtherCalls()` closing every test,
`Xeption.SameExceptionAs()` for exception equality, which is the one thing a test
project adds `Xeption` for — **no shipped package references it (§SOL17 rule 6),
and no test project does yet either, because none has a test in it** — and partial
test classes — a
root plus `{Tests}.{Method}.Logic.cs`, `.Validations.cs`, `.Exceptions.cs`.
Failing-path tests use `ShouldThrow{Exception}On{Action}If{Condition}AndLogItAsync`
naming, which encodes the condition *and* the logging assertion in the name.

Three deviations and clarifications specific to this solution:

1. **Payload shapes are committed fixtures, not randomised.** The Standard
   randomises inputs by default, but content mapping is only meaningful against
   real USX and real upstream HTML. Randomise the values threaded through them
   (bibleId, verse text, copyright string, keys); **never randomise the
   structure**.
2. **Brokers get no unit tests** (§SOL8 rule 1). They are proven through
   acceptance tests only.
3. **Acceptance tests use `WireMockServer`, never a stubbed
   `HttpMessageHandler`.** A stubbed handler bypasses exactly the typed-client and
   resilience-pipeline configuration the provider documents spend a section on
   (§APB6, §YVN6), leaving that code untested while the test still passes. Start a
   server per test class, repoint `BaseUrl` at it, and drive the real provider
   through its real constructor and only through `IBibleProvider`.

---

## ABS36. `…Abstractions.Tests.Unit` (#1)

No HTTP anywhere.

1. **`UsfmReferenceTests`** — round-trips for verse, range, chapter, chapter
   range, single-chapter books; the positional rule including the collisions
   `PSA.23.TOB`, `JHN.3.16.JUB`, `TOB.3.16`, `TOB.3.TOB`, `JUB.1.1.KJV`; `JUD.5` →
   `InvalidReference`; mixed-granularity ranges rejected.
2. **`LooseReferenceParserTests`** — table-driven: full names, abbreviations,
   `1 John` vs `John`, parenthesised and bare translations, chapter ranges,
   `Jude 5` → `JUD.1.5`, multi-part rejected, garbage → a named token. Plus: an
   omitted translation resolves to the supplied default, and the parser never
   returns a null or empty `Translation`.
3. **`BibleReferenceTests`** (§ABS41) — the public surface, no network.
   `TryParse` accepts both a USFM key and a loose reference and produces the same
   `UsfmReference` for `MRK.3.1` and `Mrk 3:1`; `ToDisplayString()` renders
   `Mark 3:1` from both. Then the suggestion invariants, each its own test:
   an input that parses returns **no** suggestions (rule 4); every pair in rule 8's
   table resolves exactly and never suggests across — `Jos 1:1` stays Joshua,
   `Jn 3:16` stays John; a genuine tie returns both candidates rather than a winner
   (rule 5); garbage returns an empty list rather than the nearest book (rule 6);
   a suggestion carries the chapter and verse through **unchanged and unvalidated**,
   and a suggestion for a chapter that cannot exist is still returned, because
   validating it is not this library's job (rule 3).
4. **`LanguageScopingTests`** (§ABS42) — `"Juan 3:16"` parses under `["spa"]` and
   fails under `["eng"]`; the ordered preference list is honoured first-match;
   a token outside the declared scope is **never** matched even when a loaded table
   would accept it (§ABS42.4); `RenderReference` produces `John 3:16`, `Juan 3:16`
   and `Johannes 3,16` from the same `UsfmReference` including the German comma
   separator; rendering never consults `CultureInfo.CurrentUICulture` — assert by
   rendering under a hostile current culture and getting the edition's language
   anyway; an unknown script direction stays `Unknown` and never defaults to
   `LeftToRight`; `ScriptureHtmlRenderer` emits `dir="rtl"` for a RightToLeft
   passage and omits it otherwise.
5. **`ProviderServiceTests`** — root + `.GetProviderByName.{Logic,Validations,Exceptions}.cs`
   against hand-written fakes. Case-insensitive resolution; blank name →
   `InvalidProviderServiceException`; empty registration →
   `NullProviderServiceException`; unknown name → `NotFoundProviderServiceException`
   (assert explicitly it is not NRE-derived); duplicate names including case-only
   differences → `AlreadyExistsProviderServiceException` at construction.
6. **`BibleAbstractionProviderTests`** — root + the three files per method.
   Forwards to the named provider exactly once and returns its result untouched;
   the token reaches the provider; `Dispose()` disposes every provider. The §ABS27
   mapping in full, and **one test that proves the async mapping works**: a provider
   whose exception is thrown *after* its first `await` must still be classified,
   which a synchronous `TryCatch` would silently fail. A marked
   `IBibleRateLimitException` arrives as `BibleAbstractionProviderDependencyException`
   with the original as `InnerException`. `OperationCanceledException` passes
   through unwrapped. One negative test asserts the abstraction never calls a second
   provider.
7. **`BibleProviderBaseTests`** — over a fake subclass: parse failure
   short-circuits without calling `FetchAsync` and calls `FetchByRawReferenceAsync`
   when overridden; a missing translation is filled before `FetchAsync` sees it;
   `FetchAsync` receives the parsed reference, not a string; a marked exception
   passes through; an unmarked one becomes a service exception; caller cancellation
   propagates while a provider timeout becomes an unavailability exception; a null
   `Attribution` on a `Found` result logs a Warning.
8. **`ScriptureUsageTests`** — `ToStorageString`/`TryParse` round-trip for all four
   obligations, never null or empty including `NotRequired`, survives a token
   containing the delimiter, carries no endpoint.
9. **`TranslationMetadataMergeTests`** (§ABS45) — the merge as a pure function:
   per-field, so an entry supplying only `PublisherUrl` leaves a live `Attribution`
   intact; upstream wins where present; **null, empty and whitespace upstream values
   all fall through to config** (rule 3); matching is case-insensitive on
   `Abbreviation`; an unmatched abbreviation changes nothing; and a duplicate
   abbreviation throws at construction rather than last-one-wins (rule 4).
10. **`ScriptureMarkupTests`** (§ABS43) — `ToStorageString`/`TryParse` round-trip
   for all four trust levels; never null or empty including `None`; a
   default-constructed or field-missing value reads as `Unknown` and
   `IsSafeToRender` is false; `Generated` requires a non-null `GeneratedBy`; a blank
   or unparseable stored value parses to `Unknown` rather than throwing or
   defaulting to safe.
11. **`ScriptureHtmlRendererTests`** — golden tests: `q1` vs `q2` indent surviving,
   a `wj` run containing `add` rendering as `<span class="wj it">`, a speech
   crossing a verse boundary rendering as one span, a merged-verse label
   round-tripping, `SectionHeading` omitted from `Text` but present in `Html`,
   scripture text HTML-escaped.

---

## ABS37. `…Abstractions.Tests.Acceptance` (#1)

The public surface end-to-end with real collaborators: build a
`BibleAbstractionProvider` over stub providers and exercise it as a composition
root would — construct, resolve by name, get a result; an unknown name throws the
documented type; `BibleProviders` is the immutable snapshot handed in; disposal
cascades. Cheap, and catches accessibility mistakes that same-assembly unit tests
cannot see — which is the whole point, given §ABS11.

---

## ABS38. `…Abstractions.Conformance` (#1)

An opt-in package of abstract tests a **provider package** inherits, so every
provider is held to the contract by the same suite. It ships to NuGet so a
third-party provider can take the same medicine (§SOL7 rule 3), and carries no
`Tests` token in its name so CI does not try to run it directly (§SOL4 rule 2).

1. Every `Found` result carries a non-empty `Text`, a non-null `Usage`, and a
   `Usfm` that re-parses.
2. `Usage.ToStorageString()` survives a storage round-trip and returns a usage
   equal to the original.
3. Rate-limit and quota conditions surface as exceptions carrying the right
   marker — not as a `ScriptureResult`.
4. Caller cancellation propagates rather than being converted.
5. **The return direction holds too:** a provider driven to `NotFound`,
   `TranslationNotSupported` or `InvalidReference` *returns* a `ScriptureResult` and
   throws nothing. This is the mistake a provider author porting from an
   exception-heavy upstream will make.
6. An auth rejection surfaces as `IBibleAuthorizationException` and an upstream
   outage as `IBibleUnavailableException`.
7. **A provider-side timeout, with the caller's token unsignalled, surfaces as
   `IBibleUnavailableException` — not `OperationCanceledException`.** The subtlest
   rule in the contract (§ABS13 rule 3), and invisible without a test.
8. `Name` equals the provider's own `ProviderName` constant, and `Provider` on
   every returned `ScriptureResult` equals it too.
9. Every `Found` result carries a `Markup` that is never `Unknown`, round-trips
   through `ToStorageString()`, and is `None` exactly when `Html` is null (§ABS43).
10. Every thrown `IBibleDependencyException` exposes `ProviderConsole` — a value or
    a deliberate null, never a throw (§ABS7.1).
11. `GetTranslationsAsync` returns the same result twice without a second upstream
    request, every entry's `Abbreviation` round-trips through `UsfmReference`, and a
    cold-cache upstream failure **throws** rather than returning empty (§ABS44.2).
12. `Notes` is empty on every `Found` result and never null (§ABS39 rule 3).
13. A configured `TranslationMetadata` entry backfills a `Found` passage whose
    upstream attribution was absent, and does **not** displace one that was present
    (§ABS45.1).

---

## ABS39. Open questions (#1)

1. ~~**Where does a consumer's translation list come from?**~~ **Settled: an async
   `GetTranslationsAsync` on `IBibleProvider`, served from the cached catalogue.**
   §ABS44.
2. **TFM** — settled as `net10.0` (§SOL6). Recorded here only because earlier
   drafts left it open.
3. ~~**Footnotes and cross-references**~~ **Settled: the space is reserved, not
   built.** `ScripturePassage.Notes` and `ScriptureNote` exist (§ABS16, §ABS22) and
   are always empty; providers keep requesting notes suppressed. Populating them
   later is then **additive and MINOR** rather than a model change and MAJOR
   (§SOL7 rule 4) — which is the whole reason to spend the twenty lines now.

   **Reopened by evidence, and no longer hypothetical.** Biblica's licence — which
   covers NIV — requires that "all footnotes to the TRANSLATIONS text must be
   included along with the TRANSLATIONS text and accessible to the end-user"
   [verified, §YVN14.6]. The reserved-space decision still stands and is exactly why
   this is additive rather than breaking; what was wrong was assuming nothing needed
   them yet. **Footnote support is a precondition of serving Biblica-licensed
   translations**, not a later nicety, and eight further publisher agreements remain
   unread.

   **Whoever populates them must design the §APB9 interaction first.** A verse
   whose *only* content is a footnote is exactly the case the content check has to
   read as empty, and suppressing notes is currently what makes that detectable.
   Turning `include-notes` on without that pass would turn omitted verses into
   `Found` results carrying nothing but a footnote.
4. **A third provider** — §SOL17 rule 5.
5. ~~**Does `ScripturePassage` need an `AttributionUrl`?**~~ **Settled: no.** The
   per-quotation hyperlink API.Bible's terms require points at the *consumer's own*
   copyright page, which this library cannot know, and neither upstream puts a URL
   on a passage at all (§ABS44.5). The publisher links that page needs ride on
   `TranslationSummary.PublisherUrl` instead — populated for YouVersion, null for
   API.Bible, and fillable either way from `TranslationMetadata` (§ABS45).

   **The published-surface cluster is now closed.** `Language`, `ScriptDirection`
   (§ABS42.6), `Markup` (§ABS43), `Notes` (rule 3), `GetTranslationsAsync` (§ABS44)
   and `TranslationMetadata` (§ABS45) all landed during design — every one a
   `required` member or an interface member, and therefore free now and a major
   version after the first publish (§SOL7 rule 4). **Nothing in this cluster is
   still open**, which is what the first `RELEASES:` PR was waiting on.

---

## ABS40. Work breakdown (#1)

Abstraction-side items only. Each provider document carries its own breakdown, and
every item there depends on items 1–7 here.

| # | Item | Contents | Est. |
|---|---|---|---|
| 1 | **Models** | `IBibleProvider`, `ScriptureResult`, `ScripturePassage`, `ScriptureBlock`/`ScriptureSegment`/`ScriptureStyle`, book tables. The projects already exist; the scaffolding gaps are §SOL15 | 1 d |
| 2 | **`UsfmReference`** | Positional tokenizing (§ABS18), ranges, chapter ranges, single-chapter books, `ToProviderKey`/`ToDisplayString`, table-driven tests including the `TOB`/`JUB` collisions | 1 d |
| 3 | **`LooseReferenceParser`** | Three productions, abbreviation tables, the default fill-in rule (§ABS20), rich failures | 0.5–1 d |
| 4 | **Abstraction provider & exceptions** | `IBibleAbstractionProvider`/`BibleAbstractionProvider` + its async `.Exceptions` partial, Foundation `ProviderService` + `.Validations`/`.Exceptions`, the marker interfaces and the §ABS27 types, composition-root sample, and the Standard-shaped test suite — two partial test classes, root + Logic/Validations/Exceptions per method (~11 files) | 2–2.5 d |
| 5 | **`ScriptureHtmlRenderer`** | Block + inline vocabulary, run merging, the plain-text projection, `dir="rtl"`, the `ScriptureMarkup` assertion and its storage round-trip (§ABS43), golden tests. **Before item 6 and before any provider**, because providers render `Html`/`Text` through it and their acceptance assertions cannot pass without it | 0.5–1 d |
| 6 | **`BibleProviderBase`** | Parse → default fill-in → `FetchAsync`; `FetchByRawReferenceAsync`, `RenderReference`, `InternalServices`/disposal, logging scopes, the compliance warnings (§ABS32), the cancellation and exception-discipline arms (§ABS13); unit tests over a scripted fake subclass | 1–1.5 d |
| 7 | **Usage contract** | `ScriptureUsage` + storage round-trip, `ScriptureViewerContext`, `IScriptureUsageReporter` and its result/payload types (§ABS29, §ABS30). No reporter implementation — that ships with the provider that needs one | 0.5–1 d |
| 8 | **Conformance package** | A new project, plus the eight inherited tests in §ABS38 | 0.5 d |
| 9 | **Reference surface** (§ABS41) | `BibleReference` over items 2–3 — cheap, it is a facade. Then `Suggest`, `ReferenceSuggestion`, the confidence floor, and the invariant tests in §ABS36 item 3 including the one-character collision fixture. **Priced for the tests, not the matcher:** edit distance over the existing abbreviation table is an afternoon; proving `Jos` never becomes James is the work | 1–1.5 d |
| 10 | **Language scoping** (§ABS42) | Per-language table format, the ISO 639-3 scope parameter threaded through `BibleReference`/`RenderReference`, the `Language` + `ScriptDirection` DTO fields, `dir="rtl"` in the renderer, and a second shipped table used purely to prove the format is real. **The English table alone does not prove the design** — build it with two | 1–1.5 d |
| 11 | **Translation discovery** (§ABS44) | `TranslationSummary`, `GetTranslationsAsync` on the interface, the base class and the abstraction, projected from each provider's existing catalogue holder. Cheap because the cache already exists; the tests are the cold-cache-throws and no-second-request cases | 0.5 d |
| 12 | **Metadata merge** (§ABS45) | `TranslationMetadata`, the per-field merge as a pure function, applied to both the summary and the passage, plus the duplicate-abbreviation validation. Small, and the tests are the whole of it | 0.5 d |

Abstraction total ≈ **9.5–13 dev-days**. Sequencing that matters: 2 and 3 before
6; 5 before 6 and before any provider; 4 is independent of 5–6 and can run in
parallel. **Item 9 splits:** the `BibleReference` facade lands with items 2–3 and
`BibleProviderBase` routes its parse through it (§ABS41), so that half is not
optional; `Suggest` is additive and can follow any time after.

---

## ABS41. Reference normalization and suggestion (#1)

*Appended rather than slotted next to §ABS18–§ABS21, because section numbers are
never renumbered ([Design.md](Design.md), "Conventions"). Read it with those four.*

Parsing is already the first thing every lookup does (§ABS12), and the canonical
display form already round-trips through `ToDisplayString()` (§ABS18 rule 6) — but
until this section both were reachable **only as a side effect of a successful
fetch**. That is backwards: a consumer most wants to normalize and validate a
reference *before* spending a metered request (§SOL12), and a UI wants to echo
`Mark 3:1` back at someone who typed `Mrk 3:1` without calling an upstream at all.

### The surface

```csharp
// Glory2Him.BibleProviders.Abstractions — PUBLIC. No network, no provider, no configuration.
public static class BibleReference
{
    /// <summary>Canonical USFM key: "MRK.3.1", "MRK.3.1.NIV" (§ABS18).</summary>
    public static bool TryParseUsfm(string input, string? defaultTranslation,
        out UsfmReference reference, out string? failureReason);

    /// <summary>Loose human reference: "Mrk 3:1", "Juan 3:16", "1 Cor 13:4-7 (ESV)" (§ABS19).
    /// `languages` is an ordered ISO 639-3 preference list; first match wins (§ABS42.4).</summary>
    public static bool TryParseLoose(string input, string? defaultTranslation,
        IReadOnlyList<string> languages, out UsfmReference reference, out string? failureReason);

    /// <summary>Either form — USFM first, then loose. What BibleProviderBase does internally.</summary>
    public static bool TryParse(string input, string? defaultTranslation,
        IReadOnlyList<string> languages, out UsfmReference reference, out string? failureReason);

    /// <summary>Ranked candidates for an input that did NOT parse. Never auto-applied.
    /// Empty when the input already parses, and empty when nothing clears the floor.
    /// Suggestions come only from the declared languages — never from every loaded table.</summary>
    public static IReadOnlyList<ReferenceSuggestion> Suggest(
        string input, IReadOnlyList<string> languages, int maxSuggestions = 3);
}

public sealed record ReferenceSuggestion(
    string BookCode,        // "MRK" — the correction is to the BOOK TOKEN only, see rule 3
    string Display,         // "Mark 3:1" — rendered in the language that MATCHED (see below)
    string Usfm,            // "MRK.3.1" — the key this suggestion would produce
    double Confidence,      // 0.0–1.0, monotonic; comparable only within one call
    string OriginalToken);  // "Marrk" — what was replaced, for highlighting
```

`BibleProviderBase` routes its own parse through `BibleReference.TryParse`, so
there is one implementation and a consumer pre-validating a reference provably
gets the same answer the provider will.

**`Display` is rendered in the language whose table produced the match**, not in
the caller's first preference and not in English. A Spanish table matching
`"Jaun 3:16"` suggests `Juan 3:16` — echoing a correction back in a different
language from the one the reader typed is a worse experience than the typo. It is
therefore **not** the same thing as `ScripturePassage.Reference`, which renders in
the *edition's* language (§ABS42.5): at suggestion time no edition has been
resolved, and there is nothing else it could honestly use.

### Rules

1. **Normalization is free and offline.** No HTTP, no provider instance, no
   configuration beyond the optional `defaultTranslation` string (§ABS20). This is
   the whole point: a consumer validates and normalizes user input without spending
   a request against a 5,000/month plan.

2. **`Suggest` never auto-applies, and the parsers never consult it.**
   `TryParse*` still returns `false` for a typo, and a provider still answers
   `InvalidReference` (§ABS6). A caller that wants correction asks for it
   explicitly and decides what to do with the answer. **There is deliberately no
   "fuzzy" flag on `TryParse`** — an opt-in flag would be set once in a composition
   root and then silently govern every lookup in the application, which is exactly
   the outcome rule 3 exists to prevent.

3. **A suggestion corrects the book token only.** Chapter and verse pass through
   unvalidated and unchanged. This library cannot know whether `Mark 3:1` exists
   without knowing the edition — verse numbering is edition-relative (§ABS17) and
   there is deliberately no pre-flight availability check (§ABS5 rule 4). So a
   suggestion means *"you probably meant the book Mark"*, never *"Mark 3:1 is a real
   verse"*. Say so in the API documentation; a caller who reads it as validation
   will ship a bug.

4. **An input that parses is never suggested over.** `Suggest` returns empty for
   any input `TryParse` accepts. `Jos 1:1` is Joshua, stays Joshua, and is never
   "corrected" to James. This single rule is what makes the collision set in rule 6
   safe, and it is the first test to write.

5. **Ambiguity returns every candidate, never a winner.** Where two or more books
   tie, or sit within a narrow margin, all of them come back and the caller
   disambiguates. Collapsing a genuine ambiguity into one confident-looking answer
   is the failure this whole design exists to avoid — it is the same mistake as
   §APB9's empty-verse `Found` and §ABS18 rule 3's translation-code collision, in a
   different costume.

6. **A confidence floor, below which the list is empty.** Garbage returns nothing
   rather than a desperate reach for the nearest book. The floor is a documented
   constant, not a caller parameter — a caller able to lower it will lower it.

7. **The algorithm is not the contract; the invariants are.** Edit distance over
   the §ABS19 abbreviation table is the obvious implementation and a reasonable
   default. What is binding is rules 3–6 and the collision behaviour below, so the
   matcher can be replaced without a contract change.

8. **The one-character collisions are a committed test fixture, not a code
   comment.** Scripture abbreviations for *different books* sit one edit apart, and
   a naive matcher serves the wrong book under correct-looking attribution:

   | Pair | Books | Must |
   |---|---|---|
   | `Jn` / `Jon` | John / Jonah | each resolve exactly; neither ever suggests the other |
   | `Jas` / `Jos` | James / Joshua | as above |
   | `Jud` / `Jdg` | Jude / Judges | as above — and `Jud` interacts with §ABS18 rule 5's single-chapter rule |
   | `Phil` / `Phlm` | Philippians / Philemon | as above |
   | `TOB` / `JUB` | Tobit / Jubilees, *and* two translation codes | never suggested at all from a USFM key — §ABS18 rule 3 owns this |

   Each language's table brings its own such pairs and its own fixture. **Cross-language
   collisions cannot arise**, because matching never leaves the declared scope
   (§ABS42.4) — that bound is what stops this table growing combinatorially as
   languages are added.

### What this does not change

- **`InvalidReference` still means invalid.** §ABS6's channel rule is untouched: a
  provider handed an unparseable string returns `InvalidReference` with a reason,
  and does not consult `Suggest` on the caller's behalf.
- **Nothing becomes provider-aware.** `BibleReference` has no catalogue, so it
  cannot tell you whether a translation is licensed — that stays
  `TranslationNotSupported` at fetch time (§ABS5 rule 4), and stays the open
  question in §SOL17 rule 3.

---

## ABS42. Language scoping (#1)

*Appended rather than slotted next to §ABS18–§ABS19, per the no-renumbering rule.
Read it with those two and with §ABS41.*

**This library is multilingual.** A caller may ask for scripture in any language
and edition its providers carry. That is a scope decision, and it is load-bearing:
YouVersion's catalogue is *already* language-scoped by design (§YVN7), so a
library that parsed and rendered only English would silently contradict the
provider model underneath it.

Until this section the design implied multilingual support while §ABS19's
book-name table could not deliver it and §ABS16 pinned display to English. This
section is the reconciliation.

### ABS42.1 USFM is the language-neutral spine (#1)

`MRK.3.1` is `MRK.3.1` in every language. Book **codes** are USFM's, not
English's, so everything downstream of a successful parse — `UsfmReference`,
`ToProviderKey()`, the provider key, the persisted `Usfm` — is unaffected by this
section. Only two edges are language-sensitive: **reading** a human reference, and
**writing** one back out.

That is what makes the rest of this cheap. Nothing about storage, routing,
versification (§ABS17) or the provider contract changes.

### ABS42.2 Parse language and display language are different, and resolve at different times (#1)

The distinction the design was missing:

| | When it is known | Where it comes from |
|---|---|---|
| **Parse language** | *Before* any network call, in `BibleProviderBase` | Declared by the caller or configured — the catalogue has not been consulted yet |
| **Display language** | *After* the catalogue resolves, when `ScripturePassage` is built | The resolved edition's own language |

A loose reference must be read before we know which Bible it names, so the parser
cannot infer its language from the edition. But by the time `Reference` is
rendered, the edition **is** known — so display can and should follow the
scripture's own language.

### ABS42.3 ISO 639-3 is the common currency (#1)

Both upstreams already speak it: API.Bible documents its catalogue `language`
filter as an ISO 639-3 three-letter code [verified], and YouVersion's
`language_ranges` takes the same (`eng`) [verified]. So this library uses ISO 639-3
throughout — configuration, parse scope, and the `Language` field in §ABS42.6 —
and never invents a parallel scheme or uses two-letter codes.

### ABS42.4 Parsing is scoped to a declared set, never "try every language" (#1)

```csharp
public static bool TryParseLoose(string input, string? defaultTranslation,
    IReadOnlyList<string> languages, out UsfmReference reference, out string? failureReason);
```

`languages` is an ordered ISO 639-3 preference list; first match wins. Providers
pass their own configured list, defaulting to `["eng"]`.

**Trying every loaded table is forbidden, and the reason is §ABS41 rule 8.** The
one-character collisions between *different books* — `Jn`/`Jon`, `Jas`/`Jos` — are
already the sharpest hazard in the parser within a single language. Matching across
every language at once multiplies that surface by the number of tables and makes it
grow every time someone contributes one, which is exactly the kind of hazard that
gets worse silently. A bounded, declared scope keeps the collision set finite,
reviewable, and testable.

The preference-list shape is deliberately the same as YouVersion's own
first-range-wins model (§YVN7 rule 1), so a deployment configures one idea, not two.

### ABS42.5 Display renders in the edition's language, and the §ABS16 invariant survives (#1)

`RenderReference` gains the resolved edition's language:

```csharp
protected string RenderReference(UsfmReference reference, string languageCode);
```

So a Spanish edition yields `Juan 3:16`, a German one `Johannes 3,16` — including
the **chapter/verse separator**, which is not universally `:`; German convention is
a comma. The separator is per-language table data, not a constant.

**§ABS16 rule 2's invariant is preserved, and it is worth stating why, because it
looks threatened.** That rule says `Usfm`, `Reference`, `Translation` and `Text`
are identical for a given lookup whoever answered. Rendering is a **pure function
of (`UsfmReference`, language)**, and the language is taken from the *resolved
edition*, never from the provider or from ambient culture. Two providers serving
the same edition therefore render the same string. The invariant holds; what
changed is that the function has a second argument.

**Never `CultureInfo.CurrentUICulture`.** The reference labels scripture, not the
user interface: a Spanish-speaking reader looking at a KJV passage is reading
`John 3:16`, and localising that label to `Juan 3:16` would mislabel English text.
A consumer wanting a UI-language label has both `Usfm` and `Language` and can
render its own.

### ABS42.6 `ScripturePassage` carries the language and its script direction (#1)

Two additions, both `required` and both sourced from the catalogue:

```csharp
public required string Language { get; init; }              // ISO 639-3, e.g. "eng", "spa", "heb"
public required ScriptDirection ScriptDirection { get; init; }

public enum ScriptDirection { Unknown = 0, LeftToRight = 1, RightToLeft = 2 }
```

**Script direction is not decoration.** Hebrew, Arabic, Farsi, Urdu and Syriac
scriptures are right-to-left, and a consumer that stores `Html` without knowing
this renders them wrongly with no error. API.Bible's catalogue `language` object
carries `script` and `scriptDirection` [verified]; YouVersion's equivalent field is
[unverified] and is a spike item (§YVN19). Where an upstream does not supply it,
the provider maps from the language code against a small built-in table and falls
back to `Unknown` — never silently to `LeftToRight`, which is a guess wearing a
fact's clothing.

`ScriptureHtmlRenderer` emits `dir="rtl"` on its root element for a RightToLeft
passage (§ABS23), so the default rendition is correct without the consumer doing
anything.

### ABS42.7 Book-name tables are data, and English is the only one that ships (#1)

One table per ISO 639-3 code: full names, common abbreviations, and the
chapter/verse separator convention. English ships in the box. **Additional
languages are additive data and a new table is not a contract change** — which is
what §ABS19 rule 1's "hook for additional languages" was gesturing at and never
specified.

Each new table arrives with its own collision fixture (§ABS41 rule 8) proving it
does not suggest across books *within* itself. Cross-language collisions cannot
occur by construction, because §ABS42.4 forbids matching outside the declared
scope.

### ABS42.8 What this does not do (#1)

- **No translation of scripture text.** Text is served in the edition's language,
  as fetched. This library never translates.
- **No language *detection*.** The scope is declared, not sniffed (§ABS42.4).
- **No transliteration**, and no romanised aliases beyond what a language's own
  table chooses to carry.
- **It does not make a translation available.** Which editions exist stays a
  subscription question answered at fetch time (§ABS5 rule 4). A configured parse
  language with no licensed edition behind it still yields
  `TranslationNotSupported`.

---

## ABS43. Markup provenance (#1)

*Appended per the no-renumbering rule. Read it with §ABS16 and §ABS23.*

`Html` is a rendition of scripture fetched from an upstream, stored by the
consumer, and eventually written into a page. §ABS23 says this library escapes
every text node; §SOL11 told the consumer to sanitize on write anyway. Both are
right, and together they were **unreadable**: nothing on the passage said which
kind of markup a consumer was holding, so "sanitize everything" was the only safe
reading and the escaping we already do bought the consumer nothing.

This follows the `ScriptureUsage` pattern exactly (§ABS29), for the same reason it
exists there: **a positive assertion is not the same as an absence**, and the
assertion has to survive storage.

```csharp
// On ScripturePassage, alongside Html:
public required ScriptureMarkup Markup { get; init; }

public sealed class ScriptureMarkup
{
    public required MarkupTrust Trust { get; init; }
    public required string Provider { get; init; }
    public string? GeneratedBy { get; init; }        // "ScriptureHtmlRenderer" — null unless Generated
    public string? UntrustedReason { get; init; }    // diagnostics; why it is not ours

    [MemberNotNullWhen(true, nameof(GeneratedBy))]
    public bool IsSafeToRender => Trust == MarkupTrust.Generated;

    public static ScriptureMarkup Generated(string provider, string generatedBy) => …;
    public static ScriptureMarkup None(string provider) => …;          // Html is null
    public static ScriptureMarkup Untrusted(string provider, string reason) => …;

    /// <summary>One storage field, never null and never empty — same contract as
    /// ScriptureUsage.ToStorageString(), so a blank stored value is provably a bug.</summary>
    public string ToStorageString();
    public static bool TryParse(string? storedValue, out ScriptureMarkup markup);
}

public enum MarkupTrust
{
    Unknown   = 0,   // never produced by this library — treat as untrusted
    None      = 1,   // Html is null; nothing to trust or distrust
    Generated = 2,   // built by ScriptureHtmlRenderer from Blocks, every text node escaped
    Untrusted = 3,   // markup this library did not generate — sanitize before rendering
}
```

### Rules

1. **The claim is provenance, not a scan — and that is a stronger claim, so say it
   precisely.** `Generated` does **not** mean "we ran a sanitizer over upstream
   markup and believe it is clean". It means *this library built this markup itself*
   from a parsed block model (§ABS22), emitting a closed vocabulary of elements and
   classes, with every text node HTML-escaped (§ABS23 rule 5). No upstream tag, no
   upstream attribute and no upstream URL survives into it. A sanitizer is a
   denylist that can be wrong; generation from a closed model cannot carry through
   something it has no way to express.

2. **`Unknown = 0`, so nothing is trusted by default.** A `ScripturePassage`
   hand-built in a test, deserialized from a payload missing the field, or read
   back from a row written before this existed reads as `Unknown` and must be
   treated as untrusted. Identical reasoning to `ScriptureLookupStatus.Unknown`
   (§ABS15 rule 1) and `ScriptureUsageObligation.Unknown` (§ABS29).

3. **`required`, so a provider states a position.** A provider that produced no
   `Html` says `None`; it does not leave the field to a default.

4. **`Untrusted` ships even though nothing produces it today** — the same argument
   §ABS8 makes for a quota type an upstream may never throw. A future path *will*
   want it: a provider that passes upstream markup through, a fast path that skips
   the block model, a cached rendition from an older vocabulary. If the value does
   not exist, that path is silently indistinguishable from a safe one, which is the
   exact failure this section removes.

5. **It round-trips through one non-empty storage field**, so §SOL11's guidance
   becomes conditional and checkable rather than a blanket instruction: a consumer
   renders `Html` directly when the stored `Markup` parses to `Generated`, and
   sanitizes otherwise. A blank or unparseable stored value is provably a mapping
   bug, not an absence — and is treated as `Unknown`, meaning sanitize.

6. **`ProviderMetadata` is still not markup.** Raw upstream payloads live there for
   diagnostics (§ABS16 rule 5) and are never rendered. This section does not make
   them renderable, and nothing in it should be read as licensing that.

### What it does not promise

`Generated` is a statement about **this library's output**, not about what happens
to it afterwards. A consumer that stores `Html`, edits it, concatenates it with
something else, or templates values into it has produced new markup and owns it.
The assertion travels with the row precisely so that such a consumer can tell it is
no longer holding what we handed it.

---

## ABS44. Translation discovery (#3)

*Settles §SOL17 rule 3 and §ABS39 rule 1. Appended per the no-renumbering rule;
read it with §ABS4 and §ABS5 rule 4.*

A consuming application needs to populate a translation list, and until now had no
way to: each provider's catalogue is private, and the only way to learn a
translation was unavailable was to spend a metered request and read
`TranslationNotSupported` (§SOL12).

```csharp
public sealed record TranslationSummary(
    string Abbreviation,          // "NIV" — the key callers pass back in a USFM reference
    string Name,                  // "New International Version"
    string Language,              // ISO 639-3 (§ABS42.3)
    ScriptDirection ScriptDirection,
    string? Attribution,          // the edition's copyright text, where the catalogue carries it
    string? PublisherUrl,         // the IP holder's page, where the upstream exposes one — §ABS44.5
    string ProviderEditionId);    // the upstream's own id — opaque, for diagnostics and Usage
```

### ABS44.1 Why a method and not a property (#3)

A `IReadOnlyCollection<string> KnownTranslations { get; }` was the obvious shape
and is the wrong one: **it would lie.** The catalogue loads lazily over HTTP
(§APB7, §YVN7), so a synchronous property either blocks on I/O behind a property
getter, or returns empty before the first fetch — reporting "no translations" for a
provider carrying hundreds. An async method is honest about what it does.

### ABS44.2 What it costs (#3)

**Nearly nothing, which is what makes it worth having.** Both providers already
fetch and cache the whole catalogue to resolve an abbreviation to an upstream id,
so this is served from memory once warm and spends an upstream request only on a
cold cache. It is the same cached object, projected — not a second call, and not a
second cache.

It therefore obeys the same holder rules as the catalogue it reads: TTL honoured,
faults not memoized, single-flight refresh, serve-stale-on-failure (§APB7 rule 5).
A cold-cache call that cannot reach the upstream **throws** an availability
exception rather than returning empty — an empty list means "the catalogue has
nothing", and an outage must never be mistaken for that (§ABS6).

### ABS44.3 What it does not promise (#3)

1. **It is a snapshot, not a guarantee.** §ABS5 rule 4 stands: availability is
   subscription-driven and can change between this call and the next lookup. A
   caller populates a list from it; a caller must not gate a fetch on it.
2. **It is not a support check.** Attempting the lookup and handling
   `TranslationNotSupported` remains the only correct way to find out.
3. **`Attribution` here is nullable and often null**, because not every catalogue
   carries copyright on its list response — API.Bible needs
   `include-full-details=true` for it (§APB7 rule 3), and this design does not send
   that on the hot path. A provider fills it when it has it.
4. **`PublisherUrl` is null more often than not**, and §ABS44.5 says which
   provider supplies it.

### ABS44.5 Publisher links, and why they are on the catalogue and not the passage (#3)

API.Bible Terms §7 requires a hosted copyright page carrying "IP Holder details,
and website links" (§APB19). Neither upstream puts such a link on a **passage** —
checked against both schemas [verified] — so it could only ever come from the
catalogue, which is why `TranslationSummary` is where it belongs rather than
`ScripturePassage`.

The two upstreams then differ, and the nullable type is carrying that difference
rather than papering over it:

| Provider | What the catalogue exposes |
|---|---|
| **YouVersion** | **`publisher_url`** — "URL to link to publisher page from the reader's footer" — plus `copyright` and `promotional_content`, a longer form of the copyright text [verified] |
| **API.Bible** | **Nothing.** No URL property exists on the Bible or Passage schema. `info` is a *string* of publisher information, not a link [verified] |

So `PublisherUrl` is populated for YouVersion and **null for API.Bible**, which is
the provider whose terms demand the link. **A consumer building an API.Bible
copyright page must source those links itself** — from the licence paperwork, or
from the Digital Bible Library via the `dblId`/`relatedDbl` the catalogue does
carry, though nothing documents a URL shape for those and this design does not
invent one.

That asymmetry is worth keeping visible rather than smoothing: a null here is not
a gap in the mapping, it is an upstream that does not have the data its own terms
ask a consumer to display.

### ABS44.4 The abstraction forwards it (#3)

`IBibleAbstractionProvider` gains the matching overload taking a provider name
(§ABS24), classified through the same `TryCatch` as everything else (§ABS10). It
does **not** merge across providers: two providers may carry the same abbreviation
for different editions, and silently unioning them would produce a list no single
provider can serve. Merging, if an application wants it, is an orchestration
concern (§SOL10).

---

## ABS45. Translation metadata and the config backfill (#3)

*Appended per the no-renumbering rule. Read it with §ABS44 and §ABS32.*

§ABS44.5 established that the upstreams disagree about what metadata they carry,
and that the provider whose terms demand a publisher link is the one that exposes
none. This section closes that with a **merge**: whatever the upstream supplies
wins, and configuration fills the rest, so a consumer gets a complete record from
either provider.

```csharp
// Glory2Him.BibleProviders.Abstractions — PUBLIC
public sealed record TranslationMetadata
{
    public required string Abbreviation { get; init; }   // the key; matches TranslationMap's
    public string? Name { get; init; }
    public string? Attribution { get; init; }
    public string? PublisherUrl { get; init; }
    public string? Language { get; init; }               // ISO 639-3
    public ScriptDirection? ScriptDirection { get; init; }
}
```

Each provider's configuration POCO gains
`IList<TranslationMetadata> TranslationMetadata { get; set; } = new List<TranslationMetadata>();`
— **a property on each POCO, not a shared base type**, because §ABS5 rule 1 keeps
the configuration objects plain and unrelated. The *merge* is shared; the
*configuration* is not.

### ABS45.1 The merge rule (#3)

**Field-level, upstream-wins-where-present, config-fills-the-rest.**

1. Merge **per field, not per object.** A config entry supplying only
   `PublisherUrl` fills exactly that and leaves everything else to the upstream. A
   whole-object fallback would mean one missing URL discarded a live copyright
   string.
2. **Upstream wins where it returned a value**, because that value is current and
   config is a snapshot someone typed. This is the opposite precedence to
   `TranslationMap` (§APB7 rule 1), and deliberately so: `TranslationMap` overrides
   *identity* — which edition to fetch, where the consumer knows better than an
   ambiguous abbreviation — while this overrides *description*, where the publisher
   is the authority and staleness is the risk.
3. **A blank upstream value counts as absent.** Null, empty and whitespace all
   fall through to config; §ABS32 exists because a null `Attribution` is a
   compliance event, and treating `""` as "the upstream said so" would preserve the
   defect this section removes.
4. **Matching is by `Abbreviation`, case-insensitively**, the same key
   `TranslationMap` uses. A duplicate abbreviation in the collection is a
   configuration error and **throws at construction**, alongside the other eager
   validation (§ABS5 rule 1) — last-one-wins would silently pick a copyright notice.
5. **It applies to both surfaces**: `TranslationSummary` (§ABS44) and
   `ScripturePassage.Attribution`. A passage whose upstream copyright was missing is
   backfilled from the same entry, which is the point.
6. **The merge is the foundation service's**, applied once where the passage and
   the summary are built (§SOL2 rule 2). Not the broker, not the façade.

### ABS45.2 What it fixes, per provider (#3)

| | Upstream supplies | Config typically supplies |
|---|---|---|
| **API.Bible** | `Attribution` on every passage | `PublisherUrl` — always null upstream (§ABS44.5) |
| **YouVersion** | `Attribution`, `PublisherUrl`, `promotional_content` | usually nothing |

So the asymmetry stops reaching the consumer: both providers can now yield a
`TranslationSummary` and a `ScripturePassage` carrying everything Terms §7 asks a
consumer to display (§APB19).

**`Attribution` stays `required` and stays nullable** (§ABS16). This section makes
null *avoidable*, not impossible — a translation with no upstream copyright and no
config entry still returns null, and §ABS32's Warning still fires. That is correct:
the warning is how a consumer discovers it needs a config entry.

### ABS45.3 No copyright data ships in the package (#3)

**Deliberate, and the strongest rule in this section.** It would be easy to ship a
prefilled table so consumers get publisher links with no configuration, and this
design does not, because:

1. **It is legal text about third-party IP we do not own.** A stale copyright
   notice presented as authoritative is the consumer's breach, caused by us.
2. **A NuGet package cannot be corrected in place.** Fixing a publisher's amended
   notice would need a release, and consumers pick releases up whenever they pick
   them up — with versions moving in lockstep across five packages (§SOL7 rule 3).
3. **It contradicts the refresh obligation.** API.Bible Terms §11 requires stored
   content be checked at least every 30 days (§APB17); a table compiled into a
   binary is the opposite of a refreshable cache.
4. **Getting it right for public-domain editions makes it worse, not better.**
   `KJV → "Public Domain"` is stable and correct, which lends unearned credibility
   to the `NIV` row next to it that went stale two releases ago.

**What ships instead: a sample configuration block in each provider's README**
(§SOL19.2 item 4), carrying the common editions, clearly dated and clearly the
consumer's to own. Same head start, no staleness baked into a binary, and the
consumer has actually read the notice they are displaying.
