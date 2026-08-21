$lines = Get-Content "C:\projects\EvidenciasSQA\src\EvidenciasSQA.Visor\src\EvidenciasSQA.Core\Export\WordReportBuilder.cs"
for ($i = 408; $i -lt 420; $i++) {
    Write-Host "Line $($i+1): '$($lines[$i])'"
}