Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead('Soporte_Evidencias.docx')
$entry = $zip.Entries | Where-Object {$_.FullName -eq 'word/document.xml'}
$stream = $entry.Open()
$reader = New-Object System.IO.StreamReader($stream)
$content = $reader.ReadToEnd()
$reader.Close()
$stream.Close()
$zip.Dispose()
Set-Content -Path 'document.xml' -Value $content -Encoding utf8