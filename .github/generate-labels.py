#!/usr/bin/env python3
"""Generate .github/labels.json from the authoritative prefix list in prLinter.yml.

Run from the repository root after adding a prefix to prLinter.yml:

    .github/generate-labels.py .github/workflows/prLinter.yml .github/labels.json

It fails rather than guessing when a prefix has no colour, so a new prefix cannot
silently become a default grey label the first time someone uses it in a PR title.
"""
import json
import re
import sys

PR_LINTER = sys.argv[1]
OUT = sys.argv[2]

# Colours and descriptions read from the live Glory2Him.Core labels via the GitHub API.
# Entries marked INVENTED had no label on the repo, or carried GitHub's default grey
# (ededed) because prLinter's addLabels call auto-created them.
BASE = {
    "INFRA":          ("1E90FF", "Project setup, creating the build project / build scripts"),
    "PROVISIONS":     ("2E8B57", "Provisions scripts"),
    "RELEASES":       ("2AE9F8", "The releases category"),
    "DATA":           ("DAA520", "The data category"),                      # INVENTED (was grey)
    "BROKERS":        ("4682B4", "The brokers category"),                   # INVENTED (was grey)
    "FOUNDATIONS":    ("A11EE7", "The foundations category"),
    "PROCESSINGS":    ("8A2BE2", "The processings category"),
    "ORCHESTRATIONS": ("20B2AA", "The orchestrations category"),
    "COORDINATIONS":  ("5F9EA0", "The coordinations category"),             # INVENTED (missing)
    "MANAGEMENTS":    ("B60205", "The management category"),
    "AGGREGATIONS":   ("426DD3", "The aggregations category"),
    "CONTROLLERS":    ("DB3ABE", "The controllers category"),
    "CLIENTS":        ("DB3ABE", "The clients category"),
    "EXPOSERS":       ("DB3ABE", "The exposers category"),
    "PROVIDERS":      ("DB3ABE", "The providers category"),
    "BASE":           ("708090", "The base components category"),           # INVENTED (missing)
    "COMPONENTS":     ("00CED1", "The components category"),
    "VIEWS":          ("9370DB", "The views category"),
    "PAGES":          ("C4BB46", "The pages category"),
    "ACCEPTANCE":     ("40E0D0", "The acceptance tests category"),
    "INTEGRATIONS":   ("7B68EE", "The integration tests category"),
    "CODE RUB":       ("9A3BC5", "The code rub category is for small changes (not for bug fixes)"),
    "DOCUMENTATION":  ("0075CA", "The documentation category"),             # INVENTED (missing)
    "CONFIG":         ("8FBC8F", "The configuration category is for any configuration changes i.e. setting up appsettings.json"),
    "STANDARD":       ("006B75", "Changes to The Standard skills or the engineering rules"),  # INVENTED (missing)
    "DESIGN":         ("FF8C00", "Architecture and design decisions"),      # INVENTED (was grey)
    "BUSINESS":       ("3CB371", "The business category is for documentation of business processes / Standard Operating Procedures"),
    "MIGRATIONS":     ("CD853F", "Database schema migrations"),             # INVENTED (missing)
    "PLANNING":       ("BFD4F2", "Planning and backlog shaping"),           # INVENTED (missing)
    "MENTORSHIP":     ("FEF2C0", "Mentorship and knowledge sharing"),       # INVENTED (missing)
    "DISCUSSION":     ("D4C5F9", "Open discussion, no code change expected"),  # INVENTED (missing)
    "IMPORTS":        ("C2E0C6", "Bulk imports of content or data"),        # INVENTED (missing)
    "REVIEWS":        ("BFDADC", "Code or content review work"),            # INVENTED (missing)
    "STATUS":         ("C5DEF5", "Status reporting, no code change"),       # INVENTED (missing)
}

# Labels whose live colour and wording do not follow the base + tier pattern.
OVERRIDES = {
    "MINOR FIX":  ("E99695", "A minor bug fix / change that do not significantly alter the overall functionality of the program"),
    "MEDIUM FIX": ("D93F0B", "A medium fix has a moderate level of impact on the functionality or performance of the program"),
    "MAJOR FIX":  ("B60205", "A major bug fix is a significant repair or correction made to addresses a critical or major issue"),
}

TIERS = {
    "MINOR": "a minor change",
    "MEDIUM": "a medium change",
    "MAJOR": "a major change",
}

# Not a tidy matrix: Opus 5 carries five efforts and the other two carry three.
# Verified against the live labels — there is no Opus 5 - Low, no Sonnet 5 - Max and
# no Fable 5 - Extra. DEVELOPERS.md section 10 is the written record of this.
MODEL_EFFORTS = {
    "Opus 5": ["Small", "Medium", "High", "Extra", "Max"],
    "Sonnet 5": ["Low", "Medium", "High"],
    "Fable 5": ["Low", "Medium", "High"],
}
EFFORT_COLOUR = "fbca04"

# The issue lifecycle. Approval is a label rather than a review, so these carry it.
# DEVELOPERS.md section 7 is the written record.
STATUS_LABELS = [
    ("status: needs-scoping", "e4e669", "Criteria written, waiting on a human to approve them"),
    ("status: ready-for-dev", "0e8a16", "Approved. The developer will not start without this"),
    ("status: in-progress", "1d76db", "A developer is implementing it"),
    ("status: in-qa", "5319e7", "Implemented, under adversarial verification"),
    ("status: done", "6a737d", "Merged and verified"),
]

# One per design area, applied by the analyst so an area has a live query that never
# goes stale. Note the trap: the all-caps DESIGN label is a PR category label the
# linter auto-applies from a title prefix, and is a different thing entirely.
DESIGN_AREA_COLOUR = "f9d0c4"
DESIGN_AREAS = ["architecture", "domain", "events", "security", "ui"]
EFFORT_DESCRIPTION = "Suggested model and effort for this issue"

with open(PR_LINTER, encoding="utf-8") as file:
    source = file.read()

prefixes = re.findall(r"^\s*'([A-Z][A-Z ]*):',?\s*$", source, re.MULTILINE)

if not prefixes:
    raise SystemExit("No prefixes found in prLinter.yml — has the workflow format changed?")

labels = []
unknown = []

for prefix in prefixes:
    name = prefix.rstrip(":")

    if name in OVERRIDES:
        colour, description = OVERRIDES[name]
    else:
        tier, _, remainder = name.partition(" ")
        if tier in TIERS and remainder in BASE:
            colour, base_description = BASE[remainder]
            description = f"{base_description} — {TIERS[tier]}"
        elif name in BASE:
            colour, description = BASE[name]
        else:
            unknown.append(name)
            continue

    labels.append({"name": name, "color": colour.lower(), "description": description})

for model, efforts in MODEL_EFFORTS.items():
    for effort in efforts:
        labels.append({
            "name": f"{model} - {effort}",
            "color": EFFORT_COLOUR,
            "description": EFFORT_DESCRIPTION,
        })

for name, colour, description in STATUS_LABELS:
    labels.append({"name": name, "color": colour, "description": description})

for area in DESIGN_AREAS:
    labels.append({
        "name": f"design: {area}",
        "color": DESIGN_AREA_COLOUR,
        "description": f"Touches the {area} design",
    })

if unknown:
    raise SystemExit(f"No colour mapping for: {', '.join(unknown)}")

names = [label["name"] for label in labels]
duplicates = {name for name in names if names.count(name) > 1}

if duplicates:
    raise SystemExit(f"Duplicate labels: {', '.join(sorted(duplicates))}")

with open(OUT, "w", encoding="utf-8", newline="\n") as file:
    json.dump(labels, file, indent=2, ensure_ascii=False)
    file.write("\n")

print(f"{len(labels)} labels written to {OUT} ({len(prefixes)} prefixes + {sum(len(e) for e in MODEL_EFFORTS.values())} model-effort + {len(STATUS_LABELS)} status + {len(DESIGN_AREAS)} design area)")
