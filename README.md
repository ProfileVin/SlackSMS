# SlackSms: two-way Slack ↔ SMS bridge

A small ASP.NET Core (.NET 10) service that links **one Slack channel** with **one SignalWire phone number**.

| Direction | How it works |
|---|---|
| Slack → SMS | Slack Events API calls `POST /slack/events` → message is queued → sent via SignalWire to `SignalWire:DestinationNumber` |
| SMS → Slack | SignalWire messaging webhook calls `POST /signalwire/sms` → posted to the channel as `SMS from +1555…: text` |

What's filtered:
- Bot messages, edits, deletes and joins in Slack are never sent as SMS. The bridge's own posts are bot messages, so an inbound SMS never loops back out.
- Slack retries are deduplicated by `event_id`.
- Both webhooks verify signatures and reject unsigned or forged requests with `401`.

`GET /health` returns `ok`.

## Project layout

The code follows Clean Architecture. Dependencies point inward only, and `ArchitectureTests` enforces this.

| Project | Contains | Depends on |
|---|---|---|
| `src/SlackSms.Domain` | `ChannelMessage`, `InboundSms`, `SmsBody` (formatting and the 1600-char limit) | nothing |
| `src/SlackSms.Application` | Use cases (`QueueChannelMessage`, `ForwardMessageToSms`, `RelayInboundSms`) and the ports they use (`ISmsSender`, `IChannelClient`, `IUserDirectory`, `IOutboundMessageQueue`, `IEventDeduplicator`) | Domain |
| `src/SlackSms.Infrastructure` | Slack and SignalWire clients, signature verifiers, the in-memory queue, caching and the background SMS worker | Application |
| `src/SlackSms` | Web host: `Program.cs`, the MVC controllers (`Controllers/`) and the signature filters (`Filters/`) | Application, Infrastructure |

Each controller is thin: it maps the request, calls one use case and maps the result. Signature checks run
as `[VerifySlackSignature]` / `[VerifySignalWireSignature]` filters before the body is bound.

## 1. Slack app

1. Go to https://api.slack.com/apps, choose **Create New App**, then **From scratch**.
2. Under **OAuth & Permissions**, add these **Bot Token Scopes**:
   - `chat:write`
   - `channels:history` (for a private channel, use `groups:history` instead)
   - `users:read` (sender name prefix and `@person` mentions)
   - `channels:read` and `groups:read` (`#channel` links; private channels also need the bot to be a member)
   - `usergroups:read` (`@group` mentions)

   Without these the bridge still works, but SMS shows `@U0123…` IDs or `#channel` instead of names.
3. Click **Install to Workspace** and copy the **Bot User OAuth Token** (`xoxb-…`) into `Slack:BotToken`.
4. Under **Basic Information**, copy the **Signing Secret** into `Slack:SigningSecret`.
5. In Slack, open the channel, invite the bot (`/invite @YourApp`), and copy the channel ID into `Slack:ChannelId`. The ID is under channel details → About, and looks like `C0123456789`.
6. Start the bridge and make it publicly reachable (see step 3). Then, under **Event Subscriptions**:
   - Turn it on and set the Request URL to `https://<public-host>/slack/events`. Slack should show **Verified**.
   - Under **Subscribe to bot events**, add `message.channels`, or `message.groups` for a private channel.
   - Save, and reinstall the app if Slack asks you to.

## 2. SignalWire

1. Buy a phone number with SMS capability. Its E.164 form (`+15551234567`) goes into `SignalWire:FromNumber`.
2. On the number's settings, under **Messaging Settings**:
   - Set "Handle messages using" to **LaML Webhooks**.
   - Set "When a message comes in" to `https://<public-host>/signalwire/sms`, method **POST**.
   - Put that exact URL into `SignalWire:WebhookPublicUrl` as well. The signature check is computed over it, so it must match character for character.
3. On the **API** page, copy these values:
   - The Space name (`example` in `example.signalwire.com`) into `SignalWire:Space`.
   - The **Project ID** into `SignalWire:ProjectId`.
   - The **API Token** into `SignalWire:ApiToken`. It needs Messaging scope.
   - The **Signing Key** into `SignalWire:SigningKey`.
4. Set `SignalWire:DestinationNumber` to the phone that should receive the Slack messages.

If you already have these values from another project that uses SignalWire's Twilio-compatible settings, they map like this:

| Other project (`SmsSettings`) | This project |
|---|---|
| `AccountSid` | `SignalWire:ProjectId` |
| `AuthToken` | `SignalWire:ApiToken` |
| `SignalWireSpaceUrl` (e.g. `example.signalwire.com`) | `SignalWire:Space` (full host is accepted) |
| `FromNumber` | `SignalWire:FromNumber` |
| none | `SignalWire:SigningKey`, copied from the SignalWire API page |

Incoming webhooks must pass two checks: the signature must be valid (checked with the Signing Key), and the posted `AccountSid` must equal `SignalWire:ProjectId`.

> In the US, carriers require A2P 10DLC (or toll-free verification) registration before SMS gets delivered reliably. Complete it in the SignalWire dashboard.

## 3. Configuration and running locally

All settings live in `src/SlackSms/appsettings.json`:

```json
"Slack": {
  "BotToken": "xoxb-...",
  "SigningSecret": "...",
  "ChannelId": "C0123456789",
  "IncludeSenderName": true
},
"SignalWire": {
  "Space": "example.signalwire.com",
  "ProjectId": "...",
  "ApiToken": "...",
  "SigningKey": "...",
  "FromNumber": "+15550000001",
  "DestinationNumber": "+15550000002",
  "WebhookPublicUrl": "https://<public-host>/signalwire/sms",
  "ValidateSignature": true
}
```

Then run `dotnet run --project src/SlackSms`.

> `appsettings.json` contains live credentials. Don't commit it to a shared or public repository.

The app listens on `http://localhost:5178`. Expose it with a tunnel, for example `ngrok http 5178` or `devtunnel host -p 5178 --allow-anonymous`, and use the tunnel's HTTPS host in steps 1 and 2.

Environment variables with double underscores (e.g. `SignalWire__ApiToken`) override values from `appsettings.json` if you need that later. The app refuses to start while any required setting is missing.

| Setting | Required | Notes |
|---|---|---|
| `Slack:BotToken` | yes | `xoxb-…` |
| `Slack:SigningSecret` | yes | |
| `Slack:ChannelId` | yes | Channel ID, not the name |
| `Slack:IncludeSenderName` | no | Default `true`: prefix SMS with the sender's Slack display name, e.g. `Alvin: hi` (needs `users:read`) |
| `SignalWire:Space` | yes | `example` or `example.signalwire.com` |
| `SignalWire:ProjectId` / `ApiToken` | yes | |
| `SignalWire:SigningKey` | yes | Validates incoming webhooks |
| `SignalWire:FromNumber` | yes | Your SignalWire number |
| `SignalWire:DestinationNumber` | yes | Receives every Slack message |
| `SignalWire:WebhookPublicUrl` | yes | Exact webhook URL configured in SignalWire |
| `SignalWire:ValidateSignature` | no | Default `true`. Turn it off only to troubleshoot |

## Tests

```powershell
dotnet test SlackSms.slnx
```

### Testing before the Slack app is ready

Installing a Slack app can wait on a workspace admin, but the Slack → SMS direction needs no Slack
credentials — only a correctly signed request, which `scripts/Send-TestSlackEvent.ps1` produces.

1. Fill in `SignalWire:SigningKey`, `FromNumber` and `DestinationNumber`, and put any string in
   `Slack:SigningSecret`. `Slack:BotToken`, `Slack:ChannelId` and `SignalWire:WebhookPublicUrl` ship
   with placeholders that let the app start; the bot token is unused in this direction.
2. Run the app, then:

   ```powershell
   .\scripts\Send-TestSlackEvent.ps1 -Text "hello from the bridge"
   ```

   The log should show `Queued Slack event … for SMS` then `SMS sent to …`, and the SMS arrives on
   `DestinationNumber`. The script signs with the secret and channel from `appsettings.json` unless
   you pass `-SigningSecret` or `-ChannelId`.

For the inbound direction, point the SignalWire number's webhook at your tunnel and text it. Until a
real `xoxb-` token is in place the request passes the signature check and then fails at the Slack
call (`Failed to post SMS … to Slack`, HTTP 500), which still confirms the tunnel, the webhook URL
and the signing key. A `Rejected SignalWire request with invalid signature` warning instead means
`SignalWire:WebhookPublicUrl` doesn't match the URL configured on the number.

To test the inbound direction without texting the number, `scripts/Send-TestSmsWebhook.ps1` posts a
correctly signed SignalWire webhook straight to the running app (this needs a real `Slack:BotToken`
and `Slack:ChannelId`):

```powershell
.\scripts\Send-TestSmsWebhook.ps1 -Length 256              # generated body of exactly 256 chars, ending in END
.\scripts\Send-TestSmsWebhook.ps1 -Length 256 -TextMedia   # text arrives as a .txt attachment, like some phones send it
.\scripts\Send-TestSmsWebhook.ps1 -Text "hello"
```

## Troubleshooting

- **Slack message doesn't arrive as SMS.** Check that the bot is in the channel, that the event subscription (`message.channels` / `message.groups`) is saved, and look for `Queued Slack event` or SignalWire errors in the logs.
- **Every SignalWire webhook returns 401.** `SignalWire:WebhookPublicUrl` doesn't exactly match the URL set on the number, or the Signing Key is wrong.
- **Slack returns `not_in_channel` on inbound SMS.** Invite the bot to the channel.
- **Messages are delayed or dropped.** Nothing is persisted, so SMS still waiting in the queue are lost on restart. Failed sends are retried 3 times, then logged as errors.
