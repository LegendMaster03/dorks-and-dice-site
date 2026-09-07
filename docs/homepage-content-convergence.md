# Homepage content convergence

## Status

Homepage content is a shared framework concern and now uses the same database-backed content/revision architecture as other editable site content.

This requirement is independent of the mode-definition persistence decision documented elsewhere. Normal-mode registration is consumed through the stable-ID registry boundary; homepage documents are selected through the content system.

A content document tagged `homepage` and visible to the active normal mode takes precedence over that mode's compiled home module. Exactly one visible `homepage` document may resolve for a normal mode; multiple candidates are rejected as an invalid composition rather than resolved by incidental source/query order.

Both current replacement homepages now exist in the Local authoring database:

- `professional-home`
- `dorks-and-dice-home`

The Local database was cleaned after the Professional article/media migration so these are the only Local pages intentionally retained. They remain Local while final parity and runtime validation is performed; the live External database therefore still relies on the compiled normal-mode homepage fallbacks.

The major implementation work is complete:

- database-backed homepage resolution is active;
- managed homepage media is available;
- the Dorks & Dice Minecraft block is implemented as the `minecraft-server-status` plugin rather than inline page code;
- Discord remains an installed page component;
- `/site.txt`, `/llms.txt`, and sitemap generation consume current public database-backed content at runtime;
- media can be replaced in place without changing its managed URL.

The remaining homepage work is operational convergence: verify presentation/functional parity against the current Local documents, verify managed-media replacement and crawler output with the real source composition, publish each homepage to External, and then delete the corresponding compiled/file-backed fallback implementation.

## Current systems being converged

### Professional

The legacy Professional fallback still uses `ResumeViewModel` and repository data from:

```text
Content/Resume/resume.json
```

That subsystem is retained only because `professional-home` has not yet been promoted to the live External source. It is not the intended permanent authored-content architecture.

The replacement `professional-home` document owns the directly authored homepage content and its managed media. Experience and Project entries remain in the shared database-backed content catalog and are composed through the `professional-portfolio` plugin instead of being duplicated into homepage Markdown.

Do not copy Experience or Project records into homepage Markdown merely to remove the compiled fallback. That would create duplicate editable sources of truth.

Before promotion, verify the Local homepage against the legacy page, including structured profile/contact/education/awards/skills/leadership information, managed résumé/credential media, links, and layout. The managed résumé PDF is the primary acceptance case for replace-media-in-place: updating its bytes must retain the same asset key/URL and immediately affect the homepage.

After `professional-home` is promoted and verified live, remove the compiled Professional home/resume subsystem and any static media that exists only to support it. Presentation-owned theme assets such as the Professional stylesheet/favicon remain separate from authored content when still required.

### Dorks & Dice

The legacy Dorks & Dice fallback contains authored copy and integrations that have now been given reusable boundaries:

- community/campaign copy belongs in the database-backed homepage;
- Discord is provided by the `discord-widget` plugin;
- Minecraft live status is provided by the `minecraft-server-status` plugin.

The homepage can invoke Minecraft status with:

```markdown
{{minecraft-server-status}}
```

Host, port, protocol version, query timeout, and cache policy remain deployment-owned configuration. Authored content selects the installed component but can not redirect its network query.

Minecraft is the only currently supported game-server status implementation. Hytale live status remains outside this refactor cycle; a static Hytale mention may remain where it accurately describes the community.

After `dorks-and-dice-home` reaches presentation/functional parity, promote it to External, verify it live, and remove the compiled Dorks & Dice homepage fallback.

## Shared homepage contract

A normal mode exposes a database-backed homepage by creating a content document with:

```text
tag: homepage
visible mode: <stable mode id>
body format: markdown
```

The content source is selected through the same content-source registry/context behavior as the rest of the content system, so Local/External composition and mode visibility remain consistent with normal content requests.

This intentionally does **not** add a homepage identifier to `SiteModeDefinition`. The content system resolves the current normal mode's homepage by visibility and context.

## Current runtime behavior

Homepage resolution is:

```text
request
  -> active normal mode
  -> configured content sources for that mode
  -> exactly one visible document tagged `homepage`
       -> render shared database-backed homepage
       -> if absent, use existing compiled normal-mode home module
  -> framework fallback home when no normal mode implementation applies
```

The compiled normal-mode path is temporary migration support for the two existing sites. Framework fallback remains separate and is not a normal site mode.

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

## Professional fallback retirement constraint

The legacy resume JSON contains structured records such as:

- profile/header data;
- contact links;
- education entries;
- awards;
- skill categories;
- leadership entries.

`resume.json` and the related service/view-model/Razor fallback may be deleted only after every still-needed field has been represented by the database-backed homepage/content system, managed media, or legitimate presentation/theme configuration and the External homepage has been verified live.

Experience and Projects remain independent content records and must not be collapsed into the homepage merely to eliminate the fallback.

## Remaining validation and retirement sequence

1. Verify Local `professional-home` presentation and functional parity.
2. Replace the managed Local résumé PDF in place and verify stable asset URL/key plus new bytes.
3. Verify Professional `/site.txt`, `/llms.txt`, sitemap, and managed-media links under the real Local/External source composition.
4. Promote `professional-home` to External with the normal safe single-page Move operation.
5. Verify the live Professional homepage, managed media, text endpoints, sitemap, and redirects.
6. Remove the compiled/file-backed Professional homepage/resume fallback and now-orphaned static media.
7. Verify Local `dorks-and-dice-home`, including Minecraft/Discord behavior and layout parity.
8. Verify Dorks & Dice text/sitemap output under the real source composition.
9. Promote `dorks-and-dice-home` when ready and verify it live.
10. Remove the compiled Dorks & Dice homepage fallback.
11. Rerun the complete automated suite and final live smoke/security checks.

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
