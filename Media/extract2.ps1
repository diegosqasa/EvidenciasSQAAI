Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead('Soporte_Evidencias.docx')
$entries = $zip.Entries | Where-Object {$_.FullName -eq 'word/document.xml'}
foreach ($e in $entries) {
    $stream = $e.Open()
    $reader = New-Object System.IO.StreamReader($stream)
    $content = $reader.ReadToEnd()
    $reader.Close()
    $stream.Close()
    Set-Content -Path 'document.xml' -Value $content -Encoding utf8
}
$zip.Dispose()