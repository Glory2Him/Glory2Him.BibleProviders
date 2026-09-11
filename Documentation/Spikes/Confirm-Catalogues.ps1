<#
.SYNOPSIS
    Answers the three catalogue questions the design cannot settle without live keys.

.DESCRIPTION
    Closes, or fails to close, these open items:

      APB23 rule 2   Is WEB in API.Bible's open-access set on a Starter key?
                     The shipped DefaultTranslation depends on it (APB4, ABS20.1).

      APB28.3        What does the `copyright` field actually contain? Free prose,
                     or something structured enough to derive a rights class from?
                     Decides whether ShareRights is configured or derived (USE9 rule 1).

      YVN19 rule 2   Does a YouVersion app key see WEB (WEBUS, id 206) without
                     accepting an agreement in the portal? If not, ABS20.2 rule 2
                     is the escape hatch and that deployment must configure a default.

    Keys are read from environment variables and are never printed, logged or
    written to disk (SOL14 rule 5). Nothing is written anywhere; this only reads.

.EXAMPLE
    $env:APIBIBLE_KEY = '...'; $env:YOUVERSION_APP_KEY = '...'
    .\Documentation\Spikes\Confirm-Catalogues.ps1

.EXAMPLE
    .\Documentation\Spikes\Confirm-Catalogues.ps1 -OutFile spike-results.json
#>
[CmdletBinding()]
param(
    [string] $Language = 'eng',
    [string] $OutFile
)

$ErrorActionPreference = 'Stop'

function Write-Head($text) {
    Write-Host ''
    Write-Host $text -ForegroundColor Cyan
    Write-Host ('-' * $text.Length) -ForegroundColor Cyan
}

function Write-Verdict($label, $ok, $detail) {
    $mark = if ($ok) { '[PASS]' } elseif ($null -eq $ok) { '[????]' } else { '[FAIL]' }
    $colour = if ($ok) { 'Green' } elseif ($null -eq $ok) { 'Yellow' } else { 'Red' }
    Write-Host ("{0,-7}{1,-46}{2}" -f $mark, $label, $detail) -ForegroundColor $colour
}

$results = [ordered]@{
    runUtc   = (Get-Date).ToUniversalTime().ToString('o')
    language = $Language
}

# ---------------------------------------------------------------- API.Bible --
Write-Head 'API.Bible  -  APB23 rule 2, APB28.3'

if (-not $env:APIBIBLE_KEY) {
    Write-Host 'APIBIBLE_KEY is not set - skipping. Set it and re-run.' -ForegroundColor Yellow
    $results.apiBible = @{ skipped = $true }
}
else {
    $uri = "https://rest.api.bible/v1/bibles?language=$Language&include-full-details=true"
    try {
        $response = Invoke-RestMethod -Uri $uri -Headers @{ 'api-key' = $env:APIBIBLE_KEY } -Method Get
        $bibles = @($response.data)
        Write-Host ("{0} Bibles visible to this key in '{1}'." -f $bibles.Count, $Language)

        # --- APB23 rule 2: is the shipped default actually reachable? ---
        foreach ($abbr in @('WEB', 'BSB', 'ASV', 'KJV', 'WEBBE')) {
            $hit = $bibles | Where-Object { $_.abbreviation -eq $abbr -or $_.abbreviationLocal -eq $abbr } | Select-Object -First 1
            if ($hit) { Write-Verdict $abbr $true $hit.id } else { Write-Verdict $abbr $false 'not in this catalogue' }
        }

        # --- APB28.3: what is in the copyright field? ---
        Write-Host ''
        Write-Host 'copyright field, three samples (APB28.3):' -ForegroundColor Cyan
        $samples = @()
        foreach ($abbr in @('WEB', 'FBV', 'NIV', 'NLT', 'NKJV')) {
            $hit = $bibles | Where-Object { $_.abbreviation -eq $abbr } | Select-Object -First 1
            if ($hit) { $samples += $hit }
            if ($samples.Count -ge 3) { break }
        }
        if (-not $samples) { $samples = $bibles | Select-Object -First 3 }

        foreach ($b in $samples) {
            $c = if ($b.copyright) { ($b.copyright -replace '\s+', ' ').Trim() } else { '<null or absent>' }
            if ($c.Length -gt 220) { $c = $c.Substring(0, 220) + ' ...' }
            Write-Host ("  {0,-8} {1}" -f $b.abbreviation, $c)
        }

        $withCopyright = @($bibles | Where-Object { $_.copyright }).Count
        Write-Host ''
        Write-Verdict 'copyright populated' ($withCopyright -gt 0) "$withCopyright of $($bibles.Count) carry a copyright value"
        Write-Host '  Judgement call for a human: is that prose, or is it structured enough' -ForegroundColor DarkGray
        Write-Host '  to classify a translation from? Prose => USE7 stands, ShareRights is' -ForegroundColor DarkGray
        Write-Host '  configured. Structured => it may be derived (USE9 rule 1).' -ForegroundColor DarkGray

        $results.apiBible = @{
            total         = $bibles.Count
            abbreviations = @($bibles | ForEach-Object { $_.abbreviation } | Sort-Object -Unique)
            withCopyright = $withCopyright
            samples       = @($samples | ForEach-Object { @{ id = $_.id; abbreviation = $_.abbreviation; copyright = $_.copyright } })
        }
    }
    catch {
        Write-Host ("Request failed: {0}" -f $_.Exception.Message) -ForegroundColor Red
        Write-Host 'A 401 means the key is rejected; 403 means the plan does not cover this call.' -ForegroundColor DarkGray
        $results.apiBible = @{ error = $_.Exception.Message }
    }
}

# -------------------------------------------------------------- YouVersion --
Write-Head 'YouVersion  -  YVN19 rule 2'

if (-not $env:YOUVERSION_APP_KEY) {
    Write-Host 'YOUVERSION_APP_KEY is not set - skipping. Set it and re-run.' -ForegroundColor Yellow
    $results.youVersion = @{ skipped = $true }
}
else {
    # YVN7 rule 2: send the bracketed spelling first; on a 422 naming the field,
    # retry once with the bare spelling and say so. Two upstream pages disagree.
    $headers = @{ 'X-YVP-App-Key' = $env:YOUVERSION_APP_KEY }
    $spelling = 'language_ranges[]'
    try {
        try {
            $uri = "https://api.youversion.com/v1/bibles?language_ranges%5B%5D=$Language"
            $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }
        catch {
            $code = $null
            if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            if ($code -ne 422) { throw }
            Write-Host 'Bracketed language_ranges[] returned 422 - retrying bare spelling (YVN7 rule 2).' -ForegroundColor Yellow
            $spelling = 'language_ranges'
            $uri = "https://api.youversion.com/v1/bibles?language_ranges=$Language"
            $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        }
        Write-Host ("Catalogue accepted the '{0}' spelling - record this against YVN7 rule 2." -f $spelling) -ForegroundColor Cyan
        $versions = @($response.data)
        Write-Host ("{0} versions visible to this key in '{1}'." -f $versions.Count, $Language)
        if ($versions.Count -eq 0) {
            Write-Host 'An empty list is a 200, not an error: it usually means this key has' -ForegroundColor Yellow
            Write-Host 'accepted no agreement in the portal (YVN17), which is itself the answer' -ForegroundColor Yellow
            Write-Host 'to YVN19 rule 2 for a fresh key.' -ForegroundColor Yellow
        }
        if ($response.next_page_token) {
            Write-Host 'More pages exist - this is page one only (YVN7).' -ForegroundColor DarkGray
        }

        # id is the load-bearing value here; abbreviation spelling varies (YVN4.1)
        $known = [ordered]@{ '206' = 'WEBUS - the shipped default'; '3034' = 'BSB'; '12' = 'ASV'; '1' = 'KJV'; '111' = 'NIV' }
        foreach ($id in $known.Keys) {
            $hit = $versions | Where-Object { "$($_.id)" -eq $id } | Select-Object -First 1
            if ($hit) {
                $abbr = if ($hit.abbreviation) { $hit.abbreviation } else { '?' }
                Write-Verdict ("id $id  ($($known[$id]))") $true "visible as '$abbr'"
            }
            else {
                Write-Verdict ("id $id  ($($known[$id]))") $false 'not visible to this key'
            }
        }

        Write-Host ''
        $web = $versions | Where-Object { "$($_.id)" -eq '206' } | Select-Object -First 1
        if ($web) {
            Write-Host 'YVN19 rule 2 closes YES: the shipped default resolves on this key.' -ForegroundColor Green
            Write-Host 'Confirm separately whether this key has accepted any portal agreement -' -ForegroundColor DarkGray
            Write-Host 'a key that has accepted one does not prove a fresh key would see it.' -ForegroundColor DarkGray
        }
        else {
            if ($response.next_page_token) {
                Write-Host 'INCONCLUSIVE: 206 is absent from page one and more pages exist.' -ForegroundColor Yellow
                Write-Host 'Page through next_page_token before recording a NO.' -ForegroundColor Yellow
            }
            else {
                Write-Host 'YVN19 rule 2 closes NO for this key: WEBUS (206) is not visible.' -ForegroundColor Red
            }
            Write-Host 'ABS20.2 rule 2 is then load-bearing - this deployment must set' -ForegroundColor DarkGray
            Write-Host 'DefaultTranslation explicitly, and YVN4 needs revisiting.' -ForegroundColor DarkGray
        }

        $results.youVersion = @{
            total    = $versions.Count
            hasWebUs = [bool]$web
            versions = @($versions | ForEach-Object { @{ id = $_.id; abbreviation = $_.abbreviation; title = $_.title } })
        }
    }
    catch {
        Write-Host ("Request failed: {0}" -f $_.Exception.Message) -ForegroundColor Red
        Write-Host 'A 401 means the app key is rejected; a 422 that survived the retry means' -ForegroundColor DarkGray
        Write-Host 'neither language_ranges spelling was accepted (YVN7 rule 2).' -ForegroundColor DarkGray
        $results.youVersion = @{ error = $_.Exception.Message }
    }
}

if ($OutFile) {
    $results | ConvertTo-Json -Depth 6 | Out-File -FilePath $OutFile -Encoding utf8
    Write-Host ''
    Write-Host "Full results written to $OutFile (contains no credentials)." -ForegroundColor Cyan
}

Write-Host ''
Write-Host 'Record what this returns in the design before acting on it - APB23, APB28.3, YVN19.' -ForegroundColor Cyan
