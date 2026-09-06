# Authored site-mode media migration

Authored content now references managed media through the existing upload, attachment, transfer, and revision services. Application ownership is independent of content ownership: a favicon or icon can remain a deployed application asset while an identical managed copy belongs to authored content. Classification uses resolved path references, not directory names or filename-only matches.

## Result

- Inspected 28 source files: 22 have authored-content uses. All 28 originals remain; 14 have confirmed application/file-backed uses, including 10 that also have managed content copies.
- Imported 21 new assets and reused one existing asset across the complete migration. The review correction added five assets and five attachments, revising two records.
- Revised 11 distinct Local content records, including 10 copies of External records. Local now contains 12 pages, 35 media records, and 35 media-dependency relationships: one original relationship, 13 preserved during transfers, and 21 new migration attachments.
- Current Local authored content contains **zero direct `/site-modes/...` references**, with no exceptions. Historical revisions retain original references and remain unchanged.
- Live External content was never modified. Read-only verification matched all nine content tables and every stored media byte against the pre-migration snapshot. Backups and validation evidence are retained outside Git.
- The Development favicon was not imported: its filename-only match was not an authored-content path reference.

## Retained application assets

Paths below are web-root-relative. These source files remain for application or file-backed presentation uses, independently of managed copies:

| Source asset | Authored-content copy? |
| --- | --- |
| `/site-modes/development/css/site.css` | No |
| `/site-modes/development/images/favicon.svg` | No |
| `/site-modes/dorks-and-dice/css/site.css` | No |
| `/site-modes/professional/css/site.css` | No |
| `/site-modes/professional/files/Honors Society for Computing - Kyle.pdf` | Yes |
| `/site-modes/professional/files/f71495bc-911d-11f1-9d9c-97457dea8923.pdf` | Yes |
| `/site-modes/professional/files/fc59cd40-548b-11f1-bb92-d362b13ee885.pdf` | Yes |
| `/site-modes/professional/files/kyle-resume.pdf` | Yes |
| `/site-modes/professional/images/favicon.svg` | Yes |
| `/site-modes/professional/images/icons/github.svg` | Yes |
| `/site-modes/professional/images/icons/gmail.svg` | Yes |
| `/site-modes/professional/images/icons/linkedin.png` | Yes |
| `/site-modes/professional/images/icons/phone.svg` | Yes |
| `/site-modes/professional/images/profile/kyle-headshot.jpg` | Yes |

The remaining source media are retained for unchanged External content and revision history. The static resume text and alternate `wired-works.jpg` logo are also retained; lack of a current database owner was not treated as proof that deletion was safe.

## Managed media mapping

Every source asset below remains in place. Contact icons still serve the file-backed resume; the Professional favicon still serves mode presentation. The canonical filenames follow the existing upload normalization. The Safe Future source has a misleading PNG extension but contains WebP bytes, so its managed filename uses `.webp`.

| Source URL | Managed URL | Local content record |
| --- | --- | --- |
| `/site-modes/professional/files/Honors Society for Computing - Kyle.pdf` | `/content/media/11fb35d85dd84298bfc36ead882003dd/Honors-Society-for-Computing--Kyle.pdf` | `professional-home` |
| `/site-modes/professional/files/f71495bc-911d-11f1-9d9c-97457dea8923.pdf` | `/content/media/0d7c5aa50f8546afaa9cd2ec4261c9ef/f71495bc-911d-11f1-9d9c-97457dea8923.pdf` | `professional-home` |
| `/site-modes/professional/files/fc59cd40-548b-11f1-bb92-d362b13ee885.pdf` | `/content/media/f93a94fdfff24d639231f0d96b6b4f27/fc59cd40-548b-11f1-bb92-d362b13ee885.pdf` | `professional-home` |
| `/site-modes/professional/files/kyle-resume.pdf` | `/content/media/eded0c44dade448a93515f73349a410a/kyle-resume.pdf` | `professional-home` |
| `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_1.pdf` | `/content/media/266eabed94d242a4ad95c3d357534d36/Directed_Independent_Study_Deliverable_1.pdf` | `directedindependentstudy` |
| `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_2.pdf` | `/content/media/c5283fbd28ed47eb9dcd67862330ce7c/Directed_Independent_Study_Deliverable_2.pdf` | `directedindependentstudy` |
| `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_3.pdf` | `/content/media/f5a9d782412143bf9419d644eb40e18a/Directed_Independent_Study_Deliverable_3.pdf` | `directedindependentstudy` |
| `/site-modes/professional/images/logos/consolevariations-bee.png` | `/content/media/e539f9936d7c4c17bccc094a0883ef48/consolevariations-bee.png` | `freeing-the-bees-consolevariations-puzzle` |
| `/site-modes/professional/images/logos/florida-poly.png` | `/content/media/1bd8b6d293d9432587f0936a01ee5e32/florida-poly.png` | `experiencesimlab` |
| `/site-modes/professional/images/logos/osec-logo-full.png` | `/content/media/0ff70af8e41e4d2d8494c1329fabfd16/osec-logo-full.png` | `experiencecybersecurityteam` |
| `/site-modes/professional/images/logos/safe-future-logo.png` | `/content/media/e6ebab965c574d43a4865841eed99cd0/safe-future-logo.webp` | `seniorproject` |
| `/site-modes/professional/images/logos/skyblivion.png` | `/content/media/ed3a85d3e87a41e1a1940f69a9468cf7/skyblivion.png` | `skyblivion` |
| `/site-modes/professional/images/logos/skywind.png` | `/content/media/63bab5f27cb34aafba5b62be6e0729a9/skywind.png` | `skywind` |
| `/site-modes/professional/images/logos/unf.svg` | `/content/media/ebb1f78a4aaa4cd293217a03130e9dda/unf.svg` | `directedindependentstudy` |
| `/site-modes/professional/images/logos/wired-works-transparent.png` | `/content/media/92302dfce42a43da810455c87fd8d6ba/wired-works-transparent.png` | `experiencewiredworks` |
| `/site-modes/professional/images/logos/xngine-logo.png` | `/content/media/c39c239693d44948b21688fb3e2cc0d6/xngine-logo.png` | `xngine` |
| `/site-modes/professional/images/profile/kyle-headshot.jpg` | `/content/media/21a9d1fde7ce4c8db49140ec3666641b/kyle-headshot.jpg` | `professional-home` |
| `/site-modes/professional/images/favicon.svg` | `/content/media/0767c43bab974e5280652a587772e610/favicon.svg` | `personalmultimodewebsite` |
| `/site-modes/professional/images/icons/github.svg` | `/content/media/5fa79f719bee4118b7107ad8775a26e5/github.svg` | `professional-home` |
| `/site-modes/professional/images/icons/gmail.svg` | `/content/media/955b3d79a8884f169adf2b7f8bcd19bb/gmail.svg` | `professional-home` |
| `/site-modes/professional/images/icons/linkedin.png` | `/content/media/1a64595258554191b4d2f35dd8a0834b/linkedin.png` | `professional-home` |
| `/site-modes/professional/images/icons/phone.svg` | `/content/media/f090c4196f6145d2ba25872272b9a0bb/phone.svg` | `professional-home` |

## Production behavior and validation

PDF and passive SVG support use the existing media schema and dependency parser. PDFs produce Markdown links and appropriate library previews. Passive SVG validation retains the uploaded bytes, rejects active content, and accepts the non-executable `role` accessibility attribute. Invalid uploads report a declared **media type** mismatch.

- Full suite: **334 passed, 0 failed, 0 skipped**.
- HTTP validation: **82 checks passed**, covering all 22 managed media byte hashes, all 11 revised content records, both homepage modes, mode/source isolation, and Development access controls.
- Database checks confirmed all managed dependencies and current revision references, zero current static references, preserved historical rows, copied metadata/tags/modes/history, and SQLite integrity.
- Source files and mode/plugin/routing/source-selection configuration are unchanged. No one-time migration runner or generated workspace remains in the branch.
