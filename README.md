# Growcreate.LibraryMigrator

A backoffice package for Umbraco 18 that migrates content saved as Element Types to the new Library (reusable elements) feature introduced in Umbraco 18.

## Features

Two entry points into the same migration engine:

- **Per-container workspace tab** — a Library Migration tab on configured document types for in-context migration of a container's direct children
- **Global doc-type dashboard** — a Library Migration dashboard in the **Content** section that lists every eligible document type and migrates *all* of its documents site-wide, wherever they live in the content tree (no container configuration required)

Shared capabilities:

- Preview report showing exactly what will change before committing
- Migrates documents to Library elements (a container's **direct children**, or every instance of a chosen type)
- Rewrites references to the migrated content across content, media and member properties — including nested blocks and Rich Text Editor blocks
- Historical version rewriting so published and draft history reflects the new Library element keys
- Snapshot and restore — roll back a migration to the original element data
- Migration status check per container document or per document type
- Admin-only access enforced at both the API and UI layers (every endpoint requires an administrator)
- Auto-discovered via `IComposer` — no manual registration in the host project required

## Installation

Install the NuGet package:

```bash
dotnet add package Growcreate.LibraryMigrator
```

Or via the Package Manager Console:

```powershell
Install-Package Growcreate.LibraryMigrator
```

## Configuration

Add a `Growcreate.LibraryMigrator` section to `appsettings.json` listing the document type aliases that act as containers for element content:

```json
{
  "Growcreate.LibraryMigrator": {
    "ContainerDocTypeAliases": [ "blogPost", "landingPage" ]
  }
}
```

| Setting | Type | Description |
|---------|------|-------------|
| `ContainerDocTypeAliases` | `string[]` | Document type aliases whose documents will show the Library Migration workspace tab |

The package registers the settings automatically. No code changes are required in the host project beyond this configuration.

## Usage

The package exposes two ways to run a migration. Both use the same engine, previews,
reference rewriting and restore — they differ only in how the set of documents is chosen.
Both the UI and every API endpoint are administrator-only — non-admin users never see them.

### Running a per-container migration (workspace tab)

1. In the Umbraco backoffice, navigate to the **Content** section
2. Open a document whose document type is in `ContainerDocTypeAliases`
3. Click the **Library Migration** tab in the workspace view
4. Review the **Preview** report — it lists the child documents that will become Library elements, plus every picker/block property whose references will be rewritten
5. Click **Migrate** to run the migration

> **Scope:** only the container document's **direct children** are migrated to Library elements.
> Element-ness is a content-type setting, so the child document types are flipped to elements
> for the whole site — any instances of those types living outside the container become
> element-type documents and are listed as warnings in the Preview report. References to the
> migrated content (pickers, blocks, RTE blocks, and historical versions) are rewritten
> site-wide across content, media and member properties.

### Migrating a whole document type (global dashboard)

For a content-wide migration that is not tied to a container, use the dashboard:

1. In the Umbraco backoffice, navigate to the **Content** section
2. Open the **Library Migration** dashboard (listed after the built-in Content dashboards)
3. Pick a document type from the list of **eligible types**
4. Review the **Preview** report for that type — its site-wide instance count plus every picker/block property whose references will be rewritten
5. Click **Migrate** to convert **every** document of that type into Library elements, wherever it lives in the content tree. Migrated elements are grouped into Library folders named after each document's original parent

> **Eligible types** are document types that have at least one document in the content tree
> and have **no template** (page-less content types — the natural Library candidates). Types
> already converted to elements are excluded.
>
> **Blockers:** if *any* document of the selected type has child nodes, the whole run is
> refused (migrating to a Library element would discard the subtree). Move or remove those
> children first. The blocking documents are listed in the Preview report.

No `appsettings.json` configuration is required for the dashboard — `ContainerDocTypeAliases`
only governs the per-container workspace tab.

### Restoring a migration

If a migration needs to be undone, return to wherever you started it:

1. For a per-container migration, open the same document and go to the **Library Migration** tab; for a doc-type migration, open the **Library Migration** dashboard and select the same type
2. Click **Restore** to revert all properties to their original document data

A snapshot is stored automatically during migration and is required for restore to be available.

### Checking migration status

The tab (per container) and the dashboard (per selected type) show current status on load:

- **Not migrated** — no migration has run for this document
- **Migrated** — migration completed cleanly; restore is available
- **Partially migrated** — the elements were created, but a later phase (picker data-type conversion and/or reference rewrite) hit errors and was rolled back as a unit. References may still point at the old content. Inspect the reported errors; restore is still available

## Requirements

- Umbraco 18.x (`[18.0.0, 19.0.0)`) — pinned below 19 because the package relies on APIs that can change across a major version
- .NET 10.0

## Development

### Prerequisites

- .NET 10 SDK
- Node.js 20.19+ minimum; Node 24 LTS is the dev target (pinned in [`.nvmrc`](src/Growcreate.LibraryMigrator/Client/.nvmrc))

### Building from source

The client assets build automatically as part of the .NET build — the `BuildClientAssets`
MSBuild target runs `npm ci` (or `npm install`) and `npm run build` before static web assets
are resolved, so a clean checkout produces a complete package:

```bash
dotnet build
```

Node.js must be on `PATH`. To skip the client build (e.g. a .NET-only build where the
assets already exist), pass `-p:SkipClientBuild=true`. You can also build the client
manually:

```bash
cd src/Growcreate.LibraryMigrator/Client
npm install
npm run build
```

### Watching for client changes

During development, watch for TypeScript/Lit changes:

```bash
cd src/Growcreate.LibraryMigrator/Client
npm run dev
```

Restart the .NET host after any change that alters the output file list so the Static Web Assets manifest is regenerated.

### Creating a NuGet package

```bash
dotnet pack src/Growcreate.LibraryMigrator/Growcreate.LibraryMigrator.csproj -c Release
```

## Contributing

Contributions are welcome. Please submit a Pull Request.

## License

Licensed under the Apache License 2.0 — see [LICENSE](LICENSE) for details.

## Support

- [GitHub Issues](https://github.com/growcreate/Growcreate.LibraryMigrator/issues)

Built for Umbraco 18 by [Growcreate](https://growcreate.co.uk).
