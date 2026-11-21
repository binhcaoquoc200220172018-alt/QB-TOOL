Add-Type -AssemblyName "System.IO.Compression.FileSystem"
$excelPath = "C:\Users\ADMIN\Desktop\TEST TOOL_RAI CONG NGANG\CHAY MO HINH\TEST CONG TRON\DU_LIEU_CONG_NAP_TOOL.xlsx"
$tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.Guid]::NewGuid().ToString())
[System.IO.Compression.ZipFile]::ExtractToDirectory($excelPath, $tempDir)

$ss = @()
if (Test-Path "$tempDir\xl\sharedStrings.xml") {
    $ssXml = [xml](Get-Content "$tempDir\xl\sharedStrings.xml")
    foreach ($si in $ssXml.sst.si) {
        # si might have t directly or r/t
        if ($si.t) { $ss += $si.t }
        elseif ($si.r) { $ss += ($si.r | ForEach-Object { $_.t }) -join "" }
        else { $ss += "" }
    }
}

$sheetXml = [xml](Get-Content "$tempDir\xl\worksheets\sheet3.xml")
$rows = $sheetXml.worksheet.sheetData.row
foreach ($r in $rows | Select-Object -First 10) {
    $vals = @()
    foreach ($c in $r.c) {
        $v = $c.v
        if ($c.t -eq "s") {
            $v = $ss[[int]$v]
        }
        $vals += "$($c.r): $v"
    }
    Write-Host "Row $($r.r): $($vals -join ' | ')"
}

Remove-Item -Recurse -Force $tempDir
