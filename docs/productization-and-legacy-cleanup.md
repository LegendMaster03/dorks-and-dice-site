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
2. **isolate** - compatibility must remain for existing content, but it should live behind a clearly named legacy/compatibility boundary;
3. **migrate and remove** - existing data/content can be converted to the modern representation and the compatibility code deleted.

Legacy compatibility must not remain embedded in otherwise generic framework or mode code without an explicit reason.

## Known legacy article-specific CSS

The Professional mode stylesheet currently contains an article-specific rule for the ConsoleVariations "Free the Bees" icon:

```css
/* Article-specific treatment for the ConsoleVariations Free the Bees icon. */
.consolevariations-bee .content-detail-logo-link img[src$="consolevariations-bee.png"] {
  ...
}
```

This is a holdover from the former article system where articles were individual HTML pages. The modern content system uses Markdown, and this special CSS exists to preserve the presentation of one logo that could not be represented directly by the migrated Markdown content.

This named selector must not become part of the reusable framework or remain indefinitely in the global Professional stylesheet merely because the old article depended on it.

During the content-architecture portion of the refactor, determine the smallest generic mechanism that can represent the required presentation. Possible outcomes include a supported content/logo presentation option, a constrained Markdown/content directive, or another content-owned presentation hook. The exact mechanism should be chosen from actual content requirements rather than by introducing arbitrary per-article CSS execution.

After that article is migrated to the generic mechanism, remove the `consolevariations-bee` compatibility rule.

## One-rebuild principle

When a structural decision is already known to be required for a reusable/productizable architecture, prefer addressing it during this refactor rather than intentionally preserving a design that will require a second major rebuild shortly afterward.

This does not mean implementing speculative features. It means establishing durable boundaries and extension contracts now while avoiding unnecessary product features that have no current requirement.
