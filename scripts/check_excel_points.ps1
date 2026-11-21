$json = Get-Content 'C:\Users\ADMIN\AppData\Roaming\Autodesk\Revit\Addins\2025\DV_TOOL_HTKT\UserConfig\LastSessionConfig.json' -Raw | ConvertFrom-Json
Write-Host "ExcelFilePath: " $json.ExcelFilePath
Write-Host "CulvertRowOverrides: " ($json.CulvertRowOverrides | ConvertTo-Json -Depth 4)
