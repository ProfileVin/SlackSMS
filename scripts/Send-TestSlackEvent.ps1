<#
.SYNOPSIS
    Sends a correctly signed, fake Slack message event to a locally running bridge.

.DESCRIPTION
    Exercises the Slack -> SMS direction without a Slack workspace: the payload is signed the same
    way Slack signs its requests (HMAC-SHA256 over "v0:{timestamp}:{body}"), so the app accepts it
    as genuine and sends a real SMS through SignalWire.

    The app must already be running, and Slack:ChannelId must match -ChannelId or nothing is
    forwarded. Defaults for the signing secret and channel are read from appsettings.json.

.EXAMPLE
    .\scripts\Send-TestSlackEvent.ps1 -Text "hello from the bridge"
#>
[CmdletBinding()]
param(
    [string] $Text = "Test message from Send-TestSlackEvent.ps1",
    [string] $SigningSecret,
    [string] $ChannelId,
    [string] $BaseUrl = "http://localhost:5178"
)

$ErrorActionPreference = "Stop"

$settingsPath = Join-Path $PSScriptRoot "..\src\SlackSms\appsettings.json"
if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if (-not $SigningSecret) { $SigningSecret = $settings.Slack.SigningSecret }
    if (-not $ChannelId)     { $ChannelId     = $settings.Slack.ChannelId }
}

if (-not $SigningSecret) {
    throw "No signing secret. Set Slack:SigningSecret in appsettings.json, or pass -SigningSecret. Any string works for local testing, as long as the app uses the same one."
}
if (-not $ChannelId) {
    throw "No channel id. Set Slack:ChannelId in appsettings.json, or pass -ChannelId."
}

# A new id every run: the app ignores an event id it has already seen for an hour.
$eventId = "Ev" + [Guid]::NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant()
$textJson = $Text | ConvertTo-Json

$body = '{"type":"event_callback","event_id":"' + $eventId +
        '","event":{"type":"message","channel":"' + $ChannelId +
        '","user":"U0LOCALTEST","ts":"1700000000.000100","text":' + $textJson + '}}'

$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()

$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [Text.Encoding]::UTF8.GetBytes($SigningSecret)
try {
    $hash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("v0:${timestamp}:${body}"))
} finally {
    $hmac.Dispose()
}
$signature = "v0=" + (($hash | ForEach-Object { $_.ToString("x2") }) -join "")

Write-Host "POST $BaseUrl/slack/events" -ForegroundColor Cyan
Write-Host "  channel $ChannelId, event $eventId"

try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/slack/events" `
        -Method Post `
        -ContentType "application/json; charset=utf-8" `
        -Headers @{ "X-Slack-Request-Timestamp" = $timestamp; "X-Slack-Signature" = $signature } `
        -Body ([Text.Encoding]::UTF8.GetBytes($body)) `
        -UseBasicParsing
} catch {
    $status = $null
    if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }

    if ($status -eq 401) {
        throw "The app rejected the signature (401). Slack:SigningSecret and the secret used here don't match."
    }
    throw "Request failed: $($_.Exception.Message). Is the app running at $BaseUrl? Check $BaseUrl/health."
}

Write-Host "Accepted ($($response.StatusCode)). Watch the app log for 'Queued Slack event' then 'SMS sent to'." -ForegroundColor Green
Write-Host "No 'Queued' line means Slack:ChannelId in appsettings.json is not '$ChannelId'." -ForegroundColor DarkGray
