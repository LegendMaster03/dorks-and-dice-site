# Productization and legacy cleanup goals

This refactor should be treated as the major structural rebuild for the foreseeable future. The target is not merely a cleaner private repository; the architecture should be left in a state that could plausibly be packaged, documented, licensed, or sold without requiring another foundational rewrite.

## Productization goals

The reusable framework should be separable from this deployment and from the specific Professional and Dorks & Dice modes. Product-specific and deployment-specific code should have obvious boundaries.

A prospective purchaser or integrator should be able to determine, primarily from the repository structure and public contracts:

- what the reusable framework provides
- how to register a normal site mode
- how framework fallback behavior works
- how Trusted Preview/global administration works
- how to register and host tools
- how optional plugins/modules integrate without replacing tools
- how content, identity, routing, permissions, and deployment composition relate
- which configuration is installation-specific
- which extension points are supported and stable

The framework should avoid requiring consumers to edit central switch statements or copy implementation-specific code when adding a mode or tool.

The rebuild should also leave room for a later packaging/licensing decision without forcing that decision during the refactor. A public/open framework, commercial framework, dual-license model, hosted product, or sale of the codebase should all remain technically feasible.

## Repository and API expectations

Productization favors:

- explicit public contracts over hidden conventions
- narrow dependency boundaries
- stable string identifiers rather than deployment-specific enums where extensibility matters
- declarative registration where it improves discoverability
- dependency injection rather than global service lookup
- framework defaults for the common case
- explicit special cases for fallback and Trusted Preview
- examples and synthetic contract tests that prove third-party extension is possible
- deployment configuration that can be replaced without modifying framework or mode code

The repository should not expose personal deployment assumptions as reusable framework behavior.

## Legacy compatibility inventory

Legacy behavior that exists only because of earlier site implementations should be identified during the migration rather than carried forward invisibly.

Each item should be classified as one of:

1. **generalize** - the behavior represents a legitimate reusable need and should become a supported framework/content feature;
2. **isolate** - compatibility must remain for existing data that can not yet be migrated, but it should live behind a clearly named legacy/compatibility boundary;
3. **migrate and remove** - existing data/content can be converted to the modern representation and the compatibility code deleted.

Legacy compatibility must not remain embedded in otherwise generic framework or mode code without an explicit reason.

## Retired legacy article format

The former article system used individual HTML pages. All articles from that format have been converted to the current Markdown-backed content system, and the old article versions have been removed.

The legacy HTML article format therefore has no compatibility requirement. Any renderer, parser, route, view, migration shim, test fixture, or other implementation that exists solely to support the retired HTML article format should be deleted when identified rather than generalized or preserved behind a compatibility layer.

Markdown-backed content is the supported article representation going forward. Normal Markdown rendering and sanitization are part of the current content system and are not legacy HTML compatibility merely because the resulting rendered output is HTML.

## ConsoleVariations presentation holdover

The Professional mode stylesheet currently contains an article-specific rule for the ConsoleVariations "Free the Bees" icon:

```css
/* Article-specific treatment for the ConsoleVariations Free the Bees icon. */
.consolevariations-bee .content-detail-logo-link img[src$="consolevariations-bee.png"] {
  ...
}
```

This selector is a remaining presentation artifact from the old article implementation, not a reason to preserve support for the retired HTML article format. The current Markdown article itself is already the authoritative content representation.

The selector may remain temporarily while the presentation/theme boundary is being refactored, but it must not become part of the reusable framework or remain an unexplained article-specific exception indefinitely. When the constrained content-presentation/theme mechanism is revisited, migrate this treatment to that supported mechanism and remove the named selector. Do not add arbitrary per-article CSS execution to preserve it.

## One-rebuild principle

When a structural decision is already known to be required for a reusable/productizable architecture, prefer addressing it during this refactor rather than intentionally preserving a design that will require a second major rebuild shortly afterward.

This does not mean implementing speculative features. It means establishing durable boundaries and extension contracts now while avoiding unnecessary product features that have no current requirement.
