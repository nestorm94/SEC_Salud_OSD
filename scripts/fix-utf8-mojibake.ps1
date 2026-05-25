# Corrige mojibake (ej. GobernaciÃ³n -> Gobernacion con acentos correctos)
$enc1252 = [System.Text.Encoding]::GetEncoding(1252)
$encUtf8 = New-Object System.Text.UTF8Encoding $false
$root = Join-Path $PSScriptRoot "..\public"

Get-ChildItem -Path $root -Recurse -Include *.html,*.css | ForEach-Object {
    $raw = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
    if ($raw.Contains([char]0x00C3) -or $raw.Contains([char]0x00E2)) {
        $fixed = $encUtf8.GetString($enc1252.GetBytes($raw))
        [System.IO.File]::WriteAllText($_.FullName, $fixed, $encUtf8)
        Write-Host "Fixed: $($_.FullName)"
    }
}

Write-Host "Listo."
