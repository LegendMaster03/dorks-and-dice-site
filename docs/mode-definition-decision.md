# Open decision: compiled vs runtime-defined normal modes

## Status

This decision is intentionally open during the current refactor.

The refactor may continue on boundaries that are valid under either model, but it must not make the current C# mode definitions or a future database-backed mode catalog irreversible until the purpose of the framework is settled.

The current `Modes/Professional` and `Modes/DorksAndDice` directories are useful migration boundaries because they expose deployment-specific behavior. They are not, by themselves, a decision that normal modes must remain compiled modules.

## Shared assumptions under either option

The comparison is specifically about where the **mode definition and structural composition** live. It is not a choice between static and dynamic site content.

Both options are expected to support:

- database-backed editable content;
- independently selectable/configurable themes;
- media managed through the content/media system;
- runtime tool registration and per-mode tool enablement;
- per-mode content-source composition, including a dedicated database where desired;
- mode-scoped moderation/editor permissions;
- homepage and ordinary page content that can change without rebuilding or restarting the service;
- deployment-specific host/domain and secret configuration outside the intrinsic mode definition.

The existing article infrastructure should continue evolving into a general content/page system. Articles are one use of that system, not its architectural identity.

Campaigns should not force a generic framework abstraction. The existing Tool model provides a natural way for campaign functionality to become an installed application capability that Dorks & Dice enables, regardless of how modes themselves are defined.

## Option A: compiled C# mode definition

A normal mode has a compiled module or definition in source, for example:

```text
Modes/
    Professional/
        ProfessionalMode.cs
    DorksAndDice/
        DorksAndDiceMode.cs
```

The source definition owns structural behavior that is genuinely part of that mode. Content, themes, media, tool configuration, and other normal editorial state remain outside the compiled definition.

### Strengths

- Strong source-level discoverability and locality of concern.
- Compile-time validation for structural mode behavior.
- Arbitrary strongly typed specialization remains straightforward.
- Structural mode definitions are naturally version controlled.
- A mode's implementation is visibly separable and can be copied/extracted with its data and theme when spinning it into another deployment or a more independent application.
- Creating framework extension contracts is conventional and testable with ordinary .NET tooling.

### Costs

- Creating a fundamentally new mode requires a code change, build/deployment, and application restart.
- Structural mode changes require deployment even when they could theoretically be represented declaratively.
- A large number of mostly declarative modes could produce source modules that contain little more than configuration.
- Care is required to prevent every shared feature from growing mode-specific provider interfaces and switches.

## Option B: runtime/data-defined mode

A normal mode is persisted runtime state interpreted by the framework. The source tree defines reusable capabilities, while the database/configuration defines each site instance.

Conceptually:

```text
Mode
    Id
    DisplayName
    Homepage
    Theme
    Navigation
    ContentSources
    EnabledTools
    PresentationSettings
```

### Strengths

- Authorized administrators can potentially create and structurally configure modes through the site without deployment or restart.
- A large number of modes can be composed from installed capabilities without adding source files.
- The model is closer to a wiki farm/CMS where the engine is code and site instances are runtime state.
- Tools remove much of the need for a runtime mode record itself to express arbitrary executable behavior.

### Costs

- Compile-time validation is replaced in part by runtime validation and schema/version checks.
- Exact instance composition is less visible from the repository alone and needs a strong administrative inspector/export representation.
- Portability does not fall out automatically from a source directory. It must be a deliberate framework contract.
- New executable behavior still requires an installed capability/tool even if creation of a new mode does not.

## Portability requirement

Portability is a first-class concern in this decision.

The framework should preserve the ability to separate a mode from a multi-mode installation and either:

1. move it to another installation running the same framework; or
2. run it as the only mode in a separate deployment.

A compiled mode naturally carries structural implementation in source. A runtime-defined mode would need an explicit portable representation, potentially including:

```text
mode package
    manifest / mode definition
    content and revisions
    theme/configuration
    media ownership/references
    content-source mapping metadata
    required tool IDs/versions
    exportable mode-owned tool data where supported
```

Deployment secrets, database credentials, trusted-network configuration, and production host bindings must not be embedded in such a package.

A runtime mode therefore does not have to be less portable, but portability becomes a feature that must be designed, tested, and versioned.

## Homepage/content requirement independent of this decision

The current Professional and Dorks & Dice homepages use disjoint storage/composition mechanisms. Unifying them belongs in this refactor regardless of how mode definitions are represented.

Target properties:

- homepage content is stored in the same database-backed content architecture used for editable site content;
- changing homepage content does not require a service restart;
- every normal mode uses the same general homepage/page contract;
- dynamic behavior is supplied by installed components/tools rather than by embedding frequently edited content in C#;
- Professional résumé data should be evaluated for migration into the general content architecture instead of remaining a separate file-driven homepage store solely because that is how the current implementation began;
- Dorks & Dice dynamic integrations such as Minecraft status/Discord should be treated as behavior/components, not as justification for a separate homepage content system.

The general page/content contract should therefore be designed so that it can resolve a homepage from either a compiled mode descriptor or a runtime mode record. Do not make the content engine depend on which persistence model is ultimately selected for modes.

## Work that can continue before the decision

Safe/refactor-neutral work includes:

- removing named-mode branches from generic framework code;
- separating framework, deployment, fallback, Trusted Preview, and tool concerns physically;
- making stable string mode IDs authoritative at runtime boundaries;
- unifying page/homepage content storage behind a generic content contract;
- isolating/removing legacy compatibility code;
- improving tool portability and mode-aware tool contracts;
- keeping host/domain/database secrets in deployment infrastructure;
- preserving characterization/security tests;
- documenting ownership and migration boundaries.

Work to defer until this decision is made includes:

- making a database mode catalog the permanent source of truth;
- making C# mode modules the permanent extension contract;
- designing an on-site mode-creation UI around either representation;
- deleting migration boundaries solely because one representation appears likely;
- defining final mode export/import semantics that depend on the chosen representation.

## Decision question

The central product question is:

> Is the framework primarily intended to let developers define different applications/sites from shared code, or to let administrators create and operate different site instances from installed capabilities?

The final design may still use both code and data, but one of them should be the authoritative definition of a normal mode rather than leaving two competing sources of truth.
