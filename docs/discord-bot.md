# Discord bot integration

The Discord bot is a generic Site plugin. It owns Discord transport, bot credentials, managed Discord-object persistence, ownership verification, desired-state reconciliation, and cleanup. Site modes contribute desired guild workspaces through `IDiscordWorkspaceProjectionSource`; the generic bot does not contain campaign, event, or other mode-specific business rules.

## Operating model

The current integration is REST-driven and does not need the Discord Gateway for synchronization. It intentionally does not request message-content, presence, or guild-member event streams.

A workspace projection can contain bot-managed roles and bot-managed channels. The generic reconciler records the Discord IDs of objects it created and only modifies or deletes objects it owns. It does not adopt unrelated roles or channels merely because they have the same name.

Synchronization is desired-state based. If a Discord request fails temporarily, a later pass can converge the guild without requiring the originating Site-domain event to be replayed.

Managed channels currently support text channels, voice channels, categories, and parent-category placement. Mode capabilities can therefore add temporary or persistent channel structures without moving Discord-specific transport logic into the mode.

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

The installation link requests only the Discord permissions the current integration needs:

- **Manage Roles**, for bot-managed role creation and assignment;
- **Manage Channels**, for bot-managed channel/category creation and cleanup.

Do not grant Administrator merely for this integration. Discord role hierarchy still applies, so the bot's server role must remain above the roles it manages.

## Identity boundary

Discord identity remains global. Assigning a bot-managed role to a Site user requires:

1. a global Discord account link;
2. an active Discord activation for the relevant Site mode;
3. mode-owned state that includes that Site user in the projected role.

If a user disconnects Discord from a mode or globally unlinks Discord, the next successful reconciliation removes assignments previously managed for that identity.

## Dorks & Dice

Dorks & Dice contributes campaign-derived Discord state today. Other Dorks & Dice capabilities can add independent workspace projection sources later. For example, a Dorks & Dice-wide event can contribute event roles, categories, text channels, or voice channels to the main server without changing the generic Discord plugin or campaign domain.

### Main Dorks & Dice server

The main server is a special, mode-owned integration. It is not a user-managed server binding.

Its guild ID comes from:

```text
ModeConnections:dorks-and-dice:discord:ResourceId
```

The Dorks & Dice workspace currently manages:

- `Linked Account` for Discord users whose Site account is linked and enabled for the Dorks & Dice mode;
- `Player` for users who are Players in at least one active campaign;
- `DM` for users who are DMs in at least one active campaign;
- one `Campaign: <name>` role for each active campaign, assigned to all active members of that campaign.

`Linked Account` is contributed by a separate Dorks & Dice-wide projection source rather than by campaign logic. It is independent of campaign membership and is removed when the user's Discord identity is no longer active for the Dorks & Dice mode.

`Player` and `DM` are independent. A user who holds both kinds of campaign membership receives both roles. There is no combined Player/DM role.

Because the main server is a mode-owned workspace, future Dorks & Dice-wide features are not limited to campaigns. Separate mode capabilities can project their own bot-managed roles and channels into the same guild.

### User-managed Discord servers

An authenticated user may also configure additional Discord servers they own. These bindings are separate from the main server and are owned by that Site account.

The setup flow is:

1. link and enable Discord for the Dorks & Dice mode;
2. install the Dorks & Dice bot in a Discord server;
3. enter the Discord server ID on the Dorks & Dice **Discord Servers** page;
4. the Site verifies, through the installed bot, that the linked Discord account is the guild owner;
5. choose the campaign scope for that server.

A user-managed server can use one of three campaign scopes:

- **One campaign** — exactly one active campaign where the server owner is currently a DM;
- **Selected campaigns** — one or more explicitly selected active campaigns where the owner is currently a DM;
- **All campaigns I DM** — dynamic; the projected set follows all active campaigns where the owner currently has the DM role.

The server owner can update the scope later or remove the binding entirely.

For a single-campaign server, the campaign projection manages only `Player` and `DM`; a redundant campaign-name role is not created.

For selected-campaign and all-DM-campaign servers, the projection manages `Player`, `DM`, and one `Campaign: <name>` role per included campaign so members can still be distinguished by campaign.

If the configuring user stops being a DM of a selected campaign, that campaign immediately drops out of the desired projection on the next synchronization pass. The saved selection is retained, but it is not authoritative while the owner lacks DM authority.

Removing a user-managed server binding causes the generic reconciler to remove only the roles and channels that it previously created for that projection source.

## Future mode capabilities

The workspace contract intentionally does not encode campaigns or events. A mode capability can independently contribute desired roles/channels to any guild that mode legitimately owns or configures. This allows later Dorks & Dice features such as:

- convention or community-event roles;
- event categories and channels;
- temporary staff or participant spaces;
- other mode-wide Discord organization.

Those features should remain mode-owned business logic. The generic Discord plugin remains responsible for translating desired workspace state into Discord API operations.
