# One-time External media promotion

Temporary tool for the 22 explicit mappings in docs/local-content-media-migration.md.
Commit and run only from migration/promote-content-media-to-external; do not merge
this branch. The operator runs the live write phases.

## Running

Requires the repository's .NET SDK and existing Development configuration/user
secrets for the Local and External content sources. No account or website endpoint
is needed. The tool uses the application's configured PostgreSQL External source.

From the tool checkout in PowerShell, set the source checkout containing the actual
migrated Local database and the ignored operation directory:

    $sourceRoot = 'D:\Projects\dorks-and-dice-site'
    $state = Join-Path $sourceRoot '.tmp\content-media-external-promotion'
    $project = '.\tools\ContentMediaExternalPromotion\ContentMediaExternalPromotion.csproj'
    dotnet run --project $project -c Release -- --plan --root $sourceRoot --state $state

The source root can be separate from the tool checkout. The executing tool must
remain in its temporary branch checkout for write operations. Local must resolve
to exactly the source root's dorks-and-dice-site/Content/content.db.

Plan is the default operation. It opens all content databases read-only and writes
only the requested plan JSON file. Review every entry, missing target, revision
baseline and proposed reuse in plan.json. A plan cannot overwrite existing state.
If a plan already exists, inspect it or choose a fresh ignored directory. Never
replace an operation's plan after any staging has started.

The original backup default is work/content-media-migration/local-before-20260906T055928Z.db.
If that file is absent, the tool uses the preserved copy at
.tmp/content-media-backups/local-before-20260906T055928Z.db. Use --backup with an
explicit path if needed. Local and the backup are always opened read-only.

Run these phases separately, with the same source root and state directory:

    dotnet run --project $project -c Release -- --stage-assets --root $sourceRoot --state $state
    dotnet run --project $project -c Release -- --apply-revisions --root $sourceRoot --state $state
    dotnet run --project $project -c Release -- --verify --root $sourceRoot --state $state

Stage uploads/reuses media and adds dependencies without changing any revisions.
Apply uses each current External editor document and replaces only explicit
manifest references. It appends one revision per changed page. Missing targets
are skipped, including their otherwise-unused media. No Local pages are copied.

Verify opens content databases read-only, validates history, metadata, tags, modes,
dependencies, media SHA-256 and exact current references. An in-process application
host checks rendered pages, both homepages, source selections and media isolation.
Its temporary developer authentication exists only inside this test host, which
does not expose a listening server. Startup content-schema and identity writes
are disabled. The normal application tests also run; their logs and results stay
in the ignored state directory. Clear CONTENT_TEST_POSTGRES and
IDENTITY_TEST_POSTGRES overrides before verification.

## Recovery and rollback

Preserve plan.json and journal.json until the migration has been verified and its
rollback window has ended. They contain content snapshots, generated keys, revision
IDs, checksums and timestamps, but no connection strings or credentials. They are
private local operational files, not source files to commit.

A failed command exits nonzero and reports journal progress. Commit-pending means
the durable journal preceded the database commit; it is not proof that the write
committed. Rerun the same phase and it reconciles the actual database records.
Repeated stage/apply calls do not duplicate committed work. Stage cannot run after
the revision phase starts. A page failure stops the operation; earlier committed
pages remain recorded and no global success is reported.

If an editor changes an affected page, the revision guard refuses to overwrite it.
Do not edit the journal or replace the plan to bypass that guard. Reconcile the
concurrent change before resuming. The transaction locks protect the final
revision check/save, and a database advisory lock prevents concurrent instances
of this tool from writing the same destination.

To undo the recorded operation:

    dotnet run --project $project -c Release -- --rollback --root $sourceRoot --state $state

Rollback restores the previous current revision and removes only the recorded
migration-created revision and dependencies, in one database transaction. It
refuses newer current revisions, descendants, or changed historical data.
Preexisting/reused assets stay. Newly created assets stay if another revision,
page or known Local cross-source dependency uses them. Rollback never writes Local.
After rollback use a new ignored state directory for any new operation.

## Implementation limits

The tool checks the application's expected content tables and columns without
calling the write-capable schema initializer in plan or verify. Source integrity,
manifest hashes, media bytes, dependencies and Local/current revision references
must agree before write phases proceed. The destination identity is printed only
as a provider and opaque fingerprint.

Normal media and authoring services perform the writes. Console-only interceptors
flush the journal before commit, lock/check revisions inside authoring transactions
and preserve legacy tag/mode spellings on newly added revision children. Historical
rows are never normalized or replaced. Unknown metadata that the normal editor
would drop causes an abort. Rollback uses narrowly scoped SQL for recorded IDs.

This is a one-time manifest tool, not a general merge service. A changed manifest,
Local snapshot, backup or destination identity invalidates the saved operation.
There is no website API, permanent authentication change, automatic page creation,
database credential, or database deployment in this branch.

## Tests

    dotnet test tools/ContentMediaExternalPromotion.Tests -c Release -p:UseAppHost=false
    dotnet test dorks-and-dice-site.slnx -c Release -p:UseAppHost=false

Promotion tests use disposable SQLite fixtures by default. Setting
PROMOTION_TEST_POSTGRES to an explicitly disposable loopback PostgreSQL admin
connection with database postgres creates uniquely named test databases there.
Never point this variable at a valuable database/server. The fixture evidence is
kept under ignored .tmp/promotion-tests and the disposable server can be removed
afterward.

## Rehearsal findings (2026-09-06)

The read-only live plan found 22 manifest entries, 10 existing applicable External
pages, and 13 applicable media assets. The missing professional-home page accounts
for nine skipped entries. No live stage, apply or rollback was run.

A disposable copy of all External content passed repeat staging/apply, preservation
of the 10 revised pages and their history, and rollback back to the exact original
table fingerprints. The actual Local source remained unchanged. Media, page
rendering and isolation checks passed 72 HTTP checks before a separate homepage
issue stopped verification: the current Local dorks-and-dice-home uses the
minecraft-server-status-badge directive, which this base branch does not register.
Local-only Dorks & Dice homepage rendering therefore returns HTTP 500. Full live
verification must continue to report failure until the application/content
compatibility issue is resolved. The tool does not modify that unrelated content.

The normal application suite passed 334 tests. Promotion tests cover interruptions
around commits, idempotence, metadata/history preservation, reused/shared assets,
concurrent editorial changes, rollback, read-only HTTP checks and legacy mode names.
