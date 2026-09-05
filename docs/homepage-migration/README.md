# Homepage Markdown migration inputs

These files are staging inputs for moving the two current compiled/file-backed homepages into the shared database-backed `homepage` content path.

They are **not** a second runtime content store. Once the database-backed homepage revisions are accepted and the compiled fallbacks are removed, these migration files can also be removed.

## Dorks & Dice

`dorks-and-dice.md` is ready to load into a local development content database for visual and routing validation.

Create a content item with:

```text
Stable ID: dorks-and-dice-home
Slug: dorks-and-dice-home
Listed: true
Tags: homepage
Visible site modes: dorks-and-dice
Body format: markdown
```

Use this metadata JSON:

```json
{
  "title": "Home",
  "summary": "A gaming community built around ambitious campaigns, player-driven stories, and the spaces that keep the group connected between sessions.",
  "linkText": "Open homepage",
  "metaTitle": "Dorks & Dice",
  "metaDescription": "Dorks & Dice is a tabletop gaming community built around long-form campaigns, shared worlds, game servers, and collaborative play."
}
```

The Markdown intentionally removes the live Minecraft status block and Discord iframe from authored homepage content. Those are runtime capabilities, not authored content. Game-server monitoring is now a candidate for extraction as a Tool. The compiled Dorks & Dice home remains the fallback until the database homepage is present, so this can be tested without deleting the old implementation first.

## Professional

`professional-static-sections.md` converts the portions currently sourced from `Content/Resume/resume.json` that map cleanly to authored Markdown:

- identity/header copy;
- contact information;
- professional summary;
- skills;
- education;
- honors and awards;
- leadership.

It is **not yet activation-ready** as the Professional homepage because Experience and Projects are already database-backed structured content and the existing homepage composes them dynamically. Duplicating those records into the homepage Markdown would create two editable sources of truth.

Before activating the Professional Markdown homepage, provide a general page-composition mechanism that can render those existing database-backed collections from a Markdown page, or choose another shared structured-content composition that does not special-case the Professional site in framework code.

The intended Professional item identity is:

```text
Stable ID: professional-home
Slug: professional-home
Listed: true
Tags: homepage
Visible site modes: professional
Body format: markdown
```

The current metadata target is:

```json
{
  "title": "Resume",
  "summary": "Software developer and technology generalist with experience across application development, cybersecurity, systems, and client technology needs.",
  "linkText": "Open resume",
  "metaTitle": "Kyle Barnett Resume",
  "metaDescription": "Professional resume for Kyle Barnett: experience, education, and selected projects.",
  "metaImage": "/site-modes/professional/images/profile/kyle-headshot.jpg"
}
```

## Tool extraction direction

Game-server monitoring should be treated as a Tool candidate rather than a homepage subsystem. A later extraction should move the Minecraft/Hytale monitoring behavior and its configuration out of the Dorks & Dice homepage module while preserving the host/tool security boundaries already defined in `tool-hosting-boundaries.md`.

Do not delete the existing monitoring service solely because the Markdown draft omits it. Remove the old homepage dependency only after the replacement Tool is registered and the DB homepage has been validated.
