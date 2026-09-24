# Discord bot integration

The Discord bot is a generic Site plugin. It owns Discord transport, bot credentials, managed-role persistence, reconciliation, and safe cleanup. Normal Site modes contribute desired Discord state through `IDiscordRoleProjectionSource`; the generic bot does not contain campaign or other mode-specific business rules.

## Operating model

The bot uses Discord's REST API and does not require the Gateway for role synchronization. It intentionally does not request message content, presence, or guild-member event streams.

The bot manages only roles whose Discord role IDs are recorded in the Site Identity database. It does not rename, assign, or remove unrelated server roles.

Role synchronization is desired-state based. A failed request can be retried on the next pass without requiring a campaign event to be replayed.

## Configuration

`DiscordBot` is disabled by default.

```json
{
  "DiscordBot": {
    "Enabled": true,
    "ClientId": "application-id",
    "TokenFile": "/run/secrets/discord_bot_token",
    "ReconcileIntervalSeconds": 60,
    "RequestTimeoutSeconds": 15
  }
}
```

`ClientId` may be omitted when the same Discord application is already configured under `AccountLinks:Discord:ClientId`.

The token may be supplied as `Token` for local development or, preferably in production, through `TokenFile`. The production deployment should mount the bot token as a Docker secret. The bot token is separate from the OAuth client secret used for account linking.

The bot needs the Discord **Manage Roles** permission. Discord's role hierarchy still applies: the bot's own server role must be above every role it is expected to create or manage.

## Dorks & Dice behavior

Dorks & Dice contributes two kinds of guild projection.

### General Dorks & Dice server

The general guild is the existing mode-level Discord resource from:

```text
ModeConnections:dorks-and-dice:discord:ResourceId
```

The bot manages:

- `Player` when the linked Site user is a player in at least one active campaign;
- `DM` when the linked Site user is a DM in at least one active campaign;
- one `Campaign: <name>` role for each active campaign, assigned to every active Site member of that campaign.

`Player` and `DM` are independent. A user who is both a player and a DM receives both roles; there is no combined role.

### Dedicated campaign server

A campaign DM may optionally bind one Discord guild to that campaign. The same Discord application/bot can be installed in many campaign guilds, but one dedicated guild can belong to only one Dorks & Dice campaign.

A dedicated campaign guild receives only:

- `Player`, based on that campaign's Player membership;
- `DM`, based on that campaign's DM membership.

It does not receive the general server's per-campaign role because the guild itself already represents the campaign.

Campaign guild bindings are stored in Dorks & Dice mode storage. Removing a binding or deleting its campaign makes the generic reconciler clean up roles that it previously owned in that guild. Archiving a campaign leaves the binding in place but removes the active campaign roles until the campaign is restored.

## Identity boundary

Discord identity remains global. Role projection requires:

1. a global Discord account link;
2. an active Discord activation for the Dorks & Dice mode;
3. current Dorks & Dice domain membership that requires the role.

The dedicated campaign-guild projection uses the same Dorks & Dice mode activation as the general guild. If a user disconnects Discord from the Dorks & Dice mode or globally unlinks Discord, the next reconciliation removes bot-managed roles from that user's known assignments.

## Campaign-server setup

On a campaign's settings page, a DM can:

1. install the bot in the intended campaign Discord server;
2. enter that Discord server ID;
3. update or remove the dedicated-server binding later.

The install link requests only the bot scope with Manage Roles permission. The Discord server ID is deployment/domain data, not a credential.
