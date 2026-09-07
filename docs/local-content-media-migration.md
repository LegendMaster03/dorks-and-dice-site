# Authored site-mode media migration

Authored content now references managed media through the existing upload, attachment, transfer, and revision services. Application ownership is independent of content ownership: a favicon or other presentation asset can remain deployed while an identical managed copy belongs to authored content. Classification uses resolved path references, not directory names or filename-only matches.

## Initial migration result

The original Local migration inspected 28 source files, imported 21 new assets, reused one existing asset, and revised 11 Local content records. At that migration checkpoint Local contained 12 pages, 35 media records, and 35 page/media relationships. Current Local authored revisions had zero direct `/site-modes/...` references; historical revisions were deliberately left unchanged.

That checkpoint was transitional. The later External promotion and cleanup changed the repository and Local database state described below.

## Current post-promotion state

The ten Professional article records were promoted to External with their managed media and then removed from Local. The Local authoring database is intentionally reduced to the two homepage records that are still being finished before publication:

- `professional-home`
- `dorks-and-dice-home`

After the Local cleanup, the Local content database contained:

- 2 pages;
- 16 revisions;
- 9 managed assets;
- 9 page-owned asset relationships;
- 0 page-asset dependency rows.

The nine remaining Local managed assets belong to `professional-home`. The Dorks & Dice homepage does not currently own managed media.

The ten promoted Professional article records now resolve their current media through managed `/content/media/...` URLs in External. Their obsolete article-specific source copies were removed from `wwwroot`. The unused alternate `wired-works.jpg` source image was also removed after a current-reference audit found no application use.

Historical content revisions were intentionally not rewritten. A historical revision that still stores a removed `/site-modes/...` URL can therefore lose that historical media if rendered verbatim. If exact historical media fidelity becomes a requirement, prefer a compatibility redirect from the legacy static URL to the managed asset rather than restoring duplicate source files or rewriting revision history.

## Current retained application/fallback assets

Paths below are web-root-relative.

| Source asset | Why it remains |
| --- | --- |
| `/site-modes/development/css/site.css` | Development presentation |
| `/site-modes/development/images/favicon.svg` | Development presentation |
| `/site-modes/dorks-and-dice/css/site.css` | Dorks & Dice presentation |
| `/site-modes/professional/css/site.css` | Professional presentation |
| `/site-modes/professional/images/favicon.svg` | Professional presentation |
| `/site-modes/professional/images/profile/kyle-headshot.jpg` | Legacy Professional fallback plus current Professional default metadata/structured-data image |
| `/site-modes/professional/files/kyle-resume.pdf` | Legacy Professional file-backed fallback only after the route-probe cleanup |
| `/site-modes/professional/files/fc59cd40-548b-11f1-bb92-d362b13ee885.pdf` | Legacy Professional file-backed fallback |
| `/site-modes/professional/files/f71495bc-911d-11f1-9d9c-97457dea8923.pdf` | Legacy Professional file-backed fallback |
| `/site-modes/professional/files/Honors Society for Computing - Kyle.pdf` | Legacy Professional file-backed fallback |
| `/site-modes/professional/images/icons/github.svg` | Legacy Professional file-backed fallback |
| `/site-modes/professional/images/icons/gmail.svg` | Legacy Professional file-backed fallback |
| `/site-modes/professional/images/icons/linkedin.png` | Legacy Professional file-backed fallback |
| `/site-modes/professional/images/icons/phone.svg` | Legacy Professional file-backed fallback |

The legacy Professional resume/home subsystem remains only until `professional-home` reaches parity, is moved to External, and is verified live. At that point the fallback-only source assets above can be deleted with the fallback implementation. The static headshot must be handled separately because it still has a non-fallback presentation dependency.

## Removed article-specific source copies

These files no longer have a current application role and were removed after their current authored references had been promoted to managed media:

- `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_1.pdf`
- `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_2.pdf`
- `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_3.pdf`
- `/site-modes/professional/images/logos/consolevariations-bee.png`
- `/site-modes/professional/images/logos/florida-poly.png`
- `/site-modes/professional/images/logos/osec-logo-full.png`
- `/site-modes/professional/images/logos/safe-future-logo.png`
- `/site-modes/professional/images/logos/skyblivion.png`
- `/site-modes/professional/images/logos/skywind.png`
- `/site-modes/professional/images/logos/unf.svg`
- `/site-modes/professional/images/logos/wired-works-transparent.png`
- `/site-modes/professional/images/logos/xngine-logo.png`
- `/site-modes/professional/images/logos/wired-works.jpg`

## Managed media mapping

The mapping below records the stable managed asset identity created by the migration. `professional-home` assets are still Local until that homepage is promoted. The article-owned assets are in External after the completed promotion.

| Former source URL | Managed URL | Current owner |
| --- | --- | --- |
| `/site-modes/professional/files/Honors Society for Computing - Kyle.pdf` | `/content/media/11fb35d85dd84298bfc36ead882003dd/Honors-Society-for-Computing--Kyle.pdf` | Local `professional-home` |
| `/site-modes/professional/files/f71495bc-911d-11f1-9d9c-97457dea8923.pdf` | `/content/media/0d7c5aa50f8546afaa9cd2ec4261c9ef/f71495bc-911d-11f1-9d9c-97457dea8923.pdf` | Local `professional-home` |
| `/site-modes/professional/files/fc59cd40-548b-11f1-bb92-d362b13ee885.pdf` | `/content/media/f93a94fdfff24d639231f0d96b6b4f27/fc59cd40-548b-11f1-bb92-d362b13ee885.pdf` | Local `professional-home` |
| `/site-modes/professional/files/kyle-resume.pdf` | `/content/media/eded0c44dade448a93515f73349a410a/kyle-resume.pdf` | Local `professional-home` |
| `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_1.pdf` | `/content/media/266eabed94d242a4ad95c3d357534d36/Directed_Independent_Study_Deliverable_1.pdf` | External `directedindependentstudy` |
| `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_2.pdf` | `/content/media/c5283fbd28ed47eb9dcd67862330ce7c/Directed_Independent_Study_Deliverable_2.pdf` | External `directedindependentstudy` |
| `/site-modes/professional/files/projects/directed-independent-study/Directed_Independent_Study_Deliverable_3.pdf` | `/content/media/f5a9d782412143bf9419d644eb40e18a/Directed_Independent_Study_Deliverable_3.pdf` | External `directedindependentstudy` |
| `/site-modes/professional/images/logos/consolevariations-bee.png` | `/content/media/e539f9936d7c4c17bccc094a0883ef48/consolevariations-bee.png` | External `freeing-the-bees-consolevariations-puzzle` |
| `/site-modes/professional/images/logos/florida-poly.png` | `/content/media/1bd8b6d293d9432587f0936a01ee5e32/florida-poly.png` | External `experiencesimlab` |
| `/site-modes/professional/images/logos/osec-logo-full.png` | `/content/media/0ff70af8e41e4d2d8494c1329fabfd16/osec-logo-full.png` | External `experiencecybersecurityteam` |
| `/site-modes/professional/images/logos/safe-future-logo.png` | `/content/media/e6ebab965c574d43a4865841eed99cd0/safe-future-logo.webp` | External `seniorproject` |
| `/site-modes/professional/images/logos/skyblivion.png` | `/content/media/ed3a85d3e87a41e1a1940f69a9468cf7/skyblivion.png` | External `skyblivion` |
| `/site-modes/professional/images/logos/skywind.png` | `/content/media/63bab5f27cb34aafba5b62be6e0729a9/skywind.png` | External `skywind` |
| `/site-modes/professional/images/logos/unf.svg` | `/content/media/ebb1f78a4aaa4cd293217a03130e9dda/unf.svg` | External `directedindependentstudy` |
| `/site-modes/professional/images/logos/wired-works-transparent.png` | `/content/media/92302dfce42a43da810455c87fd8d6ba/wired-works-transparent.png` | External `experiencewiredworks` |
| `/site-modes/professional/images/logos/xngine-logo.png` | `/content/media/c39c239693d44948b21688fb3e2cc0d6/xngine-logo.png` | External `xngine` |
| `/site-modes/professional/images/profile/kyle-headshot.jpg` | `/content/media/21a9d1fde7ce4c8db49140ec3666641b/kyle-headshot.jpg` | Local `professional-home` |
| `/site-modes/professional/images/favicon.svg` | `/content/media/0767c43bab974e5280652a587772e610/favicon.svg` | External `personalmultimodewebsite` |
| `/site-modes/professional/images/icons/github.svg` | `/content/media/5fa79f719bee4118b7107ad8775a26e5/github.svg` | Local `professional-home` |
| `/site-modes/professional/images/icons/gmail.svg` | `/content/media/955b3d79a8884f169adf2b7f8bcd19bb/gmail.svg` | Local `professional-home` |
| `/site-modes/professional/images/icons/linkedin.png` | `/content/media/1a64595258554191b4d2f35dd8a0834b/linkedin.png` | Local `professional-home` |
| `/site-modes/professional/images/icons/phone.svg` | `/content/media/f090c4196f6145d2ba25872272b9a0bb/phone.svg` | Local `professional-home` |

## Current validation boundary

The completed External article promotion passed managed-byte verification, database verification, HTTP checks, and the normal application test suite. The later Local cleanup preserved only the two unfinished homepage records and their intended managed assets. Subsequent homepage validation is tracked in `homepage-content-convergence.md` and should be completed before either homepage is moved to External.
