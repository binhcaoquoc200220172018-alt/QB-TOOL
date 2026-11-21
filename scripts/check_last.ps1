$lines = Get-Content 'C:\Users\ADMIN\.gemini\antigravity\brain\3829e2fd-b6e6-46de-9443-93a35207030f\.system_generated\logs\transcript.jsonl'
for ($i = $lines.Count - 1; $i -ge 0; $i--) {
    $line = $lines[$i]
    if ($line.Contains('"USER_INPUT"')) {
        $json = $line | ConvertFrom-Json
        Write-Host "FOUND USER_INPUT at step $($json.step_index):"
        Write-Host $json.content
        break
    }
}
