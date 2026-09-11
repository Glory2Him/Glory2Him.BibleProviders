# Usage permission — what may be stored, and what may be passed on

**Area prefix:** `USE` · **Sections:** §USE1 – §USE9
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
| **API.Bible** — public domain, CC BY, CC BY-SA | Yes, refreshed on cycle | **Yes** |
| **API.Bible** — any other translation (NIV, ESV, NLT…) | Yes, refreshed on cycle | **No**, unless the rights holder expressly authorised it |
| **YouVersion** — the Public Domain & Creative Commons set (361) | Yes | **Governed by each work's own PD/CC licence**, not by an agreement — §USE6 |
| **YouVersion** — any of the nine publisher agreements (1,124) | Yes | **No** — licensed for display *in your application* |

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
| KJV, ASV, WEB and other public domain | **Permitted** |
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

**Reading: a share-out feature is not permitted for the 1,124 Bibles under the
nine publisher agreements.** Marked **[unverified]** as a *conclusion* rather than
a quotation — no clause says "you may not share to social media", and this is
inference from the scope of the grant. **If a share feature matters commercially,
ask YouVersion rather than rely on this paragraph.**

---

## USE6. The public-domain set is a different question (#3)

YouVersion's largest licence row — **361 Bibles under "Public Domain and Creative
Commons"** — has **no agreement document to show** [verified]. That is consistent:
there is no licensor to agree with.

**Those works are governed by their own public-domain status or Creative Commons
licence, not by anything in this design.** A CC BY-SA work carries share-alike
obligations that follow the text wherever it goes; a CC BY-NC work cannot be used
commercially whatever any platform says. **Check the individual work**, and note
that the same NC/ND exclusions API.Bible names in §9.9(a) apply by the licences'
own terms.

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
3. **For a share feature, prefer a public-domain translation.** KJV, ASV and WEB
   are shareable on both upstreams, and are the shipped defaults for a reason
   (§APB4, §YVN4).
4. **Share a reference, not the text, when in doubt.** "John 3:16 (NIV)" plus a link
   to your own page carries no licensed text at all, and no clause above restricts
   it. This is the design's recommendation for licensed translations.
5. **Carry the attribution into whatever is shared**, since it leaves your UI with
   the text. Lockman additionally requires the tag to be a *link* (§YVN14.10).
6. **Ask, where it matters.** §9.9(a) permits transmission the rights holder
   "expressly authorized". That authorisation is obtainable — it is simply not the
   default.

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
4. **API.Bible Terms §13, "Updates and Removals", has not been read.** §APB17
   records the removal duties from §10 and §11; whether §13 adds to them is unknown
   (§APB26.4).
5. **Per-translation figures are not recorded here**, only per-provider and
   per-publisher ones, because neither upstream exposes a per-translation rights
   class (§USE7). If a consumer builds the classification table anyway, this file is
   where it belongs.
