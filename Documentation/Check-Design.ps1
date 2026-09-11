<#
.SYNOPSIS
    Mechanical consistency checks over the design documents and the packed READMEs.

.DESCRIPTION
    Four classes of defect have each recurred on this branch. Each is cheap to
    detect and expensive to find by eye, so each gets a check:

      1  Dangling citation      A section reference that resolves to nothing.
      2  Sub-section gap        YVN14.7 deleted rather than struck, leaving a hole.
      3  Ordered-list gap       ABS36 and SOL19.4: a rule inserted mid-list or
                                sourced out of order, silently renumbering rules
                                that other sections cite. Design.md calls a
                                citation that resolves to the WRONG rule "worse
                                than a dangling reference".
      4  Stale range header     A file's "Sections: X - Y" header left behind.

    Exits non-zero if anything fails, so it can gate a commit.

.EXAMPLE
    .\Documentation\Check-Design.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$designDir = Join-Path $root 'Documentation\Design'
$failures = 0

function Fail($check, $detail) {
    Write-Host ("  FAIL  {0}" -f $detail) -ForegroundColor Red
    $script:failures++
}
function Pass($check) { Write-Host ("  ok    {0}" -f $check) -ForegroundColor Green }

$designFiles = Get-ChildItem -Path $designDir -Filter *.md
$readmes = @(Get-ChildItem -Path $root -Filter README.md) +
           @(Get-ChildItem -Path $root -Directory -Filter 'Glory2Him.BibleProviders.*' |
             ForEach-Object { Get-ChildItem -Path $_.FullName -Filter README.md -ErrorAction SilentlyContinue })
$prose = @(Get-ChildItem -Path $root -Filter *.md) + $designFiles + $readmes |
         Sort-Object FullName -Unique

# ---------------------------------------------------------------- 1. sections --
$sections = [System.Collections.Generic.HashSet[string]]::new()
foreach ($f in $designFiles) {
    foreach ($line in Get-Content $f.FullName) {
        if ($line -match '^##\s+((?:SOL|ABS|APB|YVN|USE)\d+)\.\s')       { $null = $sections.Add($Matches[1]) }
        elseif ($line -match '^###\s+((?:SOL|ABS|APB|YVN|USE)\d+\.\d+)\s') { $null = $sections.Add($Matches[1]) }
    }
}

Write-Host "`n1. Citations resolve" -ForegroundColor Cyan
$dangling = @()
foreach ($f in $prose) {
    if ($f.Name -eq 'CLAUDE.md') { continue }
    $n = 0
    foreach ($line in Get-Content $f.FullName) {
        $n++
        foreach ($m in [regex]::Matches($line, '§((?:SOL|ABS|APB|YVN|USE)\d+(?:\.\d+)?)')) {
            if (-not $sections.Contains($m.Groups[1].Value)) {
                $dangling += ("{0}:{1} -> {2}" -f $f.Name, $n, $m.Groups[1].Value)
            }
        }
    }
}
if ($dangling) { $dangling | ForEach-Object { Fail 'citation' $_ } }
else { Pass ("{0} sections defined; every reference resolves" -f $sections.Count) }

# ------------------------------------------------------------ 2. subsection gaps --
Write-Host "`n2. Sub-section numbering is contiguous" -ForegroundColor Cyan
$groups = @{}
foreach ($s in $sections) {
    if ($s -match '^(.+)\.(\d+)$') {
        if (-not $groups.ContainsKey($Matches[1])) { $groups[$Matches[1]] = @() }
        $groups[$Matches[1]] += [int]$Matches[2]
    }
}
$gapFound = $false
foreach ($k in ($groups.Keys | Sort-Object)) {
    $have = $groups[$k] | Sort-Object
    $missing = 1..($have[-1]) | Where-Object { $_ -notin $have }
    if ($missing) { Fail 'subsection' ("{0} missing .{1} - strike it in place, never delete" -f $k, ($missing -join ', .')); $gapFound = $true }
}
if (-not $gapFound) { Pass 'no gaps' }

# --------------------------------------------------------- 3. ordered-list gaps --
# A markdown ordered list whose SOURCE numbers are not 1,2,3... renumbers when
# rendered, so "rule 4" means different things in the repo and on GitHub.
Write-Host "`n3. Ordered lists are sourced contiguously" -ForegroundColor Cyan
$listBad = $false
foreach ($f in $designFiles) {
    $lines = Get-Content $f.FullName
    $heading = ''; $last = 0; $firstLine = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        # Reset only at a SECTION boundary. A list may legally continue across a
        # '###' sub-heading - SOL7's release rules run 1-5, a sub-heading, then 6-10.
        if ($lines[$i] -match '^##\s+(.+)$')   { $heading = $Matches[1]; $last = 0 }
        elseif ($lines[$i] -match '^###\s+(.+)$') { $heading = $Matches[1] }
        if ($lines[$i] -match '^(\d+)\.\s') {
            $n = [int]$Matches[1]
            # 1 starts a fresh list; otherwise it must be exactly one more than the
            # last item seen under this heading. A list may legally resume after
            # prose or a table, so only the SEQUENCE matters, not adjacency.
            if ($n -eq 1) { $last = 1; $firstLine = $i + 1 }
            elseif ($n -eq $last + 1) { $last = $n }
            else {
                Fail 'list' ("{0}:{1} under '{2}' jumps {3} -> {4}" -f $f.Name, ($i + 1), $heading, $last, $n)
                $listBad = $true
                $last = $n
            }
        }
    }
}
if (-not $listBad) { Pass 'every ordered list is sourced 1..n' }

# ------------------------------------------------------------- 4. range headers --
Write-Host "`n4. Range headers match their file" -ForegroundColor Cyan
$rangeBad = $false
foreach ($f in $designFiles) {
    $text = Get-Content $f.FullName -Raw
    if ($text -match '\*\*Sections:\*\*\s+§(\w+?)(\d+)\s+–\s+§(\w+?)(\d+)') {
        $prefix = $Matches[1]; $declared = [int]$Matches[4]
        $actual = ([regex]::Matches($text, '(?m)^##\s+' + $prefix + '(\d+)\.\s') |
                   ForEach-Object { [int]$_.Groups[1].Value } | Measure-Object -Maximum).Maximum
        if ($actual -ne $declared) {
            Fail 'range' ("{0} header says {1}{2} but the file ends at {1}{3}" -f $f.Name, $prefix, $declared, $actual)
            $rangeBad = $true
        }
    }
}
if (-not $rangeBad) { Pass 'headers current' }

Write-Host ''
if ($failures) {
    Write-Host ("{0} failure(s). Design.md's Conventions block says why each of these matters." -f $failures) -ForegroundColor Red
    exit 1
}
Write-Host 'All design consistency checks passed.' -ForegroundColor Green
