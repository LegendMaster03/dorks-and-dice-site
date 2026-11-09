# Repository ownership: reusable architecture and hosted sites

## Current scope

This repository has two simultaneous purposes: it runs the **Dorks & Dice**
and **Professional** websites through one ASP.NET Core MVC application, and it
serves as the working prototype of a reusable web architecture that has not yet
been named or extracted into a separate package. A normal site mode is a
separate site identity, not necessarily a separate running process.

This map describes current source ownership and intended extraction boundaries;
it is not a claim that the framework is already independently packaged.
See [site mode architecture](site-mode-architecture.md) for runtime behavior
and the [refactor plan](refactor-architecture-plan.md) for acceptance criteria
and outstanding migration decisions.

## Source ownership

| Area | Responsibility | Extraction boundary |
| --- | --- | --- |
| `dorks-and-dice-site/Framework/` | Normal-mode contracts, synthetic states, fallback modules, plugin contracts, and generic operator contracts | Reusable application mechanics |
| `dorks-and-dice-site/Services/Site/` | Host-mode context, route ownership, and shared mode-aware request behavior | Generic mechanics; avoid hardcoding the two current modes |
| `dorks-and-dice-site/Services/Content/` and `Models/Content/` | Database-backed content, revisions, media, and content source composition | Shared content infrastructure |
| `dorks-and-dice-site/Services/Identity/` and `Services/Tools/` | Shared authorization and Tool hosting implementations | Generic contracts and services, subject to the refactor's stated boundaries |
| `dorks-and-dice-site/Modes/Professional/` and `Modes/DorksAndDice/` | Mode-owned registrations, business behavior, services, and persistence | Site-specific modules, not core framework behavior |
| `dorks-and-dice-site/Views/SiteModes/` and `wwwroot/site-modes/` | Mode-specific view and branding assets, alongside framework-owned fallback and development views | Keep site-owned presentation distinguishable from framework fallback |
| `dorks-and-dice-site/Plugins/` | Installed in-process executable extensions, some shared and some presentation-specific | Separate from normal modes and from independently hosted Tools |
| `dorks-and-dice-site/Deployment/` and deployment configuration | Composition of the current normal modes, host bindings, and runtime configuration | Installation-specific, not portable mode identity |
| `dorks-and-dice-site.Tests/` | Behavioral, integration, security, and architecture tests | Preserve tests for both shared contracts and site-specific behavior |

`Program.cs` currently composes the running application. Its presence in
the same project as the above components does not make all registration and
deployment details part of the portable framework.

## Site modes versus framework states

The two current **normal modes** are `professional` and `dorks-and-dice`.
They may own routes, persistence, authorization relationships, presentation,
and domain behavior. Campaigns, characters, and the Dorks & Dice Discord
workspace are examples of domain responsibilities that should not be copied
into framework services merely because the framework hosts them.

**Development/Trusted Preview** is a synthetic control-plane state, not
another tenant site. **Fallback/Unassigned** handles requests for which no
normal site identity is resolved; it is likewise not a hosted site mode.

The generic `ISiteModeRegistry` consumes stable registered mode IDs. The
deployment currently supplies built-in definitions through
`DeploymentSiteModeRegistrationSource`. Whether future definitions use
compiled registrations, manifests, or persistent data is still undecided.

## Rules for documentation and eventual extraction

- Describe present implementations separately from planned interfaces and
  productization goals. Do not claim that extracting the architecture is done.
- Explain shared contracts in terms of responsibilities and invariants, not
  current customer names or production hostnames.
- Document site-specific behavior in its owning mode, including dependencies
  on generic contracts and any deployment configuration it requires.
- Keep hosting domains, secrets, network endpoints, connection strings, and
  machine-specific paths outside portable framework and mode contracts.
- Distinguish in-process plugins, mode-native domain features, and independent
  Tools by ownership and lifecycle, not by implementation size alone.
- Treat the current repository name and the names of its two sites as
  deployment/project identities. Do not assign the future framework a name
  before one is chosen.

The separation is a design goal supported by the current folder and contract
structure. Remaining coupling and migration tasks belong to the refactor
plan; documentation should not obscure them.
