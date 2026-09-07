# Homepage content convergence

## Status

Homepage content is a shared framework concern and uses the same database-backed content/revision architecture as other editable site content.

This requirement is independent of the mode-definition persistence decision documented elsewhere. Normal-mode registration is consumed through the stable-ID registry boundary; homepage documents are selected through the content system.

A content document tagged `homepage` and visible to the active normal mode takes precedence over any registered home module. Exactly one visible `homepage` document may resolve for a normal mode; multiple candidates are rejected as an invalid composition rather than resolved by incidental source/query order.

Both normal-mode homepages have been promoted to the live External content source:

- `professional-home`;
- `dorks-and-dice-home`.

The Local authoring database is empty after those moves. The ordinary single-page Move operation preserved the homepages, their histories, and their managed media in External.

The Dorks & Dice database-backed homepage passed authenticated live parity validation, including desktop/mobile presentation, light/dark themes, Discord, live Minecraft status, Hytale copy, link destinations, public text exporters, sitemap behavior, and mode isolation. Its compiled normal-mode homepage fallback has been retired.

The Professional database-backed homepage was repaired through authored Markdown/component metadata to restore stable section anchors, curated Experience/Project ordering, résumé download behavior, and external-link behavior. The repaired live page was visually approved. Its compiled/file-backed homepage and résumé fallback has therefore been retired on the cleanup branch.

The framework fallback remains. It is a framework runtime state, not a normal site mode, and is intentionally independent of the two database-backed normal-mode homepages.

## Completed shared implementation

The permanent homepage architecture includes:

- database-backed homepage resolution through the normal content catalog and source composition;
- managed homepage media with stable URLs;
- replace-media-in-place without changing the stable asset key, file name, URL, or page references;
- page-dependency inspection before media replacement;
- inline PDF previews and explicit Open PDF actions in authoring media surfaces;
- the Dorks & Dice Minecraft block as the `minecraft-server-status` plugin;
- Discord as an installed page component;
- Professional Experience and Project collections through the `professional-portfolio` plugin;
- runtime `/site.txt` and `/llms.txt` representations;
- database-aware sitemap generation;
- source-neutral site-mode registration and stable-ID mode ownership.

Authored content therefore no longer requires an application rebuild merely to change a homepage, its prose, its current managed media, or its collection composition.

## Professional convergence

The live Professional homepage preserves the behavior that was previously encoded in the file-backed résumé/homepage implementation through generic authored contracts.

Stable section IDs are authored directly in Markdown:

```text
experience-section
projects-section
skills-section
education-section
honors-section
leadership-section
```

For example:

```markdown
## Experience {#experience-section}
```

The Experience collection uses the curated order:

```text
seniorproject,experiencecaspenterprises,experiencetechnologyservices,experiencecybersecurityteam,experiencesimlab,experiencewiredworks,skyblivion,skywind
```

The Project collection uses:

```text
xngine,pythonfinanceanalytics,personalmultimodewebsite,seniorproject,directedindependentstudy,skyblivion,skywind,simlabexpo,dndtools
```

with:

```text
featured-first="true"
```

The managed résumé remains at its stable media URL and uses the generic Markdown `download` attribute. LinkedIn/GitHub and credential/official-record links use authored generic link attributes rather than Professional-specific rendering logic.

The former `ProfessionalHomeModule`, `Content/Resume/resume.json`, resume-specific view-model/service path, compiled Professional homepage view/partials, and their duplicate static résumé/contact/credential media are no longer required by the runtime architecture and are removed by the Professional retirement cleanup.

`/resume` remains a Professional-owned alias through `ISiteModeHomeService`; it does not require or imply a separate résumé content store.

The Professional presentation stylesheet and favicon remain presentation-owned assets. The default social/meta image and structured-data `Person.image` now use the managed Professional headshot instead of a duplicate static headshot.

## Dorks & Dice convergence

The Dorks & Dice homepage is authoritative in External. Its dynamic behavior is supplied by reusable boundaries:

- community/campaign copy is authored in `dorks-and-dice-home`;
- Discord is provided by the `discord-widget` plugin;
- Minecraft live status is provided by the `minecraft-server-status` plugin.

The homepage can invoke Minecraft status with:

```markdown
{{minecraft-server-status}}
```

Host, port, protocol version, query timeout, and cache policy remain deployment-owned configuration. Authored content selects the installed component but can not redirect its network query.

Minecraft is the only currently supported game-server status implementation. Hytale live status remains outside this refactor cycle; static Hytale copy may remain where it accurately describes the community.

The former `DorksAndDiceHomeModule` and its compiled homepage Razor view are no longer part of the normal-mode architecture. The Dorks & Dice presentation module, branding partials, stylesheet, and other legitimate presentation-owned resources remain.

## Shared homepage contract

A normal mode exposes a database-backed homepage by creating a content document with:

```text
tag: homepage
visible mode: <stable mode id>
body format: markdown
```

The content source is selected through the same content-source registry/context behavior as the rest of the content system, so Local/External composition and mode visibility remain consistent with normal content requests.

This intentionally does **not** add a homepage identifier to `SiteModeDefinition`. The content system resolves the current normal mode's homepage by visibility and context.

Permanent normal-mode resolution is therefore:

```text
request
  -> active normal mode
  -> configured content sources for that mode
  -> exactly one visible document tagged `homepage`
       -> render shared database-backed homepage
  -> framework fallback when no normal-mode homepage/module applies
```

A future deployment may still register an `ISiteModeHomeModule` for a mode that intentionally requires application-owned homepage behavior, but the two current normal sites no longer need that mechanism as authored-content storage. The framework fallback module remains registered separately.

## Page composition

The shared page composer lets authored Markdown invoke installed executable capabilities without permitting authored code.

Current examples include:

```text
content-collection
professional-portfolio
discord-widget
minecraft-server-status
```

This is the preferred boundary for compact dynamic homepage behavior. A homepage should not require a mode-specific Razor implementation merely to invoke an installed capability.

For Professional, this preserves the database-backed Experience and Project collections without duplicating their records.

For Dorks & Dice, Minecraft status remains a plugin because it is a compact in-process query/presentation. Substantial interactive applications with independent lifecycle/data boundaries remain Tools.

## Media validation

Authenticated validation after promotion confirmed that all nine Professional homepage managed assets are present in External and report `External/professional-home` as a page dependency. The résumé and credential PDFs render through the inline authoring preview and open normally in a separate tab. Replace controls are available both in the central External media library and on the Professional homepage's Media Dependencies surface.

The duplicate source-tree résumé PDF, credential PDFs, contact icons, and headshot were retained only by the legacy Professional fallback. After the presentation metadata/structured-data headshot reference was switched to the managed asset, those static duplicates became removable. The Professional stylesheet and favicon remain because they are presentation-owned rather than authored media.

The Dorks & Dice homepage has no attached managed media; its dynamic blocks are plugin-backed components.

The authoring usage query ensures each configured content store is on the current storage schema before querying `content_page_asset_dependency`. This keeps dependency inspection valid for newly created/disposable SQLite stores as well as initialized live stores.

## Remaining convergence sequence

1. Run the complete automated suite and CI against the Professional fallback-retirement cleanup.
2. Confirm Professional `/`, `/resume`, `/articles`, managed media, `/site.txt`, `/llms.txt`, sitemap, and mode isolation remain correct without the legacy fallback/static duplicates.
3. Confirm Dorks & Dice `/`, Discord, Minecraft, and route isolation remain correct after the shared cleanup.
4. Re-audit source for stale legacy normal-mode homepage/resume dependencies and obsolete `/site-modes/professional/...` media references.
5. Merge the validated cleanup into `refactor/site-mode-modules`.
6. Run the final refactor-wide smoke/security/source-isolation pass before requesting explicit approval to merge the refactor into `main`.

## Definition of completion for this refactor cycle

Homepage convergence is complete when:

- both normal modes obtain authored homepage content from the live database-backed content source;
- ordinary homepage edits through the site editor require no application rebuild/restart;
- no normal mode retains a separate file-backed authored homepage store;
- homepage selection is stable-ID/mode-context based and respects source composition;
- dynamic behavior is supplied through reusable installed plugins/capabilities;
- the Dorks & Dice homepage uses `minecraft-server-status` for live Minecraft data;
- managed homepage media can be maintained through the content-media system rather than source-tree copies;
- runtime text/sitemap output follows current database content;
- Professional and Dorks & Dice presentation remains functionally equivalent after migration;
- both compiled normal-mode homepage fallbacks are removed after live verification;
- Hytale live-status support is not required for completion;
- framework fallback and Trusted Preview remain framework concerns rather than normal-mode content.
