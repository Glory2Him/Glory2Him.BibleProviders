# Usage permission — what may be stored, and what may be passed on

**Area prefix:** `USE` · **Sections:** §USE1 – §USE10
**Solution overview:** [Design.md](Design.md) · **Providers:** [ApiBible.md](ApiBible.md) · [YouVersion.md](YouVersion.md)

Conventions, heading tags and provenance tags: [Design.md](Design.md), "Conventions".

This file answers two questions the other documents scatter: **how long may a
consumer keep scripture**, and **may a consumer pass it on** — the share-to-WhatsApp
button, the verse-of-the-day email, the quote card posted to X.

It is a consumer-facing reading of the licences, not a statement about this
library, which stores nothing and transmits nothing (§SOL2 rule 5).

---

## USE1. The short answer (#3)

| | May a consumer **store** it? | May a consumer **pass it on** outside their app? |
|---|---|---|
| **API.Bible** — public domain, CC BY, CC BY-SA | Yes, refreshed on cycle | **Yes** — §USE6. **Except the KJV**, which is territorially restricted and may not be transmitted at all (§USE11) |
| **API.Bible** — any other translation (NIV, ESV, NLT…) | Yes, refreshed on cycle | **No**, unless the rights holder expressly authorised it |
| **YouVersion** — the Public Domain & Creative Commons set (361) | Yes | **Governed by each work's own PD/CC licence**, not by an agreement — §USE6 |
| **YouVersion** — any of the nine publisher agreements (1,125) | Yes | **No** — licensed for display *in your application* |

**The pattern is the same on both upstreams and worth stating once:** publishers
licensed *display inside your application*. Storage supports that. Transmission
out of it is a separate right that was not granted, and on API.Bible it is refused
in terms.

---

## USE2. Storage — API.Bible (#3)

All [verified] from the Terms and the common-questions page (§APB17, §APB18).

| Question | Answer |
|---|---|
| May scripture be stored? | **Yes.** The Terms assume it: "If you store API.Bible Content… you must keep it up to date" |
| For how long? | **Indefinitely, while kept current.** There is no expiry — there is a staleness ceiling |
| Staleness ceiling | **Check at least every 30 days** (Terms §11). ABS separately recommends clearing cache **every 14 days or less** — stricter, and satisfies both |
| How much? | **Fewer than 500 consecutive verses** is requested. The 200-verse cap on one passage keeps a single request inside it; stitching adjacent passages into a stored book does not |
| Must anything be deleted? | **Yes.** "You must delete or modify any content you have if it is deleted or modified in API.Bible" (§11). Refreshing is not enough — withdrawn content must disappear |
| On termination? | **72 hours** to remove everything, on licence termination, suspension, or a terminated *or deactivated* subscription. **An unpaid plan counts as deactivated** (§10.2) |
| On request? | **72 hours**, from ABS or the rights holder (§10.3) |
| Urgent corrections? | **24 hours** to apply an update once requested (§11) |

**Two of these need a mechanism a refresh loop does not provide** — the deletion
duty and the 72-hour purge. Design the delete path before the first row is
written; nothing in normal operation exercises it.

---

## USE3. Storage — YouVersion (#3)

All [verified] from the nine publisher agreements (§YVN14.5, §YVN14.9).

| Question | Answer |
|---|---|
| May scripture be stored? | **Yes, explicitly.** The common grant covers the right to "perform, **store**, distribute, and redistribute the Content on Your Application", and permits sublicensing to users "both online and **offline**" |
| For how long? | **For the term of the agreement.** No staleness ceiling and no refresh cadence |
| Refresh duty | **Update on request**, not on a timer. Biblica's Section VI obliges updates "as may be requested by LICENSOR"; the common template requires no edits and a duty to *notify* the publisher if text looks wrong |
| How much? | No storage cap. **But Biblica caps *display* at two chapters or twenty-five verses, whichever is greater, per user at any given time** — a display limit, not a storage one, and the tightest constraint in this design |
| On termination? | Rights cease. Biblica's term is **two years, auto-renewing**; Lockman's ends on **thirty days' notice** from Lockman *or YouVersion* |
| Anything else? | Industry-standard **encryption** against unauthorised supply, onward-supply or reproduction; **all footnotes** displayed and accessible (§YVN14.6) |

**So the two upstreams differ in kind.** API.Bible bounds *staleness* and demands
a timer; YouVersion bounds *the relationship* and demands responsiveness. A
consumer serving both needs a forced-refresh path for either, and a 30-day sweep
for API.Bible only.

---

## USE4. Passing it on — API.Bible (#3)

**This is the question the design had never asked, and the Terms answer it
directly.** All [verified].

> **§9.9(a)** — content may be transmitted electronically (email, SMS, messaging)
> only where it is **Public Domain, CC BY, or CC BY-SA**, or where the IP Holder has
> expressly authorised such transmission. "No other API Content or Third Party IP
> Content may be so transmitted, **including content under any Creative Commons
> license with a NonCommercial (NC) or NoDerivatives (ND) element**."

So a share-to-WhatsApp button:

| Translation | Share button |
|---|---|
| ASV, WEB, BSB and other public domain | **Permitted** |
| **KJV** | **Not permitted** — §9.9(b)(i) excludes territorially restricted content "irrespective of identification as Public Domain" (§USE11) |
| CC BY or CC BY-SA editions | **Permitted** |
| CC BY-**NC** or CC BY-**ND** editions | **Not permitted** — the NC and ND elements are named as exclusions |
| NIV, ESV, NLT, CSB and other licensed | **Not permitted** without the rights holder's express authorisation |

Reinforced elsewhere: **§4.2** forbids sublicensing or distributing content to any
third party without written approval, and **§9.5(d)** forbids providing third-party
access to it.

**And §12 goes further than permission — it requires prevention.** Read in full at
§APB26: a consumer "will incorporate industry-standard digital rights management
… which **restricts end users from copying or distributing** the Licensed Products
and the Property", and may only use the content "in a secured manner that **does
not allow the property to be freely copied**".

**That is a different kind of obligation from the rest of this file.** Everywhere
else the question is whether a consumer *may* do something. Here a consumer must
**build something to stop its own users doing it** — which makes a share button, a
copy-verse button, and arguably freely selectable text all problems for licensed
translations, rather than merely unpermitted features. The duty is
"commercially reasonable efforts", not perfection (§APB26.2), which is the clause
to lean on: a web page cannot truly prevent copying and §12 does not pretend
otherwise.

§12 also caps printing at **100 verses**, restricts use to a **Territory**, and
caps **device count** — the latter two being parameters fixed at sign-up that
nothing in this design has ever seen (§APB26.3).

---

## USE5. Passing it on — YouVersion (#3)

No clause as explicit as §9.9(a), but the common agreement forecloses it by
scope. All [verified].

1. **The grant is bounded to your application.** The right is to store and
   distribute "the Content **on Your Application** via the Developer Tools", and to
   sublicense to users "**through Your Application**".
2. **Other uses are excluded by name.** "You may **not use the Content for any
   purpose other than digital display in Your Application** via the Developer
   Tools." A message composed in your app and delivered by WhatsApp is displayed in
   WhatsApp.
3. **Onward-supply is the thing the encryption clause exists to prevent** —
   measures "to prevent the unauthorized supply, **onward-supply**, or reproduction"
   of the content.
4. **Biblica is more explicit still.** Its Electronic Rights "do not include the
   **transfer or download** of any media files of the CONTENT or any portion
   thereof, **to any end user or third party**".
5. **Lockman requires the opposite of a share feature** — display must be arranged
   "as to make the downloading of a large portion or the entire UNDERLYING WORKS
   difficult or impractical for use **without REQUESTER's website or application**".

**Reading: a share-out feature is not permitted for the 1,125 Bibles under the
nine publisher agreements.** Marked **[unverified]** as a *conclusion* rather than
a quotation — no clause says "you may not share to social media", and this is
inference from the scope of the grant. **If a share feature matters commercially,
ask YouVersion rather than rely on this paragraph.**

---

## USE6. Public domain and permissively licensed works (#3)

**The instinct this section exists to test is right, and it is right for a reason
worth stating precisely: for a public-domain work, copyright restrains nobody.**
Caching it, storing it indefinitely, printing it, and sending it on by email or
WhatsApp are all permitted *by the work*.

But that is only one of two questions, and the design has to answer both:

1. **Does copyright restrain you?** A property of **the work**. For a
   public-domain work the answer is no.
2. **Does your agreement with the API operator restrain you?** A property of
   **how you obtained it**. That answer is not no, and it is not the same on the
   two upstreams.

**A public-domain text fetched from an API arrives wrapped in a contract.** The
text is free; the pipe is not. Everything below turns on keeping those apart.

---

### USE6.1 What public domain settles (#3)

Take the World English Bible as the worked example, because its dedication is
unusually explicit [verified,
[ebible.org](https://ebible.org/engwebp/copyright.htm)]:

> "The World English Bible is in the Public Domain. That means that it is not
> copyrighted. … You may copy, publish, proclaim, distribute, redistribute, sell,
> give away, quote, memorize, read publicly, broadcast, transmit, share, back up,
> post on the Internet, print, reproduce, preach, teach from, and use the World
> English Bible as much as you want, and others may also do so."

That list is close to exhaustive of what this design ever asks about. **Store,
share, transmit, print, and use commercially — all yes**, from the work's side.

Two riders, both real:

- **The name is a trademark.** eBible.org asks that a *changed* text not be called
  the World English Bible. Since §ABS17 forbids altering a single character, a
  conforming consumer cannot trip this — but a consumer that "modernises"
  punctuation downstream can, and would then be misattributing as well.
- **Public domain is territorial, not global.** A work is out of copyright *in a
  jurisdiction*. The KJV is the notorious case and has its own section (§USE11) —
  public domain in the United States, and **Crown copyright in perpetuity in the
  United Kingdom**.

---

### USE6.2 What API.Bible's Terms still require for public-domain content (#3)

**This is the part a reader is most likely to get wrong**, because "it is public
domain" feels like it should end the conversation. It ends the *copyright*
conversation. The Terms are a separate contract with American Bible Society, and
their duties are written against **"API Content"**, which is defined to
**include** public-domain content [verified, §2]:

> "'API content' … means the data, information, text, audio and other content
> provided through the API.Bible API, **including but not limited to** biblical
> texts, **public domain content**, creative commons content, licensed content…"

So the default is that a duty applies unless it carves public domain out. Only
two do.

| Terms duty | Applies to public domain? |
|---|---|
| **§7** copyright page and IP-holder link | **No** — "excluding explicitly labeled Public Domain content" [verified] |
| **§12** DRM restricting users from copying or distributing | **No**, for permitted transmission — §USE6.3 |
| **§4.4** review the licensing metadata before use | **Yes** — names "Public Domain" expressly [verified] |
| **§11** keep stored content no more than 30 days out of date | **Yes** — no carve-out |
| **§11** delete or modify content withdrawn or changed upstream | **Yes** — no carve-out |
| **§10** remove everything within **72 hours** of termination, or of a deactivated (including unpaid) plan | **Yes** |
| **§13** delete within **24 hours** of a written request, including where content "**gains protected status**" | **Yes** |
| **§14 / §3** FUMS reporting from a webapp | **Yes** — no carve-out anywhere in §14 |
| **§9.2** commercial-use bar on a plan designated non-commercial | **Yes** — a plan term, not a copyright term |
| **§9.1** text-to-speech | **Permitted** for PD — ephemeral, **one chapter at a time**, no download [verified] |
| **§9.4(d)** no sublicensing, redistributing or syndicating API Content | **Yes** |
| **§9.6** one free-tier account per entity | **Yes** |

**Four of those deserve emphasis, because they are the ones that surprise.**

1. **FUMS is owed on a public-domain verse.** It is not a copyright mechanism —
   §14 says it exists so ABS can "communicate the value of API-accessible
   Scripture texts back to copyright holders and publishers", and §3 requires it
   of "any webapp … unless otherwise prohibited by law". Nothing conditions it on
   the rights class of what was fetched. §APB14's position is unchanged by this
   section.
2. **The 30-day recency duty survives transmission.** §11 closes with: "For
   content transmitted through Electronic Correspondence as defined in Section
   9.9, this requirement is measured **as of the time of transmission**"
   [verified]. The obligation on a sent verse is therefore satisfiable — the text
   must have been fresh when it left, not for ever afterwards. **That is the
   clause that makes a share button workable at all**, and it is worth knowing it
   exists before someone concludes a sent message must somehow be recalled.
3. **The deletion duties bind public-domain content.** §13's "gains protected
   status" is the reason: a work labelled public domain upstream can stop being
   labelled that. The delete path §USE2 asks for is not a licensed-translation
   feature.
4. **The copyright-page exemption is not an attribution exemption.** §7 exempts PD
   content from the *full copyright page*. It does not make attribution
   pointless — §ABS16 still delivers `Attribution`, and a WEB or BSB passage
   carries a dedication notice worth showing. Dropping it becomes a choice rather
   than a breach.

---

### USE6.3 The DRM clause does not bite public-domain content (#3)

§USE4 records §12 as requiring a consumer to "incorporate industry-standard
digital rights management … which restricts end users from copying or
distributing the Licensed Products and the Property". Read alone, that flatly
contradicts §9.9(a)'s permission to transmit public-domain content: you cannot
both be allowed to email a verse and be required to stop your users copying it.

**The Terms resolve it themselves, and the resolving clause had not been read.**
§9.9(c), in full [verified]:

> "**Section 12 does not apply to transmission permitted under Section 9.9.a**, to
> the extent of that transmission only."

Two further supports point the same way:

- **§2 excludes public-domain works from "IP"**: "Bible versions and bible
  content, **unless clearly marked as Public Domain**, are considered Intellectual
  Property of the creator, steward, manager or owner." §12's DRM sentence binds
  "the Licensed Products and **the Property**" — terms that do not reach a work §2
  has just said is not IP.
- **§7's carve-out** shows the drafters carve public domain out explicitly when
  they mean to.

**But §12 does not vanish for public-domain content — only its DRM sentence, and
only so far as the permitted transmission goes.** §12's first half is about *your*
security, not your users' freedom, and binds "API.Bible Content", which includes
public domain. So these still apply to a WEB deployment [verified]:

- Never expose the API key to a third party (§SOL14 rule 5 already requires this).
- Industry-standard safeguards against unauthorised access to the API.
- Keep API.Bible Content "confidential and secure from unauthorized access … with
  no less care than you use in connection with securing similar data".
- **Notify support@api.bible immediately** on any suspected breach.

**The practical answer: for public-domain translations you may ship a copy button,
selectable text and a share sheet.** For licensed ones you may not, and §USE4
stands unchanged there.

---

### USE6.4 YouVersion's public-domain set (#3)

YouVersion's largest licence row — **361 Bibles under "Public Domain and Creative
Commons"** — has **no agreement document to show** [verified]. That is consistent:
there is no licensor to agree with.

**Those works are governed by their own public-domain status or Creative Commons
licence, not by anything in this design.** A CC BY-SA work carries share-alike
obligations that follow the text wherever it goes; a CC BY-NC work cannot be used
commercially whatever any platform says. **Check the individual work**, and note
that the same NC/ND exclusions API.Bible names in §9.9(a) apply by the licences'
own terms.

**What still binds, whatever the work's licence**, is the platform agreement — and
it is a thinner instrument than API.Bible's:

| YouVersion duty | Applies to the PD/CC set? |
|---|---|
| Reproduce the text word-for-word, unaltered | **Yes** — a platform term (§YVN14.2) |
| Keep the app key confidential; report its loss | **Yes** |
| No use with AI for open-ended chat; no AI training | **Yes** — a platform term (§YVN14.2) |
| Do not brand the content as YouVersion's | **Yes** (§YVN14.2) |
| Display all footnotes; Biblica's 2-chapter display cap; Lockman's annual report | **No** — publisher terms, and this row has no publisher |
| Any refresh cadence | **None exists** on this upstream (§USE3) |

**The asymmetry runs the opposite way to what you might guess.** On API.Bible a
public-domain work still carries a refresh timer, a delete duty and a FUMS
obligation. On YouVersion it carries almost nothing beyond "do not change the
words and do not feed it to a model" — but the **storage** question §YVN14.9
settles for publisher content is, for this row, answered by the work's own licence
rather than by any agreement.

---

### USE6.5 The translations themselves (#3)

**This is the list a consumer actually needs**, because the intent is narrow and
common: look a verse up, show it on a page, and let a reader send it on.

**Rights class is a property of the work and is [verified] below. Availability on
a given upstream is [unverified]** — neither catalogue is enumerable without a
key, and §USE7 explains why a consumer must confirm it per key anyway. Per §4.4
the authoritative per-Bible answer is the `copyright` field returned by
`/bibles?...Full Details=true`.

| Translation | Rights | Look&nbsp;up | Display<br/>on a site | **Send on**<br/><sub>email · WhatsApp · SMS</sub> | Print | Commercial |
|---|---|:---:|:---:|:---:|:---:|:---:|
| **WEB** — World English Bible | Public domain (dedicated) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **WEBBE / WMB** — British and Messianic editions | Public domain (dedicated) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **BSB** — Berean Standard Bible | Public domain (dedicated) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **ASV** — American Standard Version 1901 | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **YLT** — Young's Literal Translation 1898 | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **DARBY** — Darby Bible 1890 | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **DRA** — Douay-Rheims, American edition 1899 | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **GNV** — Geneva Bible 1599 | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **WBT** — Webster's Bible 1833 | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **JPS 1917** — Jewish Publication Society | Public domain (expired) | ✅ | ✅ | ✅ | ✅ | ✅ |
| **KJV / KJVA** — King James Version | Public domain **in the US**; **Crown copyright in the UK** | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| **OEB** — Open English Bible | CC0 | ✅ | ✅ | ✅ | ✅ | ✅ |
| **FBV** — Free Bible Version | **CC BY-SA 4.0** | ✅ | ✅ | ✅ <sub>share-alike follows it</sub> | ✅ | ✅ |
| **ULB / UST** — unfoldingWord | **CC BY-SA 4.0** | ✅ | ✅ | ✅ <sub>share-alike follows it</sub> | ✅ | ✅ |
| **BBE** — Bible in Basic English 1949 | **[contested]** — treated as PD in the US, disputed in the UK | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| *Any* CC BY-**NC** or CC BY-**ND** edition | Restricted Creative Commons | ✅ | ✅ | ❌ <sub>named exclusion, §9.9(a)</sub> | ⚠️ | ❌ |

Beyond English the same reasoning reaches the major expired-copyright editions —
**Reina-Valera 1909** (Spanish), **Luther 1912** (German), **Louis Segond 1910**
(French), **Almeida 1911** (Portuguese), **Statenvertaling 1637** (Dutch) — and
the modern public-domain translations eBible.org publishes in hundreds of
languages. §ABS42's language scoping is what makes those reachable; each one's
rights class is [unverified] individually and is the consumer's to confirm.

**Three things this table is not.**

1. **It is not a substitute for the metadata check.** §4.4 makes reviewing the
   `copyright` field a contractual duty *before use*, for public-domain content
   expressly. A hard-coded table in a consumer's source is the thing §SOL13 and
   `INTENT.md` both argue against, for the same reason no copyright table ships
   inside these packages: it goes stale silently.
2. **It does not say these are on your key.** §USE7 stands — the class is not in
   the catalogue, and availability is per key and per plan.
3. **A ✅ under "Send on" is the work's permission, not the whole answer.** On
   API.Bible the §11 recency duty and the FUMS obligation still ride along
   (§USE6.2), and the KJV row is ⚠️ for a reason (§USE11).

---

### USE6.6 What to reach for (#3)

**If a feature sends scripture out of your application, use WEB or BSB.**

Both are modern, readable, dedicated to the public domain by their translators
rather than merely expired, and carry neither a territorial restriction nor a
share-alike obligation. WEB additionally publishes a British edition, so a UK
deployment has a same-family option the KJV cannot give it.

That is a narrower recommendation than §USE8 rule 3 made, deliberately:
**"prefer a public-domain translation" is not specific enough advice when the most
famous public-domain translation is the one carrying the territorial trap**
(§USE11).

---

## USE7. Neither API tells you which class a translation is in (#3)

**This is the practical problem, and it decides how a share feature gets built.**

Everything above turns on knowing whether a given translation is public domain,
permissively licensed, or publisher-licensed. **Neither upstream exposes that.**

- API.Bible's Bible schema has `id`, `dblId`, `abbreviation`, `abbreviationLocal`,
  `copyright`, `language`, `countries`, `name`, `nameLocal`, `description`,
  `descriptionLocal`, `info`, `type`, `updatedAt`, `relatedDbl`, `audioBibles` —
  **no licence tier, no rights class** [verified, §APB20.1].
- YouVersion's catalogue does not carry one either. The *portal* groups by
  publisher, so a human can see that 361 Bibles sit under "Public Domain and
  Creative Commons" — but the API does not say so.

**[contested] — Terms §4.4 asserts otherwise**, and §APB28.3 records it: the Terms
place a duty on the consumer to review "the `copyright` field returned for the
applicable Bible version" to identify "the applicable **copyright status, license
type**, and any use restrictions", expressly including for Public Domain content.
Both statements can be true — there is no *typed* rights field in the schema, but
ABS treats the `copyright` prose as carrying the answer. **Whether it does
reliably is a spike** (§APB28.3), and the outcome decides §USE9 rule 1.

**So a consumer that builds a share feature must classify translations itself, and
the classification must be configuration rather than inference.** That is what
`TranslationMetadata` (§ABS45) already exists for, and the natural extension is a
rights-class field on it:

```csharp
public ShareRights ShareRights { get; init; }   // proposed — §USE9

public enum ShareRights
{
    Unknown = 0,    // never assume; treat as NotPermitted
    NotPermitted = 1,
    Permitted = 2,  // public domain, CC BY, CC BY-SA, or express authorisation
}
```

**`Unknown = 0` is the whole point.** A translation nobody classified must not
default into shareable, for the same reason `ScriptureLookupStatus.Unknown` must
not default into `Found` (§ABS15 rule 1). A share button that appears because a
config row was missing is exactly the silent breach this design is built to avoid.

---

## USE8. What a consumer should actually do (#3)

1. **Store freely, refresh deliberately.** Both upstreams permit storage. Build a
   forced-refresh path and a delete path; add a 30-day sweep for API.Bible.
2. **Default the share button off.** Enable it per translation, from configuration,
   never from a guess about the copyright string. On API.Bible, for licensed
   translations, you additionally owe **DRM that prevents users copying or
   distributing** (§APB26.2) — so the question is not only whether to offer the
   button but what else must be suppressed alongside it.
3. **For a share feature, prefer a public-domain translation — but not the KJV.**
   ~~KJV, ASV and WEB are shareable on both upstreams, and are the shipped defaults
   for a reason (§APB4, §YVN4).~~ **Corrected:** ASV, WEB and BSB are shareable;
   the KJV is territorially restricted on API.Bible and excluded from the
   transmission permission outright (§USE11). **Reach for WEB or BSB** (§USE6.6) —
   **now the shipped default on both providers** (§APB4, §YVN4).
4. **Share a reference, not the text, when in doubt.** "John 3:16 (NIV)" plus a link
   to your own page carries no licensed text at all, and no clause above restricts
   it. This is the design's recommendation for licensed translations.
5. **Carry the attribution into whatever is shared**, since it leaves your UI with
   the text. Lockman additionally requires the tag to be a *link* (§YVN14.10).
6. **Ask, where it matters.** §9.9(a) permits transmission the rights holder
   "expressly authorized". That authorisation is obtainable — it is simply not the
   default.

---

## USE10. The comparison tables are derived from this file (#3)

The root README and both provider READMEs carry a **capability table** — rights
class down the columns, permission down the rows, ticks and crosses — so a reader
choosing a translation can see what they get without reading a licence.

**Those tables are a rendering of §USE1 to §USE6 and carry no facts of their own.**
Three rules follow, and §SOL19.4 rule 2 is the reason:

1. **Change this file first.** A tick that disagrees with a section here is a
   defect in the table, not a new finding.
2. **A tick is a permission, never an obligation.** Obligations go in a separate
   table below it — "required" rather than "✅" — because a reader scanning ticks
   for what they *get* will misread a tick that means what they *owe*.
3. **⚠️ means "depends on the individual work"**, and is used only where this file
   cannot resolve it — chiefly the public-domain and Creative Commons sets, where
   each work carries its own licence (§USE6).

**The provider READMEs are trimmed to their own provider**, and neither mentions
the other's rights classes. A consumer of one package should not have to reason
about an upstream they are not using.

---

## USE9. Open (#3)

1. **Does `ShareRights` earn its place on `TranslationMetadata`?** (§USE7.) It is
   additive and therefore MINOR before the first release (§SOL7 rule 4) — but it is
   also a feature this library does not itself perform, and §SOL13 keeps that sort
   of thing with the consumer. The counter-argument: the classification is
   per-translation, the consumer already configures per-translation, and getting it
   wrong is a licence breach rather than a bug. **Decide with the rest of the
   published surface.**
2. **§USE5 is inference, not quotation.** Confirm with YouVersion whether a
   share-out feature is permitted for publisher-licensed translations.
3. ~~**API.Bible's §12 DRM requirement** has not been read in full.~~ **Read** —
   §APB26. It imposed considerably more than a print limit: mandatory DRM
   restricting users from copying or distributing, a Territory restriction, a
   device-count cap, content confidentiality at a *relative* standard, and immediate
   breach notification on suspicion.

   **Two things it surfaced are now open in their own right** (§APB26.3): what
   Territory and what device count were declared at sign-up. Neither is in the API,
   neither is enforceable by this library, and both bind the consuming application.
4. ~~**API.Bible Terms §13, "Updates and Removals", has not been read.**~~
   **Read** — §APB28. It adds two things §APB17 did not carry: a **24-hour**
   deletion clock on written request, distinct from §10's 72-hour termination
   clock; and "**gains protected status**" as a removal trigger, which is what
   makes the delete path owed even by a consumer storing only public-domain
   translations (§USE6.2).
5. ~~**Per-translation figures are not recorded here**, only per-provider and
   per-publisher ones, because neither upstream exposes a per-translation rights
   class (§USE7).~~ **Done** — §USE6.5 carries a per-translation table for the
   public-domain and permissively-licensed set. Its rights classes are [verified]
   facts about the works; **availability on a given key remains [unverified]** and
   is the consumer's to confirm.
6. ~~**Should the shipped `DefaultTranslation` change from `KJV` to `WEB`?**~~
   **Decided — yes, and done** (§APB4, §YVN4). It surfaced a second finding on the
   way: YouVersion abbreviates the World English Bible **`WEBUS`**, not `WEB`, so
   that provider ships a default `TranslationMap` entry to keep one abbreviation
   meaning one thing across both (§YVN4.1).
7. **§USE11's last paragraph is inference.** API.Bible's §9.8 does not bind
   YouVersion, and no YouVersion agreement records a KJV territorial restriction —
   but the Crown's letters patent are a fact of UK law rather than a term of either
   contract. **Whether a UK deployment may serve the KJV from YouVersion is
   unresolved**, and the conservative reading is the one §USE6.6 already
   recommends: use WEB.
8. **The `copyright` field's contents are unexamined** (§APB28.3). It decides
   whether rule 1's `ShareRights` is configured or derived.

---

## USE11. The King James Version is the exception to everything above (#3)

**The most famous public-domain translation is the one a consumer may not treat as
public domain.** Full clause and analysis at §APB27; this is the consumer reading.

**Rights in the King James Version in the United Kingdom are vested in the Crown**
— perpetual letters patent, not an expiring copyright. API.Bible's Terms §9.8
therefore grants **no licence at all** for the KJV within GB, the Isle of Man,
Jersey, Guernsey and thirteen named British Overseas Territories, and says so
"**irrespective of** whether your use is Commercial Use or Non-Commercial Use,
whether any fee is charged, **whether the content is identified as Public
Domain**, and irrespective of format" [verified].

| | KJV via API.Bible |
|---|---|
| Look it up, display it, store it — **outside** the Restricted Territory | ✅ |
| Anything at all — **to a reader inside** the Restricted Territory | ❌ no licence |
| **Send it on** by email, SMS or messaging — anywhere | ❌ §9.9(b)(i) excludes territorially restricted content from §9.9(a), "irrespective of identification as Public Domain" |

**Three traps worth naming.**

1. **The duty follows the reader, not the developer.** "You shall not distribute
   the Authorized Version to a Restricted Territory." A US-hosted app with UK
   readers is in scope. **Nothing in this library knows a reader's territory**, and
   §SOL2 rule 5 keeps it that way — this is a consumer control.
2. **It is the one public-domain translation that cannot be shared.** Every other
   row in §USE6.5 marked ✅ under "Send on" is genuinely ✅. KJV is not, and the
   reason is a clause most readers will never think to look for.
3. **Derived translations are expressly out of scope.** NKJV, ESV, NASB, RSV,
   NRSV, **ASV** and MEV are named as not being the Authorized Version (§APB27.1).
   ASV in particular is a safe public-domain substitute, and WEB — itself a
   revision of the ASV — is safer still.

**Practical guidance: for a UK or Commonwealth audience, or for any share feature,
use WEB or BSB rather than KJV** (§USE6.6). If KJV must be offered, the territory
check is the consuming application's, and the safest form of it is not to offer
the edition at all in the Restricted Territory.

**This clause is API.Bible's.** YouVersion's agreements record no equivalent
[unverified, §YVN14] — but the Crown's rights are a fact of UK law rather than a
term of the API.Bible contract, so a UK deployment should not read that silence as
permission (§USE9 rule 7).

---

## USE12. Which provider for "store it, then email it"? (#3)

**This design has no default provider and will not acquire one.** The abstraction
resolves by name and nothing else (§ABS4, §SOL2) — choosing an order is an
application concern, deliberately, because which providers exist and under what
subscription changes independently of any contract here. So the question below is
a **consumer orchestration** question, and this section answers it as guidance
rather than as a shipped behaviour.

The use case is concrete: **look a verse up, keep it, and later send it to someone
by email, WhatsApp or SMS.**

### USE12.1 Separate the two permissions before comparing (#3)

**They are different rights with different sources, and the two upstreams are
strong on opposite ones.**

| | Store it | Send it on |
|---|---|---|
| **API.Bible** | Permitted, **with machinery**: 30-day recency, delete-on-withdrawal, 72-hour purge, 24-hour removal on request (§USE2, §APB28) | **Expressly permitted in writing** for public domain, CC BY and CC BY-SA — §9.9(a) |
| **YouVersion** | Permitted, **and encouraged**: express "store" grant in the publisher agreements, "Cache responses when possible" in the docs, **no timer, no purge clock, no FUMS** (§USE3, §YVN14.9, §YVN14.11) | **No express permission anywhere.** For publisher content the grant is bounded to "digital display in Your Application" and forecloses it (§USE5). For the public-domain set, the platform grants no rights in the text at all — your right to send comes from the work's own dedication (§USE6.4) |

**So the instinct is half right, and the half that is right is the important
half.** YouVersion is materially better for *storing*. It is **not** better for
*sending* — on that axis API.Bible is the only one of the two that has written
anything down.

### USE12.2 The argument that actually favours YouVersion: FUMS (#3)

**The strongest reason is one the caching line does not mention.**

API.Bible requires FUMS reporting **per display, not per fetch** (§APB14), and it
is owed on public-domain content too (§USE6.2). A stored-then-emailed verse is
exactly where that obligation becomes awkward:

- The reporting mechanism ABS documents is a **JavaScript tracker in a webapp**.
- **An email is not a webapp.** It renders in a mail client, often with images and
  script stripped.
- §11 shows ABS *contemplated* transmission — it measures recency "as of the time
  of transmission" for Electronic Correspondence — **but §14 says nothing about how
  a transmitted display is reported**, and §3 scopes the FUMS duty to webapps.

**That gap is unresolved and this design should not paper over it.** A consumer
building store-and-email on API.Bible has a compliance question to ask ABS.
**On YouVersion the question does not arise**: `Usage.Obligation` is
`NotRequired`, positively asserted (§YVN15), and there is no reporting mechanism
to keep enabled on the REST API.

**Add the retention machinery to the same ledger.** Emailed content on API.Bible
must have been no more than 30 days stale when it left (§11), the stored copy
behind it needs a 30-day sweep and a delete path, and a lapsed or unpaid plan
obliges removal within 72 hours. YouVersion imposes none of those — its duty is
update-on-request (§USE3).

### USE12.3 What argues the other way (#3)

1. **Express permission beats inferred permission.** §9.9(a) is a clause a
   consumer can point at. YouVersion's position for WEB rests on eBible.org's
   dedication plus platform silence — sound, but it is an argument rather than a
   grant, and §USE9 rule 2 still records that §USE5 is inference.
2. **The licence-acceptance gate.** A YouVersion key sees only what its portal
   agreements cover, and whether a fresh key sees the public-domain set with **no**
   agreement accepted is **[unverified]** (§YVN19 rule 2). API.Bible's open-access
   set needs no acceptance step at all. **This is the one that could stop a
   deployment working**, and it is cheap to settle.
3. **No published rate limit on YouVersion** (§YVN13). "No documented limit" is not
   "generous"; API.Bible's 5,000 a month is at least a number to design against.
4. **If the translation ever changes, the answer inverts.** Onward sending is
   foreclosed for all 1,125 publisher-licensed YouVersion Bibles (§USE5), whereas
   API.Bible at least defines a route — the IP Holder's express authorisation
   (§9.9(a)). A product that may one day send an NIV verse should not build its
   sending path on YouVersion.

### USE12.4 The recommendation (#3)

**For a store-and-send feature on a public-domain translation, prefer YouVersion —
and hold API.Bible as the failover.** The reasoning is the absence of FUMS and of
the retention clocks, not the caching line; caching is permitted on both.

**Four conditions attach, and the first is not optional:**

1. **Settle §YVN19 rule 2 first.** If a fresh key cannot see `WEBUS` without
   accepting an agreement, the ordering above is wrong on day one.
2. **Send only public-domain or CC BY / CC BY-SA text.** Default `ShareRights` to
   `Unknown` and treat it as not shareable (§USE7). WEB and BSB are the intended
   translations (§USE6.6), and both are id-verified on YouVersion (§YVN4.1).
3. **Never send the KJV from either provider** (§USE11).
4. **Carry the attribution into the message**, because it leaves your UI with the
   text (§USE8 rule 5).

**And the point that makes this a two-provider design rather than a one-provider
one:** these are different *strengths*, not a ranking. Storage favours YouVersion;
written permission to transmit favours API.Bible; availability favours whichever
key is actually provisioned. **Which is why the abstraction refuses to choose and
makes failover writable instead** (§SOL2).
