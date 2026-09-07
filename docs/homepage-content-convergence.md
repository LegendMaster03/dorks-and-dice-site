# Homepage content convergence

## Status

Homepage content is a shared framework concern and uses the same database-backed content/revision architecture as other editable site content.

This requirement is independent of the mode-definition persistence decision documented elsewhere. Normal-mode registration is consumed through the stable-ID registry boundary; homepage documents are selected through the content system.

A content document tagged `homepage` and visible to the active normal mode takes precedence over any registered home module. Exactly one visible `homepage` document may resolve for a normal mode; multiple candidates are rejected as an invalid composition rather than resolved by incidental source/query order.

Both normal-mode homepages have now been promoted to the live External content source:

- `professional-home` — External revision 42 at the September 7, 2026 validation checkpoint;
- `dorks-and-dice-home` — External revision 34 at the same checkpoint.

The Local authoring database is empty after those moves. The ordinary single-page Move operation preserved the homepages and their managed media in External.

The Dorks & Dice database-backed homepage passed authenticated live parity validation, including desktop/mobile presentation, light/dark themes, Discord, live Minecraft status, Hytale copy, link destinations, public text exporters, sitemap behavior, and mode isolation. Its compiled normal-mode homepage fallback has therefore been retired. The Dorks & Dice presentation module and theme assets remain application-owned presentation concerns.

The Professional database-backed homepage is live but still requires an authored-content revision before its compiled fallback can be retired. The remaining defects are limited to stable anchors, curated collection ordering, résumé download behavior, and external-link attributes. They can all be expressed through the existing Markdown/component contracts; no Professional-specific rendering subsystem is required.

The framework fallback remains. It is a framework runtime state, not a normal site mode, and is intentionally independent of the two database-backed normal-mode homepages.

## Completed shared implementation

The permanent homepage architecture now includes:

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

## Professional live repair

Authenticated live validation found these functional differences in External `professional-home` revision 42.

### Stable section anchors

The following IDs are referenced by homepage navigation/detail-page back links but are absent from the rendered page:

```text
experience-section
projects-section
skills-section
education-section
honors-section
leadership-section
```

Use normal Markdown generic attributes on the corresponding headings, for example:

```markdown
## Experience {#experience-section}
```

The shared Markdown pipeline already preserves authored `id` attributes.

### Experience order

Use this curated order in the Experience collection invocation:

```text
seniorproject,experiencecaspenterprises,experiencetechnologyservices,experiencecybersecurityteam,experiencesimlab,experiencewiredworks,skyblivion,skywind
```

### Project order

Use this order:

```text
xngine,pythonfinanceanalytics,personalmultimodewebsite,seniorproject,directedindependentstudy,skyblivion,skywind,simlabexpo,dndtools
```

Retain:

```text
featured-first="true"
```

That groups featured records first while preserving the curated order within the featured and non-featured groups.

### Résumé download

The current managed résumé URL is:

```text
/content/media/eded0c44dade448a93515f73349a410a/kyle-resume.pdf
```

The authored link should retain that managed URL and add the generic `download` attribute, for example:

```markdown
[Download Résumé](/content/media/eded0c44dade448a93515f73349a410a/kyle-resume.pdf){download}
```

### External contact and credential links

LinkedIn and GitHub should use:

```text
target="_blank" rel="me noopener noreferrer"
```

Credential PDFs and official-record links should use:

```text
target="_blank" rel="noopener noreferrer"
```

Email and telephone links should retain their normal direct behavior.

Once a new External Professional revision corrects these items and passes live validation, the compiled Professional homepage/resume subsystem can be deleted.

## Professional fallback retirement constraint

The remaining legacy Professional fallback uses `ResumeViewModel` and repository data from:

```text
Content/Resume/resume.json
```

It also keeps static copies of résumé/contact/credential media alive. This subsystem is transitional and must not become the permanent source of authored homepage data.

Before deleting it, every still-needed field must be represented by the live database-backed homepage/content system, managed media, or legitimate presentation/theme configuration. Experience and Projects remain independent database content records and must not be copied into homepage Markdown merely to eliminate the fallback.

The Professional presentation stylesheet/favicon remain presentation-owned assets. The static headshot also has an application-level metadata/structured-data reference that must be migrated to an appropriate managed/presentation source before that physical static copy is deleted.

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

The former `DorksAndDiceHomeModule` and its compiled homepage Razor view are no longer part of the permanent normal-mode architecture. The Dorks & Dice presentation module, branding partials, stylesheet, and other legitimate presentation-owned resources remain.

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
  -> framework fallback only when no normal-mode homepage/module applies
```

A deployment may still register an `ISiteModeHomeModule` for a mode that intentionally requires application-owned homepage behavior, but the two current normal sites no longer need that mechanism as authored-content storage. The framework fallback module remains registered separately.

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

The Dorks & Dice homepage has no attached managed media; its dynamic blocks are plugin-backed components.

The authoring usage query must ensure each configured content store is on the current storage schema before querying `content_page_asset_dependency`. This keeps dependency inspection valid for newly created/disposable SQLite stores as well as initialized live stores.

## Remaining convergence sequence

1. Save a new External `professional-home` revision with the anchor, ordering, download, and link-attribute fixes above.
2. Revalidate the live Professional homepage, `/resume`, managed media, `/site.txt`, `/llms.txt`, sitemap, and mode isolation.
3. Retire `ProfessionalHomeModule`, the file-backed `resume.json`/resume view-model service path, and the compiled Professional homepage Razor/partials.
4. Migrate the remaining application-level static-headshot metadata/structured-data dependency, then remove static Professional media that becomes genuinely orphaned. Keep real theme/favicon assets.
5. Re-audit generic/shared code for remaining legacy normal-mode homepage coupling and stale documentation/tests.
6. Run the complete automated suite, CI, final live smoke tests, and the final security/penetration-test pass.

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
