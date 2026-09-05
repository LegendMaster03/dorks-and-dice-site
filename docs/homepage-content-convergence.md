# Homepage content convergence

## Status

Homepage content is a shared framework concern and should use the same database-backed content/revision architecture as other editable site content.

This requirement is independent of the open decision documented in `mode-definition-decision.md` about whether a normal mode's structural definition ultimately lives in compiled C# or runtime data.

The first migration boundary is now implemented: a content document tagged `homepage` and visible to the active normal mode takes precedence over the mode's compiled home module. Existing compiled home modules remain as fallbacks while the two current homepages are migrated.

Exactly one visible `homepage` document may resolve for a normal mode. Multiple candidates are treated as an invalid composition instead of choosing one by incidental source/query order.

Markdown migration inputs now live under `docs/homepage-migration/`. These files are temporary migration aids, not a second runtime content source. Dorks & Dice has an activation-ready Markdown draft. The Professional draft currently contains only the sections that can move without duplicating the database-backed Experience and Projects collections.

## Current systems being converged

### Professional

The Professional homepage currently uses `ResumeViewModel`.

Most resume header/contact/education/awards/skills/leadership data comes from the repository file:

```text
Content/Resume/resume.json
```

`ResumePageContentBuilder` reads this file from disk at runtime. Editing it therefore remains a deployment/file change rather than ordinary on-site content authoring.

Experience and project entries are already loaded from the shared database-backed content catalog using the `experience` and `project` contexts. This means the Professional homepage is already partly migrated to the general content system.

The remaining resume JSON is strongly structured. The first Markdown migration draft converts the parts that are naturally authored page content while intentionally leaving Experience and Projects out until the page-composition mechanism can consume their existing database records directly.

Do not copy Experience or Project records into homepage Markdown merely to make the compiled view disappear. That would create duplicate editable sources of truth.

### Dorks & Dice

The Dorks & Dice homepage is currently a Razor view containing both authored text and executable/dynamic presentation:

- community and campaign copy;
- static cards and calls to action;
- Minecraft live status;
- Discord widget integration.

The authored copy should migrate into database-backed content.

The initial Dorks & Dice Markdown conversion is now staged in `docs/homepage-migration/dorks-and-dice.md`. It preserves the authored community/campaign/server-history copy while omitting the live Minecraft status and Discord iframe from the authored body.

Minecraft/Hytale monitoring should be extracted as a Tool rather than rebuilt as a homepage-specific component. The host can expose that Tool through the existing tool registration/security boundaries, and ordinary Markdown can link to it once the Tool exists. This keeps server monitoring operational behavior out of the page-content system.

The Discord widget remains a separate runtime-integration question. It should not force the homepage itself back into a mode-specific Razor implementation.

## Shared homepage contract

A normal mode can expose a homepage through the existing content architecture by creating a content document with:

```text
tag: homepage
visible mode: <stable mode id>
body format: markdown
```

The content source is selected through the same `ContentSourceRegistry` / `GetSourcesForContext` behavior used by the rest of the content system, so existing per-mode database/source composition is preserved.

This intentionally does **not** add a homepage identifier to `SiteModeDefinition`. Doing so now would unnecessarily bias the unresolved compiled-mode vs runtime-mode decision. The content system can resolve the current mode's homepage by visibility/context under either representation.

## Current migration behavior

Homepage resolution is now:

```text
request
  -> active normal mode
  -> shared content sources for that mode
  -> visible document tagged `homepage`
       -> render shared database-backed homepage
       -> if absent, use existing compiled home module
  -> framework fallback home when no normal mode implementation applies
```

The fallback path is temporary migration support for the two existing sites, not a second permanent homepage storage architecture.

## Page composition is a separate problem

The existing Markdown renderer supports application-owned `{{directive}}` blocks, but the current directive contract is intentionally small and synchronous:

```text
IContentDirectiveRenderer
    Name
    Render()
```

That is sufficient for static application-owned markup but not yet a complete general page-component system. Dynamic components, tool surfaces, or other request-aware/async blocks should not be forced into this interface without first defining their lifecycle, authorization, configuration, sanitization, and failure behavior.

The next page-composition design should remain compatible with both possible mode-definition strategies. A page should compose installed capabilities; it should not require a mode-specific Razor homepage solely to access those capabilities.

For Professional, this composition problem is specifically required to retain the existing database-backed Experience and Projects collections without duplicating them into Markdown.

## Resume migration constraint

The existing resume JSON contains structured records such as:

- profile/header data;
- contact links;
- education entries;
- awards;
- skill categories;
- leadership entries.

Projects and experience have already demonstrated that structured Professional homepage sections can consume the general content catalog.

The current migration approach is:

1. move directly authored identity/summary/contact/education/awards/skills/leadership copy into Markdown;
2. preserve Experience and Projects in their current database records;
3. add a general composition path for those existing collections;
4. remove `resume.json` only when every remaining field has either moved into Markdown, become structured content, become media metadata, or become presentation/theme configuration.

Do not preserve a special file-driven resume subsystem solely for compatibility, but also do not discard useful structured data merely to make the homepage one Markdown blob.

## Definition of completion for this refactor cycle

The homepage convergence work is complete when:

- both normal modes obtain authored homepage content from database-backed content sources;
- ordinary homepage edits are possible through the site editor and require no application restart;
- no mode needs a separate file-backed authored homepage store;
- shared homepage selection is stable-ID/mode-context based and works with per-mode content databases;
- dynamic homepage behavior is supplied through reusable installed capabilities or Tools rather than hard-coded authored text in mode-specific Razor;
- game-server monitoring no longer depends on the Dorks & Dice homepage implementation;
- existing Professional and Dorks & Dice presentation remains functionally equivalent after migration;
- the design does not require deciding whether the normal mode definition itself is compiled or persisted runtime data.
