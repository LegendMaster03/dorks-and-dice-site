# Embedded Module integration contract

Embedded Modules are versioned separately from the Tool Host context payload. The registration field `IntegrationContractVersion` identifies the Embedded Module integration contract; `ToolHostContext.ContractVersion` continues to identify the context payload schema.

The currently supported Embedded Module integration contract is **version 2**.

## Version 2 frontend lifecycle

An Embedded Module owns an explicit application render lifecycle for application-owned DOM.

A conforming frontend:

- renders application-owned DOM through explicit application state and render entry points;
- does not rely on observing its own DOM with `MutationObserver` as the normal mechanism for detecting application changes;
- makes post-render enhancement idempotent so invoking enhancement again does not continually rewrite equivalent DOM;
- may use `MutationObserver` for genuine external boundaries that are outside the application's render ownership, provided the observer is guarded against mutations caused by its own enhancement work;
- does not depend on a particular frontend framework, language, or server technology.

This lifecycle requirement does not change the established Tool Host contracts. Embedded Modules must continue to honor:

- the `#tool-root` mount owned by the Site shell;
- `data-tool-base-path`, `data-tool-route`, and `data-tool-context-url` supplied by the host;
- Tool Host route-state behavior beneath `/tools/{slug}`;
- the Tool Host context and authenticated session/campaign APIs;
- anonymous/public access behavior from the registration;
- the hosted upstream API gateway and trusted authentication-ticket/introspection flow;
- normal Site mode visibility and authorization boundaries.

## Registration and enforcement

New Embedded Module registrations default to integration contract version 2 in the Dev Portal and must persist that explicit value. The Site rejects an Embedded Module registration submitted with a missing or unsupported integration contract version.

At runtime, an enabled Embedded Module with a missing or unsupported version is not hosted as though it were current. Its shell, module assets, Tool Host context, and Embedded Module API/upstream paths return an administrative/developer-facing `503 Service Unavailable` problem response identifying the unsupported version. This is intentional: an old registration remains visible in Tool Management so it can be diagnosed and repaired instead of being silently treated as compatible.

`ProxiedApplication` registrations do not use the Embedded Module integration contract version and are not rejected by this policy.

## Migration from the pre-versioned contract

The schema adds nullable `tool_integration_contract_version` storage with no database default. Omitting a default is required so arbitrary legacy registrations are not silently declared compatible.

The migration explicitly marks only the two known first-party Embedded Modules as version 2 when they already exist as unversioned registrations:

- `block-initiative`
- `rules-core`

Other unversioned Embedded Module registrations remain unversioned and unsupported until a developer deliberately reviews and updates them.

The Site patch that introduces this enforcement must not be merged until the lifecycle migrations for Block Initiative and Rules Core have both landed on their respective `main` branches.
