# Pillar 2 Phase 5 — DM Origin campaign navigation

**Date:** 2026-11-12  
**Branch:** `feature/pillar2-origin-dm-campaign-authority-20261112`

The existing Site campaign details page now offers **Mixed Origin reviews**
only when the Site currently recognizes the visitor as an active campaign DM
and the campaign is active. It lists Site campaign context's **active,
connected character identities**; it does not inspect another player's
private Character Sheet, Origin draft, Class, or inventory.

The Site builds links using the registered `CharacterToolContract`:
`/tools/character-sheet/dm-origin-review/{campaignId}/{characterId}`.
The separately implemented Character Sheet DM page calls only the
narrow `mixed-review` and `activate-mixed-approved` endpoints.

Security boundaries:

- `CampaignsController` consults current campaign membership and Site
  `ICampaignContextService`. A view cannot infer DM authority from a
  global role or self-reported client state.
- `CampaignOriginReviewNavigation` rejects mismatched context, missing DM
  role, and invalid targets. The Site context itself filters out archived
  characters, disconnected associations, and inactive campaigns.
- Players do not see DM navigation; an archived campaign shows none.
  A previously rendered link does **not** remain a permission after DM
  demotion, disconnection, or campaign archival. Character Sheet checks
  the current DM role and active campaign–character association again
  before accessing a draft or processing an approval.
- Opening a link for a character with no supported draft may return a
  read-only failure. Site navigation does **not** imply a pending approval.
- The endpoint's currently supported decision is still the exact
  Human–Elf native source-offer example, not a general hereditary
  determination, proof of individual parentage, or whole-character
  mechanics projection.

The view-model/nav contract tests verify DM role, exact campaign ID,
distinct active character identities, and canonical Tool routes. Existing
Site scoped-DM ticket integration tests continue to enforce the actual
authorization boundary. Deployed browser-level acceptance remains required.

**No changes to the proposed future partial character-state slot
architecture, and no changes to Phase 5 completion requirements.**
