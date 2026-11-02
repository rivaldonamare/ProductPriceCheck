$webClient = New-Object System.Net.WebClient
$webClient.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; ElectricalStoreAI-dev)")
try {
    $task = $webClient.OpenRead("https://media.githubusercontent.com/media/onnx/models/main/README.md")
    $response = $task.GetAwaiter().GetResult()
    Write-Output ("STATUS: " + $response.StatusCode)
} catch {
    Write-Output ("FAILED: " + $_.Exception.Message)
}
