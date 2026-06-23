param(
    [string]$XmlPath = "./plewiba.xml",
    [string]$OutputCsvPath = "../../PL-bic-lookup.csv"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Array {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Array]) {
        return $Value
    }

    return @($Value)
}

function Normalize-Bic {
    param([string]$RawValue)

    if ([string]::IsNullOrWhiteSpace($RawValue)) {
        return $null
    }

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $RawValue.ToUpperInvariant(),
        "\b([A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?)\b"
    )

    if (-not $match.Success) {
        return $null
    }

    $bic = $match.Groups[1].Value
    if ($bic.Length -eq 8) {
        return "$($bic)XXX"
    }

    if ($bic.Length -eq 11) {
        return $bic
    }

    return $null
}

function Get-OptionalPropertyValue {
    param(
        [object]$Object,
        [string]$PropertyName
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

$resolvedXmlPath = Resolve-Path -LiteralPath $XmlPath
$scriptDirectory = Split-Path -Parent $PSCommandPath
$resolvedOutputPath = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory $OutputCsvPath))

Write-Host "Reading XML from: $resolvedXmlPath"
[xml]$document = Get-Content -LiteralPath $resolvedXmlPath -Raw -Encoding UTF8

$rowsByLookupKey = [ordered]@{}

$institutions = Get-Array -Value $document.Instytucje.Instytucja
foreach ($institution in $institutions) {
    $institutionName = [string]$institution.NazwaInstytucji
    $units = Get-Array -Value $institution.Jednostka

    foreach ($unit in $units) {
        $settlementNumbers = Get-Array -Value (Get-OptionalPropertyValue -Object $unit -PropertyName "NumerRozliczeniowy")

        foreach ($settlementNumber in $settlementNumbers) {
            $lookupKey = ([string]$settlementNumber.NrRozliczeniowy).Trim()
            if ($lookupKey -notmatch "^\d{8}$") {
                continue
            }

            $bic = $null
            $bicValues = Get-Array -Value (Get-OptionalPropertyValue -Object $settlementNumber -PropertyName "KodyBIC")
            foreach ($bicValue in $bicValues) {
                $normalizedBic = Normalize-Bic -RawValue ([string]$bicValue)
                if (-not [string]::IsNullOrWhiteSpace($normalizedBic)) {
                    $bic = $normalizedBic
                    break
                }
            }

            if ([string]::IsNullOrWhiteSpace($bic)) {
                continue
            }

            if (-not $rowsByLookupKey.Contains($lookupKey)) {
                $rowsByLookupKey[$lookupKey] = [PSCustomObject]@{
                    CountryCode = "PL"
                    LookupKey = $lookupKey
                    Bic = $bic
                    BankCode = $lookupKey
                    BranchCode = ""
                    BankName = $institutionName
                    Source = "Polish bank register (plewiba.xml)"
                }
            }
        }
    }
}

$rows = $rowsByLookupKey.Values | Sort-Object -Property LookupKey

$targetDirectory = Split-Path -Parent $resolvedOutputPath
if (-not (Test-Path -LiteralPath $targetDirectory)) {
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
}

$rows | Export-Csv -LiteralPath $resolvedOutputPath -NoTypeInformation -Encoding UTF8
Write-Host "Wrote $($rows.Count) PL BIC rows to: $resolvedOutputPath"