# Homepage content convergence

## Status

Homepage content is a shared framework concern and should use the same database-backed content/revision architecture as other editable site content.

This requirement is independent of the open decision documented in `mode-definition-decision.md` about whether a normal mode's structural definition ultimately lives in compiled C# or runtime data.

The first migration boundary is now implemented: a content document tagged `homepage` and visible to the active normal mode takes precedence over the mode's compiled home module. Existing compiled home modules remain as fallbacks while the two current homepages are migrated.

Exactly one visible `homepage` document may resolve for a normal mode. Multiple candidates are treated as an invalid composition instead of choosing one by incidental source/query order.

## Current systems being converged

### Professional

The Professional homepage currently uses `ResumeViewModel`.

Most resume header/contact/education/awards/skills/leadership data comes from the repository file:

```text
Content/Resume/resume.json
```

`ResumePageContentBuilder` reads this file from disk at runtime. Editing it therefore remains a deployment/file change rather than ordinary on-site content authoring.

Experience and project entries are already loaded from the shared database-backed content catalog using the `experience` and `project` contexts. This means the Professional homepage is already partly migrated to the general content system.

The remaining resume JSON is strongly structured. Do not flatten it into Markdown merely to remove the JSON file if doing so would lose useful structure or make editing worse. Its eventual representation should be chosen as part of the general page/content model.

### Dorks & Dice

The Dorks & Dice homepage is currently a Razor view containing both authored text and executable/dynamic presentation:

- community and campaign copy;
- static cards and calls to action;
- Minecraft live status;
- Discord widget integration.

The authored copy should migrate into database-backed content.

Minecraft status and Discord integration are behavior, not authored content storage. They should become reusable installed components/capabilities that a homepage can compose rather than remaining a reason for Dorks & Dice to have a separate homepage content system.

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

That is sufficient for static application-owned markup but not yet a complete general page-component system. Dynamic components such as Minecraft status, tool surfaces, or other request-aware/async blocks should not be forced into this interface without first defining their lifecycle, authorization, configuration, sanitization, and failure behavior.

The next page-composition design should remain compatible with both possible mode-definition strategies. A page should compose installed capabilities; it should not require a mode-specific Razor homepage solely to access those capabilities.

## Resume migration constraint

The existing resume JSON contains structured records such as:

- profile/header data;
- contact links;
- education entries;
- awards;
- skill categories;
- leadership entries.

Projects and experience have already demonstrated that structured Professional homepage sections can consume the general content catalog.

Before removing `resume.json`, determine which of the remaining structures should become:

1. ordinary page Markdown;
2. reusable structured content records/components;
3. media/asset metadata;
4. presentation/theme configuration.

Do not preserve a special file-driven resume subsystem solely for compatibility, but also do not discard useful structured data merely to make the homepage one Markdown blob.

## Definition of completion for this refactor cycle

The homepage convergence work is complete when:

- both normal modes obtain authored homepage content from database-backed content sources;
- ordinary homepage edits are possible through the site editor and require no application restart;
- no mode needs a separate file-backed authored homepage store;
- shared homepage selection is stable-ID/mode-context based and works with per-mode content databases;
- dynamic homepage behavior is supplied through reusable installed capabilities rather than hard-coded authored text in mode-specific Razor;
- existing Professional and Dorks & Dice presentation remains functionally equivalent after migration;
- the design does not require deciding whether the normal mode definition itself is compiled or persisted runtime data.
