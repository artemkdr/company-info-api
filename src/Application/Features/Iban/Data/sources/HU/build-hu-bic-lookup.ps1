param(
    [string]$CsvPath = "./sht.csv",
    [string]$OutputCsvPath = "../../HU-bic-lookup.csv"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-Text {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $normalized = $Value.Trim()
    $normalized = [System.Text.RegularExpressions.Regex]::Replace($normalized, "\s+", " ")
    return $normalized.TrimEnd('.')
}

function Get-CanonicalBankName {
    param([string]$Value)

    $normalized = Normalize-Text $Value
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return ""
    }

    if ($normalized -like 'Magyar Államkincstár*') {
        return 'Magyar Államkincstár'
    }

    $legalEntityMatch = [System.Text.RegularExpressions.Regex]::Match(
        $normalized,
        '^(?<base>.*?\b(?:Bank Nyrt|Bank Zrt|Bank AG|Bank N\.V\.|Bank Plc|Nyrt|Zrt|Plc|UAB|AD|Fióktelepe|Fióktelep|Ltd|hf))\.?($|\s)',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
    if ($legalEntityMatch.Success) {
        return (Normalize-Text $legalEntityMatch.Groups['base'].Value)
    }

    $dottedMatch = [System.Text.RegularExpressions.Regex]::Match(
        $normalized,
        '^(?<base>[^,.]{6,})\.\s+.+$'
    )
    if ($dottedMatch.Success) {
        return (Normalize-Text $dottedMatch.Groups['base'].Value)
    }

    return $normalized
}

function Get-BankNameScore {
    param([string]$Value)

    $score = 0
    if ($Value -match '\b(Bank|Nyrt|Zrt|Plc|UAB|AD|SA|AG|Fióktelepe|Fióktelep)\b') {
        $score += 100
    }

    if ($Value -match '\b(fiók|r\.|régió|Partner|Center|Centrum|Technikai|Önkorm|Deviz|LAFO|B-hitel|ITB|Lakossági|Külföldiek|Belföldiek|Privát|Call Center|Back-Office|Osztály|Központi|Cashless|számlavezetés|szla|üzletági)\b') {
        $score -= 80
    }

    if ($Value -match '\d') {
        $score -= 30
    }

    return $score
}

function Get-BestBankName {
    param([object[]]$Rows)

    $rankedCandidates = $Rows |
        ForEach-Object { Get-CanonicalBankName $_.'Name of the branch office' } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Group-Object |
        ForEach-Object {
            [PSCustomObject]@{
                Name = $_.Name
                Score = Get-BankNameScore $_.Name
                Count = $_.Count
                Length = $_.Name.Length
            }
        } |
        Sort-Object -Property @{ Expression = 'Score'; Descending = $true }, @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Length'; Descending = $false }, @{ Expression = 'Name'; Descending = $false }

    if ($null -eq $rankedCandidates) {
        return ""
    }

    return ($rankedCandidates | Select-Object -First 1).Name
}

$scriptDirectory = Split-Path -Parent $PSCommandPath
$resolvedCsvPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory $CsvPath))
$resolvedOutputPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory $OutputCsvPath))

Write-Host "Reading CSV from: $resolvedCsvPath"
$rows = Import-Csv -LiteralPath $resolvedCsvPath

$canonicalNameByBic = @{}
$rows |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_.'BIC code') } |
    Group-Object { Normalize-Text $_.'BIC code' } |
    ForEach-Object {
        $canonicalNameByBic[$_.Name] = Get-BestBankName -Rows $_.Group
    }

$lookupRows = $rows |
    Where-Object {
        $_.'Branch office code' -match '^\d{8}$' -and
        -not [string]::IsNullOrWhiteSpace($_.'BIC code')
    } |
    Group-Object { $_.'Branch office code'.Substring(0, 3) } |
    Sort-Object Name |
    ForEach-Object {
        $groupRows = $_.Group
        $bankCode = $_.Name
        $bics = @(
            $groupRows |
                ForEach-Object { Normalize-Text $_.'BIC code' } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Sort-Object -Unique
        )

        if ($bics.Count -eq 0) {
            return
        }

        if ($bics.Count -gt 1) {
            throw "Multiple BIC values found for HU bank code ${bankCode}: $($bics -join ', ')"
        }

        [PSCustomObject]@{
            CountryCode = "HU"
            LookupKey = $bankCode
            Bic = $bics[0].ToUpperInvariant()
            BankCode = $bankCode
            BranchCode = ""
            BankName = $canonicalNameByBic[$bics[0]]
            Source = "Hungarian branch office register (sht.csv)"
        }
    }

$targetDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $targetDirectory)) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
}

$lookupRows | Export-Csv -LiteralPath $resolvedOutputPath -NoTypeInformation -Encoding UTF8
Write-Host "Wrote $($lookupRows.Count) HU BIC rows to: $resolvedOutputPath"