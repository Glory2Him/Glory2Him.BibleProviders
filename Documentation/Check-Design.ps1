<#
.SYNOPSIS
    Mechanical consistency checks over the design documents.

.DESCRIPTION
    WHAT THIS COVERS, precisely. Each check prints only what it actually tested;
    a green run says nothing about anything under "WHAT THIS DOES NOT COVER".

      1  Section citations      A reference resolving to nothing.
      2  Section numbering      A deleted section leaving a hole (YVN14.7), and a
                                re-used number (Design.md forbids both).
      3  Ordered-list source    A list sourced 1,2,3,5,4 renumbers when rendered,
                                so "rule 4" means different things in the repo and
                                on the rendered page (SOL19.4).
      4  Rule citations         "SOL7 rule 44" where that list has five rules.
                                Section-level citation checking never saw these.
      5  Renumbered rules       A rule inserted mid-list, silently renumbering
                                pre-existing rules that other sections cite
                                (ABS36, YVN18). Detected by diffing rule TEXT
                                against a base ref, so it needs the git history.
      6  Range headers          A "Sections: X - Y" header left behind at either
                                end.

    WHAT THIS DOES NOT COVER, and must not be read as covering:

      * Whether an obligation stated in a design section reached the README that
        SOL19.3 assigns it to. That is the recurring defect class on this branch
        and this script does not test for it at all.
      * Whether a capability table's ticks agree across the three surfaces that
        carry them.
      * Anything about the licence readings. Not one clause is in this
        repository; every [verified] tag rests on a human having read a portal.
      * Prose obligations carrying no figure.

    Exits non-zero on any failure.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Documentation\Check-Design.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseRef = 'main'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$designDir = Join-Path $root 'Documentation\Design'
$failures = 0

function Fail($detail) { Write-Host ("  FAIL  {0}" -f $detail) -ForegroundColor Red; $script:failures++ }
function Pass($detail) { Write-Host ("  ok    {0}" -f $detail) -ForegroundColor Green }
function Note($detail) { Write-Host ("  --    {0}" -f $detail) -ForegroundColor DarkGray }

# Strip fenced code blocks: a sample output line "3. Genesis" is not a rule.
function Get-ProseLines([string] $path) {
    $out = New-Object System.Collections.ArrayList
    $fenced = $false
    $n = 0
    foreach ($line in (Get-Content -LiteralPath $path)) {
        $n++
        if ($line -match '^\s*```') { $fenced = -not $fenced; $null = $out.Add([pscustomobject]@{ N = $n; Text = '' }); continue }
        if ($fenced) { $null = $out.Add([pscustomobject]@{ N = $n; Text = '' }) }
        else         { $null = $out.Add([pscustomobject]@{ N = $n; Text = $line }) }
    }
    , $out
}

$designFiles = Get-ChildItem -Path $designDir -Filter *.md
$prose = @(Get-ChildItem -Path $root -Filter *.md) + $designFiles +
         @(Get-ChildItem -Path $root -Directory -Filter 'Glory2Him.BibleProviders.*' |
           ForEach-Object { Get-ChildItem -Path $_.FullName -Filter README.md -ErrorAction SilentlyContinue })
$prose = $prose | Sort-Object FullName -Unique

$sectionList = New-Object System.Collections.ArrayList
$ruleMax = @{}
$ruleText = @{}

foreach ($f in $designFiles) {
    $current = $null
    foreach ($row in (Get-ProseLines $f.FullName)) {
        $line = $row.Text
        if ($line -match '^##\s+((?:SOL|ABS|APB|YVN|USE)\d+)\.\s')        { $current = $Matches[1]; $null = $sectionList.Add($current) }
        elseif ($line -match '^###\s+((?:SOL|ABS|APB|YVN|USE)\d+\.\d+)\s') { $current = $Matches[1]; $null = $sectionList.Add($current) }
        elseif ($line -match '^\|\s*(\d+)\s*\|' -and $current) {
            # SOL16 numbers its rules as table rows, not as a markdown list.
            $n = [int]$Matches[1]
            if (-not $ruleMax.ContainsKey($current) -or $ruleMax[$current] -lt $n) { $ruleMax[$current] = $n }
        }
        elseif ($line -match '^(\d+)\.\s+(.*)$' -and $current) {
            $n = [int]$Matches[1]
            if (-not $ruleMax.ContainsKey($current) -or $ruleMax[$current] -lt $n) { $ruleMax[$current] = $n }
            $key = '{0}|{1}|{2}' -f $f.Name, $current, $n
            if (-not $ruleText.ContainsKey($key)) {
                $t = ($Matches[2] -replace '[^a-zA-Z0-9 ]', '').Trim()
                if ($t.Length -gt 50) { $t = $t.Substring(0, 50) }
                $ruleText[$key] = $t
            }
        }
    }
}
$sections = [System.Collections.Generic.HashSet[string]]::new()
foreach ($x in $sectionList) { $null = $sections.Add($x) }

Write-Host "`n1. Section citations resolve" -ForegroundColor Cyan
$bad = @()
foreach ($f in $prose) {
    if ($f.Name -eq 'CLAUDE.md') { continue }
    foreach ($row in (Get-ProseLines $f.FullName)) {
        foreach ($m in [regex]::Matches($row.Text, "$([char]0xA7)((?:SOL|ABS|APB|YVN|USE)\d+(?:\.\d+)?)")) {
            if (-not $sections.Contains($m.Groups[1].Value)) { $bad += ("{0}:{1} -> {2}" -f $f.Name, $row.N, $m.Groups[1].Value) }
        }
    }
}
if ($bad) { $bad | ForEach-Object { Fail $_ } } else { Pass ("{0} sections defined; every section reference resolves" -f $sections.Count) }

Write-Host "`n2. Section numbering has no holes or re-use" -ForegroundColor Cyan
$dupes = @($sectionList | Group-Object | Where-Object { $_.Count -gt 1 })
foreach ($d in $dupes) { Fail ("{0} is declared {1} times - a number is never re-used" -f $d.Name, $d.Count) }
$byPrefix = @{}
foreach ($x in $sections) {
    $k = $null
    if ($x -match '^([A-Z]{3})(\d+)$')          { $k = $Matches[1] }
    elseif ($x -match '^([A-Z]{3}\d+)\.(\d+)$') { $k = $Matches[1] }
    if (-not $k) { continue }
    if (-not $byPrefix.ContainsKey($k)) { $byPrefix[$k] = @() }
    $byPrefix[$k] += [int]$Matches[2]
}
$holes = $false
foreach ($k in ($byPrefix.Keys | Sort-Object)) {
    $have = @($byPrefix[$k] | Sort-Object -Unique)
    $missing = @(1..($have[-1]) | Where-Object { $_ -notin $have })
    if ($missing.Count) { Fail ("{0} missing {1} - strike a dead section in place, never delete it" -f $k, ($missing -join ', ')); $holes = $true }
}
if (-not $holes -and -not $dupes.Count) { Pass 'top-level and sub-section numbering contiguous, no number re-used' }

Write-Host "`n3. Ordered lists are sourced in sequence" -ForegroundColor Cyan
$listBad = $false
foreach ($f in $designFiles) {
    $heading = ''; $last = 0
    foreach ($row in (Get-ProseLines $f.FullName)) {
        if ($row.Text -match '^##\s+(.+)$')      { $heading = $Matches[1]; $last = 0 }
        elseif ($row.Text -match '^###\s+(.+)$') { $heading = $Matches[1] }
        elseif ($row.Text -match '^(\d+)\.\s') {
            $n = [int]$Matches[1]
            if ($n -eq 1) { $last = 1 }
            elseif ($n -eq $last + 1) { $last = $n }
            else { Fail ("{0}:{1} under '{2}' jumps {3} -> {4}" -f $f.Name, $row.N, $heading, $last, $n); $listBad = $true; $last = $n }
        }
    }
}
if (-not $listBad) { Pass 'no list is sourced out of sequence' }

Write-Host "`n4. Rule-level citations resolve" -ForegroundColor Cyan
$ruleBad = $false
foreach ($f in $prose) {
    if ($f.Name -eq 'CLAUDE.md') { continue }
    foreach ($row in (Get-ProseLines $f.FullName)) {
        foreach ($m in [regex]::Matches($row.Text, "$([char]0xA7)((?:SOL|ABS|APB|YVN|USE)\d+(?:\.\d+)?)\s+rule\s+(\d+)")) {
            $sec = $m.Groups[1].Value; $n = [int]$m.Groups[2].Value
            if (-not $sections.Contains($sec)) { continue }
            if (-not $ruleMax.ContainsKey($sec)) {
                Fail ("{0}:{1} cites {2} rule {3}, but {2} has no numbered list" -f $f.Name, $row.N, $sec, $n); $ruleBad = $true
            }
            elseif ($n -gt $ruleMax[$sec]) {
                Fail ("{0}:{1} cites {2} rule {3}; that list ends at {4}" -f $f.Name, $row.N, $sec, $n, $ruleMax[$sec]); $ruleBad = $true
            }
        }
    }
}
if (-not $ruleBad) { Pass 'every "SECTION rule N" citation names a rule that exists' }

Write-Host "`n5. Pre-existing rules keep their numbers" -ForegroundColor Cyan
$null = & git -C $root rev-parse --verify --quiet "$BaseRef^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) {
    Note ("base ref '{0}' not found - this check did not run" -f $BaseRef)
}
else {
    $renum = $false
    foreach ($f in $designFiles) {
        $rel = 'Documentation/Design/' + $f.Name
        # ls-tree exits 0 and prints nothing when the path is absent, so it cannot
        # trip PS 5.1's NativeCommandError the way `git show` on a missing blob does.
        $present = & git -C $root ls-tree -r --name-only $BaseRef -- $rel
        if (-not $present) { Note ("{0} is new since {1} - nothing to compare" -f $f.Name, $BaseRef); continue }
        $baseText = & git -C $root show "${BaseRef}:${rel}"
        if (-not $baseText) { Note ("{0} is empty on {1}" -f $f.Name, $BaseRef); continue }
        $baseMap = @{}; $cur = $null; $fenced = $false
        foreach ($line in $baseText) {
            if ($line -match '^\s*```') { $fenced = -not $fenced; continue }
            if ($fenced) { continue }
            if ($line -match '^##\s+((?:SOL|ABS|APB|YVN|USE)\d+)\.\s')        { $cur = $Matches[1] }
            elseif ($line -match '^###\s+((?:SOL|ABS|APB|YVN|USE)\d+\.\d+)\s') { $cur = $Matches[1] }
            elseif ($line -match '^(\d+)\.\s+(.*)$' -and $cur) {
                $t = ($Matches[2] -replace '[^a-zA-Z0-9 ]', '').Trim()
                if ($t.Length -gt 50) { $t = $t.Substring(0, 50) }
                if ($t -and -not $baseMap.ContainsKey("$cur|$t")) { $baseMap["$cur|$t"] = [int]$Matches[1] }
            }
        }
        foreach ($key in $ruleText.Keys) {
            $parts = $key.Split('|')
            if ($parts[0] -ne $f.Name) { continue }
            $k = '{0}|{1}' -f $parts[1], $ruleText[$key]
            if ($baseMap.ContainsKey($k) -and $baseMap[$k] -ne [int]$parts[2]) {
                $snip = $ruleText[$key]
                if ($snip.Length -gt 32) { $snip = $snip.Substring(0, 32) }
                Fail ("{0} {1}: rule '{2}...' was {3} on {4}, is now {5} - append, never insert" -f $f.Name, $parts[1], $snip, $baseMap[$k], $BaseRef, $parts[2])
                $renum = $true
            }
        }
    }
    if (-not $renum) { Pass ("no rule that existed on {0} changed its number" -f $BaseRef) }
}

Write-Host "`n6. Range headers match their file" -ForegroundColor Cyan
$rangeBad = $false
foreach ($f in $designFiles) {
    $text = Get-Content -LiteralPath $f.FullName -Raw
    if ($text -match "\*\*Sections:\*\*\s+$([char]0xA7)([A-Z]{3})(\d+)\s*[-\u2013\u2014]\s*$([char]0xA7)([A-Z]{3})(\d+)") {
        $prefix = $Matches[1]; $from = [int]$Matches[2]; $to = [int]$Matches[4]
        $nums = @([regex]::Matches($text, '(?m)^##\s+' + $prefix + '(\d+)\.\s') | ForEach-Object { [int]$_.Groups[1].Value })
        $lo = ($nums | Measure-Object -Minimum).Minimum
        $hi = ($nums | Measure-Object -Maximum).Maximum
        if ($from -ne $lo -or $to -ne $hi) {
            Fail ("{0} header says {1}{2}-{1}{3}; the file runs {1}{4}-{1}{5}" -f $f.Name, $prefix, $from, $to, $lo, $hi)
            $rangeBad = $true
        }
    }
}
if (-not $rangeBad) { Pass 'every range header matches both ends of its file' }

Write-Host ''
Write-Host 'NOT tested here: whether a design obligation reached the README SOL19.3 assigns' -ForegroundColor DarkGray
Write-Host 'it to, whether capability tables agree across surfaces, or any licence reading.' -ForegroundColor DarkGray
if ($failures) {
    Write-Host ("`n{0} failure(s)." -f $failures) -ForegroundColor Red
    exit 1
}
Write-Host "`nThe six checks above passed." -ForegroundColor Green
