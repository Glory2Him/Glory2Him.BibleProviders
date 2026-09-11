# Design — Bible Providers

**Area prefix:** `SOL` · **Sections:** §SOL1 – §SOL18

How this system is built: package boundaries, layer placement, the provider
contract, and the decisions that belong to no single package. `INTENT.md` says
what the system is *for* — it does not exist yet (§SOL17 rule 1).

**This document is authoritative.** An issue that disagrees with it is stale
intent, not an instruction — correct the issue.

The architect writes here. Nobody else does.

| Area | Prefix | Document |
|---|---|---|
| Solution overview | `SOL` | this file |
| The provider contract | `ABS` | [Abstractions.md](Abstractions.md) |
| API.Bible provider | `APB` | [ApiBible.md](ApiBible.md) |
| YouVersion provider | `YVN` | [YouVersion.md](YouVersion.md) |

---

## Conventions

**The split has happened.** `Documentation/Design.md` was a single stub; it is now
four area-scoped files under `Documentation/Design/`. Everything below follows
`DEVELOPERS.md` §4 and §6, adjusted for the fact that the split is done rather
than hypothetical.

**Numbered, prefixed, flat.** Every section carries an area prefix and a flat
number that never restarts — `§ABS1`, `§ABS2`, `§APB1` — so a bare citation stays
unambiguous with four files side by side:

```csharp
// design §ABS6: scripture outcomes return, availability failures throw
// (§APB15 rule 3, §ABS42.5)
```

Number rules within a section too, so a citation can be precise about which one
it means. A section number is never reused and never renumbered; a section that
dies is struck through, not deleted.

**A new area reserves its prefix in the table above before its file is written.**
Reserved so far: `SOL`, `ABS`, `APB`, `YVN`. A third provider takes the next free
three-letter token and gets `Documentation/Design/<Provider>.md` (§ABS33).

**Every heading carries exactly one tag, never bare:**

```markdown
## EVN14. Saved searches panel (#512)
## EVN15. Search result density (needs issue)
```

`(#N)` names the **most recent** issue that authoritatively defined the section —
not an accumulating list, because `git log` and `git blame` already give the full
history. `(needs issue)` is an explicit, greppable flag for design content nobody
has scheduled yet, and is what the analyst's sweep mode looks for:

```bash
grep -rnE "^#{2,3} .*\(needs issue\)" Documentation/Design/*.md
```

The two example headings above sit inside a code fence and still match that grep.
They use `EVN`, which is not a reserved prefix in this solution, so a sweep can
discard them on sight rather than chasing a section that does not exist.

**Every heading in all four files currently carries `(#1)`** — the issue that
defined this design in one pass. A sweep therefore finds nothing, and that is
correct rather than a gap: the tag answers *"which issue defined this section?"*,
not *"is it built yet?"*.

**The implementation backlog is the work-breakdown table at the foot of each
file** — §SOL15, §ABS40, §APB25, §YVN21 — not the heading tags. A section whose
design is later extended gets the tag of whichever issue extended it; a section
added with no issue behind it gets `(needs issue)` and the sweep picks it up.

The tag is mandatory rather than inferred, because a bare heading is ambiguous —
deliberately skipped, or just missed? Requiring the tag forces the decision every
time a section is touched.

**Relocated sections keep a `(formerly §X)` annotation** so code comments citing
the old number still resolve by grep. Two cautions worth inheriting rather than
rediscovering: resolving is not the same as being right — the annotation says
nothing about whether the section was the correct one to cite originally; and
nothing validates citations, so the annotation convention is the whole guarantee.

**Provenance tags.** These documents describe two upstreams this repository does
not control, so a claim's evidence is part of the design:

| Tag | Meaning |
|---|---|
| **[verified]** | Confirmed against the upstream's published documentation, cited inline. |
| **[unverified]** | Asserted by this design but not confirmed from any published source. Must be settled by the named spike before the code depending on it ships. |
| **[contested]** | Two sources disagree. Both are named, and the interim rule says which one the code follows. |

An untagged claim is a decision of ours, not a claim about an upstream.

---

## SOL1. What this solution is (#1)

A .NET library that fetches scripture from more than one upstream Bible API
behind one contract, and hands the consuming application a DTO it can store,
render and attribute.

1. A **provider-agnostic abstraction** that fetches scripture by either a
   translation-qualified USFM key (`JHN.3.16.NIV`) or a loose human reference
   (`"John 3:16 NIV"`).
2. **Concrete implementations** for API.Bible (American Bible Society) and
   YouVersion Platform (Life.Church).
3. A **provider abstraction layer, not a fallback engine.** The abstraction
   resolves *which* provider answers, by name; it never decides *whether* to try
   a second. Cross-provider selection and fallback are application concerns,
   built above the library (§SOL10).
4. Preserve **rich formatting** — red-letter words of Jesus, poetry indentation,
   deity names — alongside plain text, in a provider-neutral shape.
5. **Serve any language a provider carries.** References are read and rendered in
   the reader's scripture language, not only English, and right-to-left editions
   render correctly by default (§ABS42).
6. Return a **DTO, not a domain entity.** The consuming application maps it onto
   whatever it stores; audit and approval concerns are not a provider's business.

**The pattern in one paragraph.** A single **abstraction provider** aggregates
every registered provider and forwards each call to one of them by name.
Providers are constructed by the consuming application — each taking its own
configuration POCO and an optional logger — and handed to the abstraction as a
collection. The abstraction resolves the name and forwards; it never chooses
between providers, never retries, and never inspects a result. Failures cross
package boundaries as **marker interfaces**, so the abstraction and the consumer
can both classify a failure from a provider they hold no reference to. Layering
inside each package is Broker → Foundation Service → public face, with exception
handling on the Validation / Dependency / Service split. The full contract is
[Abstractions.md](Abstractions.md).

---

## SOL2. Architectural shape — the seven questions, answered for a library (#1)

The architect's checklist assumes a service with entities, events and a database.
This solution has none of those, and saying so explicitly is more useful than
leaving the headings unanswered — a later reader must not infer that storage was
forgotten.

1. **Problem.** A consuming application needs scripture text for a reference, from
   whichever licensed upstream can serve it, in one shape, with the licence
   obligations attached to it rather than discovered later.

2. **Layer placement.** Per package: **Broker → Foundation Service → the
   package's public face.** The broker is the HTTP boundary and holds no logic
   (`the-standard-brokers`; `CLAUDE.md` non-negotiable). The foundation service
   owns catalogue resolution, endpoint selection, content mapping and failure
   categorization for exactly one upstream. The public face —
   `ApiBibleProvider`, `YouVersionProvider`, `BibleAbstractionProvider` — is a
   **façade over a single foundation service**, not an orchestration.

   That distinction is load-bearing: an orchestration is bound by the Two-Three
   (Florance) rule and by "one kind of dependency, never a mix". A façade over
   one service is bound by neither, because it coordinates nothing. **No layer in
   this solution is skipped, and the orchestration-may-call-foundation exception
   in `CLAUDE.md` is never exercised here** — there are no orchestrations at all.
   Cross-provider selection, which *would* be an orchestration, is deliberately
   the consumer's (§SOL10).

3. **Entity count.** **Zero persisted entities.** Nothing in this solution has an
   identity, a lifecycle or a row. `ScripturePassage` is a DTO — a value returned
   and forgotten. Entity count therefore cannot be what decides the layer here,
   and the layer is decided by the dependency count instead: one upstream, one
   broker, one foundation service, one façade.

4. **Event contracts.** **None.** There is no bus, no envelope and no publisher
   in this solution. The `CLAUDE.md` rule that identity travels on the signed
   event envelope is therefore vacuous here — and a provider that grew an
   ambient identity accessor would violate it in spirit, which is why
   §ABS5 rule 1 forbids ambient state of any kind.

5. **Storage and migration shape.** **None.** No database, no migrations, no
   seed. The consuming application stores; §SOL11 says what shape to store, and
   the upstreams' retention rules (§APB17, §YVN14) bound how long.

6. **Security boundary.** Not identity — **credentials**. Three rules:
   - An API key or app key reaches exactly one assembly: the provider package
     that owns it, via its configuration POCO. It is never static, never
     ambient, never logged (§SOL14).
   - `Glory2Him.BibleProviders.ApiBible.Fums` handles no key at all. It is a
     separate assembly that does **not** reference the provider package, so "the
     assembly that fetches scripture cannot reach the reporting endpoint, and the
     assembly that reports holds no credential" is a fact about the assembly
     graph, assertable in CI, rather than a rule a reviewer must remember
     (§SOL5, §APB22).
   - A FUMS token is **not** a credential — it is a per-fetch capability safe in
     page markup, which is why the browser reporting path exists at all
     (§APB16). Do not let the two travel on the same rules.

7. **Risks — reversible and not.**

   | Risk | Reversible? |
   |---|---|
   | Getting the layer split wrong inside a package | Yes — internal types, no published surface |
   | A wrong `ScriptureLookupStatus` mapping | Yes — a code change |
   | Shipping `IBibleProvider`, the DTOs or the markers and then changing them | **No** — published NuGet surface, major-version break (§SOL7) |
   | Choosing edition-native versification (§ABS17) | **No** — it decides what every persisted USFM key *means* |
   | Omitting `KnownTranslations` from `IBibleProvider` (§SOL17 rule 3) | **No** — adding a member to a shipped interface is a break |
   | Consumers persisting scripture before an upstream's terms are read (§YVN14) | **No** — a licence breach is not undone by a refactor |

8. **Out of scope.** §SOL13.

---

## SOL3. Why the shared code sits in Abstractions (#1)

**The key asymmetry between the two upstreams drives the structure.** API.Bible
can resolve loose references server-side and returns structured JSON content
carrying USX style names. YouVersion is USFM-in / HTML-out, with no server-side
reference parsing and no structured alternative.

Reference parsing and HTML normalization therefore live in **shared code inside
the Abstractions package** — `UsfmReference`/`LooseReferenceParser`, driven from
`BibleProviderBase`, and `ScriptureHtmlRenderer` — so every provider behaves
consistently and the weaker upstream is brought up to the stronger one's
behaviour rather than the contract being lowered to its level.

Note "the Abstractions **package**", not `BibleAbstractionProvider`. The
abstraction *provider* is a thin name-resolving forwarder and does neither of
these things (§ABS25).

---

## SOL4. Naming (#1)

Every namespace, assembly and package id in this solution is
`Glory2Him.BibleProviders.*`. The provider **names** — the strings that route a
call and get persisted by consumers — are `"ApiBible"` and `"YouVersion"`, and
are deliberately decoupled from the CLR type (§ABS14).

Three naming decisions are settled here so nobody churns them later:

1. **`…Tests.Integrations`, plural.** It is inconsistent with the singular
   `…Tests.Unit` and `…Tests.Acceptance`, and it is what the repository already
   has. `CLAUDE.md` documents the discovery glob as `*Tests.Integration*.csproj`,
   which matches the plural form as a substring, so nothing is broken. **Keep the
   plural.** Renaming four projects to fix a cosmetic inconsistency buys nothing
   and invalidates every existing path.
2. **`…Abstractions.Conformance` carries no `Tests` token, deliberately.** It is
   a shipped library of *abstract* test base classes, not a test project. Naming
   it `…Conformance.Tests.Unit` would make CI's `*Tests.Unit*` glob discover a
   project with no concrete tests in it (§SOL7 rule 1, §ABS38).
3. **`…ApiBible.Fums` is a sibling of the provider, not a child of it.** The dot
   suggests containment; the project reference graph deliberately has none
   (§SOL2 rule 6).

---

## SOL5. Solution layout — what exists and what this design still needs (#1)

Projects sit **flat at the repository root**; the grouping into
`/Abstractions/`, `/Providers/ApiBible/`, `/Providers/YouVersion/`,
`/Infrastructure/` and `/Documentation/` is done by **solution folders** in
`Glory2Him.BibleProviders.slnx`, not by directories on disk. That is the existing
convention and this design keeps it — a physical reorganisation would rewrite
every `ProjectReference` path and every `Content Include="..\LICENSE.txt"` in
the solution for no functional gain.

| Project | State | Purpose |
|---|---|---|
| `Glory2Him.BibleProviders.Abstractions` | **exists** | The contract, DTOs, parsers, renderer, base class, abstraction provider (§ABS) |
| `…Abstractions.Tests.Unit` | **exists** | §ABS36 |
| `…Abstractions.Tests.Acceptance` | **exists** | §ABS37 |
| `…Abstractions.Tests.Integrations` | **exists** | No upstream to integrate with. §SOL7 rule 2 |
| `…Abstractions.Conformance` | **to create** | The inherited contract suite (§ABS38) |
| `…ApiBible` | **exists** | §APB |
| `…ApiBible.Tests.Unit` / `.Tests.Acceptance` / `.Tests.Integrations` | **exists** | §APB24 |
| `…ApiBible.Fums` | **to create** | The usage reporter (§APB22) |
| `…ApiBible.Fums.Tests.Unit` | **to create** | §APB24 |
| `…YouVersion` | **exists** | §YVN |
| `…YouVersion.Tests.Unit` / `.Tests.Acceptance` / `.Tests.Integrations` | **exists** | §YVN20 |
| `Glory2Him.BibleProviders.Infrastructure` | **exists** | Pipeline-as-code (ADotNet). Generates `.github/workflows/build.yml` and `prLinter.yml`. **Not a library** — §SOL7 rule 6 |
| `Glory2Him.BibleProviders.Documentation` | **exists** | Surfaces these documents in the IDE. Compiles nothing |

**The reference graph is now in place.** Until it was, the two provider projects
referenced nothing at all and neither could implement `IBibleProvider` as written:

1. `…ApiBible` → `…Abstractions` ✔
2. `…YouVersion` → `…Abstractions` ✔
3. `…Abstractions` → `Microsoft.Extensions.Logging.Abstractions` ✔ (§SOL6).
   **Not `Xeption`** — §SOL17 rule 6

**What is deliberately *not* referenced yet**, and this list is exhaustive so that
§SOL6's table is never mistaken for a description of the repository:
`Microsoft.Extensions.Http` and `Microsoft.Extensions.Http.Resilience` land with
the transport work item (§APB25 item 2, §YVN21 item 2); `WireMock.Net` lands on
the three acceptance projects with their first test (§ABS35 rule 3); `Xeption`
lands on a test project with the first test that needs `SameExceptionAs()`
(§SOL17 rule 6); `AngleSharp` is later still and conditional (§YVN10 rule 6).

**§SOL6's table is the intended end state, not the current one.** Nothing there is
referenced until code uses it — a package sitting in a `.csproj` ahead of its
first caller is one nobody remembers the reason for.

**One dependency rule is structural rather than stylistic:**
`…ApiBible.Fums` references `…Abstractions` but **not** `…ApiBible` (§SOL2 rule 6).

---

## SOL6. Targets, dependencies and build settings (#1)

**TFM: `net10.0`, single-target. Settled — this is no longer an open question.**
Every project in the repository already targets `net10.0` and CI installs `10.x`.
Multi-targeting `netstandard2.1` would require internal polyfills for
`RequiredMemberAttribute`/`CompilerFeatureRequiredAttribute` and
`MemberNotNullWhenAttribute` plus a `System.Collections.Immutable` reference,
and nothing in this solution has a `netstandard2.1` consumer. Revisit only when
a real one appears.

**Dependencies, and they are deliberately minimal.** Each row is an architect
decision per `CLAUDE.md`; a package not in this table needs one before it is
added.

| Project | References | Why it is justified |
|---|---|---|
| `…Abstractions` | `Microsoft.Extensions.Logging.Abstractions` 10.0.12 — **the only reference** | `ILogger`/`ILogger<T>`/`NullLogger<T>` live in `Microsoft.Extensions.Logging.Abstractions`, and that package is the only sanctioned way to reach them here: `Microsoft.Extensions.Http` also surfaces them transitively, but taking them that way would put an HTTP stack in the contract package. The Standard's exception discipline is satisfied by the pattern, not by a base class, so `Xeption` is deliberately absent — rule 5. **Nothing else** — no HTTP, no caching, no JSON framework, and a transitive closure of two |
| `…Abstractions.Conformance` | `…Abstractions`, `xunit`, `FluentAssertions [7.2.2]` | Ships abstract xUnit classes; the test framework is unavoidably part of its surface. Opt-in, referenced only by test projects |
| Provider packages | `…Abstractions`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Resilience` | The typed client and the resilience pipeline are what make §ABS5 rule 8's budget real rather than decorative. `Microsoft.Extensions.DependencyInjection` arrives transitively and is what the internal container is built on |
| `…YouVersion` | additionally `AngleSharp` | Its upstream returns HTML, and `Blocks` cannot be derived from it by regex. **Conditionally justified** — §YVN10 now has a cheaper source for `Text`, which may reduce this to a `Blocks`-only dependency |
| `…ApiBible` | no HTML parser | Its upstream returns a JSON tree (`content-type=json`), so `System.Text.Json` from the framework is enough |
| `…ApiBible.Fums` | `…Abstractions`, `Microsoft.Extensions.Http` — **not** the provider | §SOL2 rule 6 |
| Test projects | `xunit` 2.9.3, `Moq`, `FluentAssertions [7.2.2]`, `Tynamix.ObjectFiller`, `DeepCloner`, `coverlet.collector`, and **`Xeption`** once a test needs `SameExceptionAs()` (§SOL17 rule 6) | Already the repository's convention. **`FluentAssertions` stays pinned to exactly `[7.2.2]`** — today because it is the last version under the old licence, and again the moment a test project takes `Xeption`, which hard-pins that exact version. Preferred now, forced then; either way do not unpin it |
| Acceptance test projects | additionally `WireMock.Net` | §ABS35 rule 3 |

**The `Xeption` trap.** The NuGet package id is **`Xeption`** (singular); the
namespace is `Xeptions` (plural). `<PackageReference Include="Xeptions" />` does
not resolve.

**Build settings already in force, inherited from the existing projects:**

1. `<Nullable>enable</Nullable>` on libraries, `disable` on test projects. The
   DTOs lean on `required` and `[MemberNotNullWhen]` (§ABS15), so the nullable
   context is part of the contract, not a style choice.
2. `<ImplicitUsings>disable</ImplicitUsings>` everywhere. Every file carries its
   own `using` block, per `the-standard-csharp-directives`. Code samples in these
   documents omit them for brevity; real files do not.
3. `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`, `Version 0.1.0.0`,
   G2HSL licence, icon and README packed from the repository root.
4. `<NoWarn>CS1998,CS8632</NoWarn>` on libraries. CS1998 (async without await) is
   expected in this codebase — broker methods and `TryCatch` shapes produce it
   routinely.

**And one thing the dependency table does not show, because it is transitive:**

5. **`Xeption` is not referenced by any shipped package. Settled — see §SOL17
   rule 6 for the decision and its reasoning.** Test projects reference it
   directly, where it is not published and costs a consumer nothing.

   The measured reason: `Xeption` 2.9.0 depends on `FluentAssertions [7.2.2]` and
   `DeepCloner`, and `FluentAssertions` pulls
   `System.Configuration.ConfigurationManager` → `System.Security.Permissions` →
   `System.Windows.Extensions` → `System.Drawing.Common` and
   `Microsoft.Win32.SystemEvents`. Referencing it took
   `Glory2Him.BibleProviders.Abstractions` **from 2 packages to 13**, putting a
   test assertion library and `System.Drawing.Common` into the dependency graph of
   the one package §SOL6 exists to keep referenceable from a domain layer.

   **`PrivateAssets` cannot fix this**, and that is worth knowing before someone
   tries: a dependency declared in `Xeption`'s *own* nuspec flows to our consumers
   no matter what our project file says. The only lever is whether we reference it
   at all.

**Two settings this design asks for and the repository does not have:**

6. **`Directory.Build.props`.** Every property above is currently copy-pasted
   into each `.csproj`, so a TFM bump or a new analyzer setting is an
   eleven-file edit and the eleventh is the one that gets missed. Hoist the
   shared block; leave per-package identity (`Title`, `Description`,
   `PackageProjectUrl`) in place.
7. **`TreatWarningsAsErrors` and analyzers on.** Neither is set today.

Both are scaffolding, and both belong in the same issue as the missing project
references in §SOL5.

---

## SOL7. Packaging and the release train (#1)

`.github/workflows/build.yml` is generated from
`Glory2Him.BibleProviders.Infrastructure`. **Edit the generator, never the YAML** —
a hand-edit is overwritten the next time anyone runs the tool.

The pipeline's shape imposes constraints this design has to live with:

1. **CI discovers test projects by glob** — `*Tests.Unit*.csproj` and
   `*Tests.Acceptance*.csproj`, recursively. Adding `…Abstractions.Conformance`
   and `…ApiBible.Fums.Tests.Unit` therefore needs **no** workflow change. This
   is the reason §SOL4 rule 2 keeps `Tests` out of the Conformance project's
   name: the glob is the discovery mechanism, so the name is the control.

2. **Integration tests never run in CI.** No glob matches `*Tests.Integrations*`.
   That is correct and deliberate — they need live credentials — but it means
   they are a **local and scheduled** guard only, and nothing stops one rotting.
   Each provider's integration suite is therefore specified to *skip* rather
   than fail without a key (§APB24, §YVN20), so it is honest when run and silent
   when not.

3. **One tag, one release, all packages.** The `add_tag` job reads `//Version`
   from `Glory2Him.BibleProviders.Abstractions.csproj` alone, and the `publish`
   job then pushes `**/bin/Release/**/*.nupkg` — every package the solution
   produced, each at whatever version its own `.csproj` says.

   **Consequence, and it is a real one:** the shipped packages form a single
   release train whose tag is named after Abstractions' version. If `…ApiBible`
   is bumped and `…Abstractions` is not, the tag and release notes describe the
   wrong thing while the package still publishes. **Version the shipped packages
   in lockstep** and treat Abstractions' version as the solution's version. A
   per-package release cadence would need a different workflow, and is not worth
   building for four packages.

   Releases are gated on a `RELEASES:` PR title prefix plus the `RELEASES`
   label. `PROVIDERS` is the label for the work in these documents.

4. **Semantic versioning.** The contract in Abstractions is a published API. A
   change to `IBibleProvider`, the marker interfaces, or the
   `ScriptureResult`/`ScripturePassage`/`ScriptureUsage` shape is a major-version
   change (§SOL2 rule 7).

5. **Publishing hygiene:** SourceLink and symbol packages, so a consumer can step
   into the code while debugging. `--include-symbols` is already on the pack step.

### Release-path rules, each fixed once and easy to undo

All three were live defects when this document was written, and all three are
fixed. They stay here as **rules** rather than as history, because each was a
single line — present or absent — that nothing in the build will complain about if
it regresses.

6. **Every project that does not ship carries `<IsPackable>false</IsPackable>`.**
   `dotnet pack` at solution scope packs every packable project, and the push step
   globs `**/*.nupkg` with `--skip-duplicate`. Without it,
   `Glory2Him.BibleProviders.Infrastructure` (the build tool) and
   `Glory2Him.BibleProviders.Documentation` (the docs shell) would both have been
   published to nuget.org on the first release, under the
   `Glory2Him.BibleProviders.*` name space, permanently. The test projects set it
   explicitly and always did.

   **A published package id cannot be withdrawn**, so this is checked when a
   project is added, not when a release is cut. A new non-shipping project without
   it is a release-blocking defect.

7. **Every PowerShell step in a generated workflow carries `Shell = "pwsh"`.** The
   "Run Acceptance Tests" `TestTask` in
   `ScriptGenerationService.GenerateBuildScript` did not, so a PowerShell script
   was handed to `bash` on `ubuntu-latest` and the acceptance gate — the suite
   §ABS37, §APB24 and §YVN20 put the most weight on — was not running as intended.
   The unit-test task next to it was correct, which is exactly why the omission
   survived review.

   **The fix belongs in the generator, and the YAML is regenerated from it**
   (§SOL7 preamble). A hand-edit to `build.yml` would be silently reverted by the
   next person who runs the tool.

8. **No step assumes a Windows runner while `RunsOn` is `UbuntuLatest`.** The
   generated build began with `git config --system core.longpaths true`, which
   failed the job at its first step: `core.longpaths` is a Windows-only Git setting
   for the `MAX_PATH` limit, Git on Linux ignores it, and `--system` cannot write
   `/etc/gitconfig` without root.

   **It had never fired before.** The `Build` workflow had never run on any branch —
   `main` had only ever run `Labels` — so a workflow that could not reach its second
   step sat green-by-absence in the repository from the day it was generated. That is
   the more useful lesson than the fix: **a required check that has never executed is
   not a passing check**, and the first PR to trigger one is doing the repository a
   favour rather than breaking it.

   Restore the step only alongside a Windows runner; the generator carries a comment
   saying so.

9. **A `pwsh` loop over test projects must check `$LASTEXITCODE` after every
   `dotnet test`.** A PowerShell step exits with its *last* command's code, so a
   loop without the guard reports success whenever the final project passes — a
   failing project in the middle leaves the required `Build` check green. The
   generated loops now carry `if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }`.

   This is the same class of defect as rules 7 and 8 and the third instance of it:
   **a check that cannot fail is not a check.** Rule 7 stopped the acceptance step
   running at all, rule 8 stopped the job reaching its second step, and this one let
   it pass while tests failed. All three were invisible because the check was green.

10. **This branch's workflows are generated; `main`'s are hand-maintained
    template files, and they are not the same.** `main` has no
    `Glory2Him.BibleProviders.Infrastructure` project — the ADotNet generator and
    every project in the solution arrive with the unpushed `INFRA: Project Setup`
    commit that this branch carries (§SOL17 rule 7). Consequences to settle before
    merge, not after: the generated `build.yml` runs on `ubuntu-latest` where the
    template's runs on `windows-latest`, drops the template's "Detect What Is Here"
    step, adds `Tag and Release` and `Publish to NuGet`, and the generated
    `prLinter.yml` omits the template's `setAuthorAsPrAssignee` job.

    **Whichever wins, one of the two has to stop existing.** A repository that
    hand-edits `.github/workflows` *and* regenerates them from a checked-in
    generator will silently revert whichever was edited last, and the loser is
    always the person who did not know the other existed.

---

## SOL8. Layering rules that bind every package (#1)

Restating the two `CLAUDE.md` non-negotiables that this design's structure
depends on, because both shape the test plan as much as the code:

1. **Brokers hold no logic and get no unit tests.** The HTTP broker issues a
   request and hands back a response. Every decision — which endpoint, what the
   status means, how to map content — is the foundation service's. The
   consequence for testing is direct: broker behaviour is proven **only** in
   acceptance tests against a `WireMockServer`, never by a unit test with a
   stubbed `HttpMessageHandler` (§ABS35 rule 3).
2. **No layer calls two layers below it.** The provider façade calls its
   foundation service; the foundation service calls its broker. The façade never
   touches the broker, and nothing touches `HttpClient` outside the broker.

---

## SOL9. Test-first is not negotiable here either (#1)

`CLAUDE.md`: no production code without a failing test that demanded it,
committed as `{TestName} -> FAIL` before the implementation.

For this solution that has one specific consequence worth writing down, because
it is where the discipline usually breaks: **content mapping is the hardest thing
here to write a failing test for first, and it is also where the fixtures come
from.** Each provider's spike (§APB23, §YVN19) captures real upstream payloads
and commits them. The spike is therefore not optional and not a "research task
we can skip if we are confident" — it is the step that makes test-first possible
for the lookup and mapping items of both providers' breakdowns.

---

## SOL10. How a consuming application takes a dependency (#1)

**Register the abstraction in the composition root** (the wiring sample and its
three traps are in §ABS28), then **wrap it in the application's own broker**
rather than injecting `IBibleAbstractionProvider` into services directly:

```csharp
// In the consuming application, not in these packages.
public interface IBibleBroker
{
    IReadOnlyCollection<IBibleProvider> BibleProviders { get; }

    Task<ScriptureResult> GetScriptureByUsfmAsync(
        string providerName, string usfm, CancellationToken cancellationToken = default);

    Task<ScriptureResult> GetScriptureByReferenceAsync(
        string providerName, string reference, CancellationToken cancellationToken = default);
}
```

Registered as `services.AddTransient<IBibleBroker, BibleBroker>();` alongside the
application's other brokers, forwarding each call verbatim. The broker is the
application's own boundary type: its services depend on `IBibleBroker`, not on
this solution's types, so swapping or wrapping the library later touches one
class. It holds no logic, which is what keeps it a broker.

**Where each concern sits:**

| Concern | Layer, in the consuming application |
|---|---|
| Talking to this library | Its own **Broker** |
| Which provider to ask, in what order, and what to do on a miss | An **Orchestration Service** above it |
| Persisting the result, and refreshing it on the licence cycle | Its storage layer |

Keeping selection policy out of the library is deliberate: which providers exist,
in what order, under what subscription, is an application concern that changes
independently of the provider contracts. The decision table and a worked fallback
loop are §ABS34.

**On naming a provider at every call site.** That is the deliberate price of the
pattern — the abstraction makes no choice for you. An application wanting a
single default holds that provider name in **one place** (its own configuration,
or its orchestration service), not spread through its UI and service code.

---

## SOL11. Storing a passage (#1)

`ScripturePassage` carries three renditions. Which to persist is a decision, and
the recommended shape is:

1. **Plain text (`Text`) is the canonical stored value** — searchable, safe
   everywhere, and never empty on a `Found` result (§ABS16).
2. **The rich rendition (`Html`) goes in a separate, nullable field.** Null means
   the UI falls back to the plain text, which covers non-red-letter editions and
   providers whose markup could not be extracted.

   **Store `Markup.ToStorageString()` beside it, non-nullable** (§ABS43). That turns
   the old blanket "sanitize on write" into a rule with a decision in it: render
   `Html` directly when the stored value parses to `Generated` — this library built
   that markup itself from a closed model with every text node escaped — and
   sanitize in every other case, including a blank or unparseable value, which reads
   as `Unknown` and is provably a mapping bug rather than an absence.
3. **Do not persist `Blocks`.** Deliberate, not an omission: keeping the stored
   shape to the two derived renditions means adding a block kind or an inline
   flag later is a code change, not a data migration.
4. Alongside the text, persist `Usage.ToStorageString()` in a **non-nullable**
   field (§ABS29) and, where the provider supplies one, `Usage.ProviderEditionId`.
5. Persist `Translation` and `Usfm` together and treat the pair as the identity
   of the row. `Usfm` alone does not identify a verse (§ABS17).
6. **Persist `Language` and `ScriptDirection`.** A stored `Html` for a Hebrew or
   Arabic edition renders backwards without the direction, and nothing errors
   (§ABS42.6). They are cheap columns and unrecoverable from `Text` later.
7. **Persist whatever is needed to link back to the upstream's copyright page.**
   API.Bible's terms require more than a copyright string — see §APB19, which is
   also the one place this design knows the DTO may be a field short.

---

## SOL12. Request cost — size this before you ship (#1)

**Every lookup is one live upstream request**, plus at most one catalogue request
per `CatalogueCacheDuration`. There is no passage cache in this library (§SOL13).

Against API.Bible's **5,000 requests/month** Starter tier [verified] that is
roughly **165 lookups a day**. Overage is billed in $1 increments of 1,000 calls,
and **plans default to no overage protection — service is disrupted rather than
billed** [verified]. That last clause matters more than the number: exhausting
the plan is an *outage*, and §APB15 has to classify it as one.

YouVersion's cost is lower than an earlier reading of its documentation
suggested, because a chapter-level endpoint exists (§YVN9) — but a verse range
may still cost more than one request.

So caching or persistence in the consuming application is **not optional**. This
document does not prescribe how — only that the decision has to be made, and made
before launch rather than after the first quota exhaustion. The upstreams' own
retention rules (§APB17, §YVN14) bound what that cache may be.

---

## SOL13. What this library deliberately does not do (#1)

| Not done here | Why |
|---|---|
| Passage caching, persistence, retention | Storage and its licence-bounded retention rules belong to whoever owns the database and the agreement. §SOL12 states the cost of that choice |
| **Request counters, metrics, telemetry hooks** | Both upstreams meter usage and publish their own dashboard; a counter here would be a second, lagging, disagreeing copy of a number they already own. The exception channel carries what a dashboard cannot — the moment, the condition, the duration, and `ProviderConsole` to point at the authority. §ABS7.1 |
| Usage reporting | Requires viewer identity a fetch does not have — §ABS30, §APB16 |
| Cross-provider fallback | An application concern — §SOL10, §ABS34 |
| Cross-edition versification mapping | A USFM key is edition-relative; mapping between editions needs the Copenhagen Alliance tables and is out of scope — §ABS17 |
| Audio Bibles | Both upstreams expose them; nothing in the contract models audio. API.Bible's FUMS even has a `trackListen` verb [verified] this design does not use |
| Footnotes and cross-references | §ABS39 rule 3 |
| Multi-part references (`"John 3:16; Rom 8:28"`) | One call, one passage — §ABS19 |
| **Silently correcting a mistyped reference** | `Jn`/`Jon` and `Jas`/`Jos` are one character apart and are *different books*, so auto-correction can serve the wrong scripture under correct-looking attribution. The library normalizes freely and **suggests** on request, but never rewrites what was asked for — §ABS41 |
| Verse of the Day | YouVersion exposes `/v1/verse_of_the_days/{day}` [verified]. It is a curated feed, not a reference lookup, and has no equivalent on the other upstream |

---

## SOL14. Logging (#1)

1. **Debug** for each provider call, scoped with `{Provider}` and `{Usfm}`. The
   baseline for the normal path.
2. **Warning** for: a served-stale catalogue, a truncated passage, a transient
   rate-limit 429, a missing usage token on a `Found` result, a null `Attribution`
   on a licensed edition, and a stitched range that cost more than one upstream
   request.
3. **Error** for: a rejected or revoked key, a provider that threw rather than
   answered, a configured default translation absent from the resolved catalogue,
   and **an exhausted quota**.

   *An earlier draft logged a quota-shaped 429 at Warning. That was wrong and is
   corrected here: §SOL12 establishes that past the allowance API.Bible disrupts
   service rather than billing, so quota exhaustion is the provider going dark for
   the rest of the billing period — an outage, not a hiccup. A rate-limit 429 with a
   short `Retry-After` stays a Warning; those are different conditions and §APB15
   is where they are told apart.*

4. **Every one of those Error lines carries `ProviderConsole`** from the exception
   (§ABS7.1), so the alert says where to go rather than only what broke.
5. **Never log API keys or app keys.** Not in a request dump, not in an exception
   message, not in a `BaseUrl` that had a key interpolated into it. Each
   provider's acceptance suite asserts the key value appears in no captured log
   (§APB24, §YVN20) — an assertion, not a review item, because this is the
   failure that is invisible until a log ships somewhere it should not.

---

## SOL15. Work breakdown (#1)

Each document carries its own breakdown. Every provider item depends on the
abstraction items.

| Package | Estimate | Detail |
|---|---|---|
| Scaffolding gaps (§SOL6) | 0.25 d | **Done:** project references, `IsPackable=false`, the `pwsh` fix, the ubuntu long-paths removal. **Remaining:** `Directory.Build.props`, `TreatWarningsAsErrors` + analyzers, and `WireMock.Net` on the three acceptance projects |
| Abstractions | 8.5–12 d | §ABS40 |
| API.Bible | 6.5–9 d | §APB25 |
| YouVersion | 5–7 d | §YVN21 |
| **Solution total** | **≈ 20.5–28.5 dev-days** | |

**Cross-package sequencing:**

1. The scaffolding gaps first. Nothing compiles against the contract until
   `…ApiBible` and `…YouVersion` reference `…Abstractions`.
2. Abstraction items 1–3 (models, `UsfmReference`, `LooseReferenceParser`) next —
   everything depends on them.
3. `ScriptureHtmlRenderer` before `BibleProviderBase`, and both before either
   provider: providers render `Html`/`Text` through the renderer, and their
   acceptance assertions cannot pass without it.
4. The abstraction provider and its exception hierarchy are independent of the
   renderer and base class, so they can run in parallel.
5. **Each provider's spikes are its first item and cannot be skipped** — they
   produce the fixtures that provider's acceptance suite is built on (§SOL9), and
   for YouVersion three of them change what gets built.
6. The two providers are independent of each other.

**On the estimate.** It is higher than a first pass suggests, and the reason is
The Standard's test convention priced honestly: a root partial plus `.Logic`,
`.Validations` and `.Exceptions` files per method, for every service. A
foundation service with a single method still carries four test files. That is
the convention this repository adopts, so it is the convention the estimate
reflects.

---

## SOL16. Evidence and what is still unsettled (#1)

This design was checked against both upstreams' published documentation. What
that changed is recorded in the provider documents at the point it bites; the
summary of what remains **unsettled** is here, because these are the items that
can still change what gets built.

| # | Unsettled | Where | Consequence if wrong |
|---|---|---|---|
| 1 | Does API.Bible signal an exhausted plan as 429, 403, or something else? | §APB15 | A 403 is currently mapped to `TranslationNotSupported`, which is *returned*. An exhausted plan arriving as 403 would be read as "this translation isn't here", and a consumer would fail over silently and permanently instead of suspending the provider. **The most dangerous unknown in this design** |
| 2 | YouVersion: `language_ranges[]` with brackets, or `language_ranges` comma-separated? | §YVN7 | Two upstream pages disagree [contested]. Wrong answer ⇒ 422 on every catalogue call ⇒ every lookup fails |
| 3 | YouVersion: `page_token` or `next_page_token` as the request parameter? | §YVN7 | Two upstream pages disagree [contested]. Wrong answer ⇒ silent single-page catalogue ⇒ licensed translations report as unsupported |
| 4 | YouVersion platform terms — still unread | §YVN14 | Blocks persistence entirely |
| 5 | Do critical-text omitted verses return 200-with-empty, 204, or 404? | §APB9, §YVN11 | Decides whether the content check is a safety net or the primary mechanism |
| 6 | Is a YouVersion passage fetchable for a Bible absent from the catalogue? | §YVN7 | If yes, a catalogue miss is not a sound basis for `TranslationNotSupported` |

---

## SOL17. Open questions (#1)

Package-specific questions live in each document. These span the solution and
are **decisions, not spikes** — no amount of upstream research settles them.

1. **`INTENT.md` does not exist.** `CLAUDE.md` and `DEVELOPERS.md` both treat it
   as the statement of what this system is for, and both these design documents
   and the analyst's criteria are supposed to trace to it. Write it, or accept
   that §SOL1 is doing its job by default.

2. ~~**Files still pointing at the deleted `Documentation/Design.md`.**~~
   **Done.** All 30 references across `CLAUDE.md`, `DEVELOPERS.md`, `README.md`,
   the three agent prompts and the Mockups README now point at
   `Documentation/Design/`, and both sweep commands were narrowed to
   `^## .*(needs issue)` over `Documentation/Design/*.md`. The statements the split
   made false — the "ships as a stub" language, the "starts as a single file"
   narrative, and the architect's own path boundary, which now tightens to
   `Documentation/Design/` exactly as it anticipated — were corrected rather than
   merely repointed.

   Kept as a numbered rule rather than deleted, because what it records is general
   and will recur: **a relocated rule goes stale, and nothing validates a citation.**

3. **Where does the consuming application's translation list come from?** Each
   provider's catalogue is private and the only way to learn a translation is
   unavailable is to spend a metered request. Either the list is
   application-owned configuration — in which case each provider's
   `TranslationMap` is authoritative and the live catalogue merely validates it —
   or `IBibleProvider` grows a read-only `KnownTranslations` snapshot.
   **Decide before the first provider ships**: a small addition now, a breaking
   change later (§SOL2 rule 7).

4. **Is a ~20-second worst-case lookup acceptable?** Both providers ship a 20 s
   overall / 5 s per-attempt / 2-retry budget — the smallest that closes
   arithmetically while leaving room for a real retry. An interactive page may
   prefer to fail faster (`MaxRetryAttempts = 1`, `TimeoutSeconds = 12`, which
   also closes). Because these are per-provider POCO values, an interactive
   surface and a background import can be configured differently — so the
   question is really *which surface calls this*.

5. **A third provider** — construct it and add it to the list; there is no
   registry to extend. Its obligations are §ABS5's rules, the §ABS7 markers, a
   reserved prefix in this file's header table, and a
   `Documentation/Design/<Provider>.md` meeting §ABS33.

6. ~~**Do we accept `Xeption`'s transitive closure on a published package?**~~
   **Settled: no. Shipped packages do not reference `Xeption`; test projects do.**

   Every version checked — 2.5, 2.6, 2.8, 2.9 — pins `FluentAssertions = 7.2.2`,
   so there is no lean release to move to, and `PrivateAssets` cannot strip a
   dependency another package declares (§SOL6 rule 5). That left two options, and
   the measured cost decided it: referencing `Xeption` takes Abstractions from
   **2 packages to 13**.

   **What is given up, stated plainly, because this is a deliberate divergence
   from `the-standard-exceptions`:** exception types derive from `System.Exception`
   rather than `Xeption`, so they lose its `Data` helpers. Nothing else. The
   discipline the skill actually exists to enforce — the Validation / Dependency /
   Service split, `TryCatch` categorization, preserving the original as
   `innerException`, lifting actionable detail onto typed properties — is
   untouched, because none of it lives in the base class.

   **What is kept:** test projects reference `Xeption` directly for
   `SameExceptionAs()` (§ABS35). They are never published, so a consumer's graph
   never sees it.

   **Why this was safe to do:** §ABS7 already made the marker interfaces the entire
   public exception vocabulary, and §ABS7's own rule is that consumers catch markers
   and never concrete types. No consumer was ever meant to name `Xeption`, so
   removing it changes no contract a consumer could depend on. `CLAUDE.md` provides
   for exactly this — a skill that is wrong for this repository is argued in the
   design, not patched quietly — and the argument is that the skill prices a base
   class for an *application*, where a transitive test dependency costs nothing, and
   this is a *published library*, where it is inherited by everyone downstream.

---

7. **`INFRA: Project Setup` is in this branch and not on `main`.** `main` is
   still the bare template — **zero `.csproj` files** — and its history is
   `Initial commit` → `DOCUMENTATION: Bring The Template Files Up To Date` →
   `INFRA: Carry The Whole Resources Folder`. The commit that created all fourteen
   projects, the `.slnx`, the ADotNet generator and the generated workflows was
   never pushed, so it rides along inside the design PR and makes that PR look like
   it invents the whole solution.

   **This is a process decision, not a design one**, and it needs a human: either
   the scaffold lands on `main` on its own first and the design PR is re-targeted
   at what remains, or the PR is accepted as "scaffold + design" and its title and
   body say so. Nothing in the code settles it. Until it is settled, §SOL7 rule 10's
   workflow divergence has no correct answer either, because the two questions are
   the same question.

---

## SOL18. Reference links (#1)

- **`Xeption`** (package id singular, namespace `Xeptions`):
  https://www.nuget.org/packages/Xeption
- **USFM verse-id semantics:** https://docs.api.bible/resources/referencing-verses/
- **Copenhagen Alliance versification tables:**
  https://github.com/Copenhagen-Alliance/versification-specification
- Upstream documentation for each provider is listed in that provider's document
  (§APB1, §YVN1).
