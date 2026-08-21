$lines = Get-Content "C:\projects\EvidenciasSQA\src\EvidenciasSQA.Visor\src\EvidenciasSQA.Core\Export\WordReportBuilder.cs"
# Fix line 411 (index 410) - replace single '}' with proper closing braces
$lines[410] = "            }"
# Insert two lines after line 411
$newLines = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $newLines.Add($lines[$i])
    if ($i -eq 410) {
        $newLines.Add("        }")
        $newLines.Add("    }")
    }
}
Set-Content -Path "C:\projects\EvidenciasSQA\src\EvidenciasSQA.Visor\src\EvidenciasSQA.Core\Export\WordReportBuilder.cs" -Value ($newLines -join "`n") -Encoding utf8
Write-Host "Fixed!"