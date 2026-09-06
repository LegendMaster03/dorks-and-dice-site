# Homepage content convergence

## Status

Homepage content is a shared framework concern and should use the same database-backed content/revision architecture as other editable site content.

This requirement is independent of the open decision documented in `mode-definition-decision.md` about whether a normal mode's structural definition ultimately lives in compiled C# or runtime data.

The first migration boundary is implemented: a content document tagged `homepage` and visible to the active normal mode takes precedence over the mode's compiled home module. Existing compiled home modules remain as temporary fallbacks until the database-backed versions have verified presentation and functional parity.

Exactly one visible `homepage` document may resolve for a normal mode. Multiple candidates are treated as an invalid composition instead of choosing one by incidental source/query order.

The two current database-backed homepages are substantially migrated. The remaining convergence work is presentation parity, especially Markdown-generated vertical spacing, plus replacing the legacy Dorks & Dice inline Minecraft status block with the installed `minecraft-server-status` plugin component.

## Current systems being converged

### Professional

The legacy Professional homepage uses `ResumeViewModel`.

Most resume header/contact/education/awards/skills/leadership data historically came from the repository file:

```text
Content/Resume/resume.json
```

`ResumePageContentBuilder` reads this file from disk at runtime. Editing it therefore remains a deployment/file change rather than ordinary on-site content authoring.

Experience and project entries are already loaded from the shared database-backed content catalog using the `experience` and `project` contexts. The article-backed homepage composes those existing records through the `professional-portfolio` plugin rather than duplicating them into Markdown.

Do not copy Experience or Project records into homepage Markdown merely to make the compiled view disappear. That would create duplicate editable sources of truth.

The remaining Professional migration requirement is user-visible parity with the legacy resume homepage. Generic article heading margins must not create additional spacing when headings already live inside resume/card layout wrappers.

### Dorks & Dice

The legacy Dorks & Dice homepage Razor view contains authored text plus two executable integrations:

- community and campaign copy;
- static cards and calls to action;
- Minecraft live status;
- Discord widget integration.

The authored copy belongs in database-backed content. Discord is already provided by the `discord-widget` plugin.

Minecraft live status is now provided by the `minecraft-server-status` plugin. The existing Minecraft protocol/status-query implementation remains the working backend; the plugin supplies registration and a parameterless page component:

```markdown
{{minecraft-server-status}}
```

Host, port, protocol version, query timeout, and cache policy remain deployment-owned configuration. Authored content can select the installed status component but can not redirect its network query.

Minecraft is the only currently supported game-server status implementation. Hytale support was explored previously, and similar endpoints may plausibly exist, but no sufficiently documented or reliable endpoint was found. The known path would have required modifying the Hytale server, so Hytale status integration was deferred as low priority. It is not a requirement for this migration.

A static Hytale mention in authored community/server history is independent of live status support and may remain where it accurately describes the community.

## Shared homepage contract

A normal mode can expose a homepage through the existing content architecture by creating a content document with:

```text
tag: homepage
visible mode: <stable mode id>
body format: markdown
```

The content source is selected through the same `ContentSourceRegistry` / `GetSourcesForContext` behavior used by the rest of the content system, so existing per-mode database/source composition is preserved.

This intentionally does **not** add a homepage identifier to `SiteModeDefinition`. The content system resolves the current mode's homepage by visibility/context regardless of whether normal mode registration ultimately remains compiled or becomes data-driven.

## Current migration behavior

Homepage resolution is:

```text
request
  -> active normal mode
  -> shared content sources for that mode
  -> visible document tagged `homepage`
       -> render shared database-backed homepage
       -> if absent, use existing compiled home module
  -> framework fallback home when no normal mode implementation applies
```

The compiled normal-mode fallback path is temporary migration support for the two existing sites, not a second permanent homepage storage architecture. The framework fallback remains separate and is not a normal site mode.

## Page composition

The shared page composer allows authored Markdown to compose installed executable capabilities without permitting authored code.

Current examples include:

```text
content-collection
professional-experience / professional-projects presentations

discord-widget
minecraft-server-status
```

This is the preferred boundary for compact dynamic homepage behavior. A homepage should not require a mode-specific Razor implementation merely to invoke an installed capability.

For Professional, this composition path retains the existing database-backed Experience and Projects collections without duplicating their records.

For Dorks & Dice, Minecraft status is deliberately a plugin rather than a Tool: it is a compact in-process query/presentation with no meaningful standalone workflow. Substantial interactive applications with independent lifecycle/data boundaries remain Tools.

## Resume migration constraint

The existing resume JSON contains structured records such as:

- profile/header data;
- contact links;
- education entries;
- awards;
- skill categories;
- leadership entries.

Projects and experience have already demonstrated that structured Professional homepage sections can consume the general content catalog.

The migration approach is:

1. move directly authored identity/summary/contact/education/awards/skills/leadership copy into database-backed homepage content or other appropriate structured content;
2. preserve Experience and Projects in their current database records;
3. consume those collections through general page composition;
4. remove `resume.json` only when every remaining field has either moved into database-backed content, become media metadata, or become presentation/theme configuration.

Do not preserve a special file-driven resume subsystem solely for compatibility, but also do not discard useful structured data merely to make the homepage one Markdown blob.

## Definition of completion for this refactor cycle

The homepage convergence work is complete when:

- both normal modes obtain authored homepage content from database-backed content sources;
- ordinary homepage edits are possible through the site editor and require no application restart;
- no normal mode needs a separate file-backed authored homepage store;
- shared homepage selection is stable-ID/mode-context based and works with per-mode content databases;
- dynamic homepage behavior is supplied through reusable installed plugins/capabilities rather than hard-coded authored text in mode-specific Razor;
- the Dorks & Dice homepage uses `minecraft-server-status` for live Minecraft data;
- Markdown-generated layout does not add duplicate vertical spacing relative to the intended legacy presentation;
- existing Professional and Dorks & Dice presentation remains functionally equivalent after migration;
- compiled normal-mode homepage fallbacks are removed only after that parity is verified;
- Hytale live-status support is not required for completion;
- the design does not require deciding whether the normal mode definition itself is compiled or persisted runtime data.
