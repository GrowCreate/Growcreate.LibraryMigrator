# Growcreate.LibraryMigrator

A backoffice package for Umbraco 18 that migrates content saved as Element Types to the new Library (reusable elements) feature introduced in Umbraco 18.

## Features

- **Two entry points** — a workspace-view tab on configured container document types (per-container flow) *and* a global dashboard tab in the Content section (site-wide, per-doc-type flow)
- Preview report showing exactly what will change before committing
- Per-container flow migrates the container's **direct child documents** to Library elements
- Global flow migrates **every instance** of a chosen document type wherever it lives in the tree, grouped in the Library by immediate parent
- Auto-discovery of eligible document types on the global dashboard (no template + at least one instance)
- Rewrites references to the migrated content across content, media and member properties — including nested blocks and Rich Text Editor blocks
- Historical version rewriting so published and draft history reflects the new Library element keys
- Snapshot and restore — roll back any migration to the original element data (per-container flow restores from the workspace tab; global flow lists past runs on the dashboard with a per-run Restore button)
- Migration status check per document (per-container) and per run (global)
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

Configuration is **only needed for the per-container workspace-view flow**. The global migration dashboard works out of the box with no configuration — it discovers eligible document types automatically.

To enable the per-container workspace tab, add a `Growcreate.LibraryMigrator` section to `appsettings.json` listing the document type aliases that act as containers for element content:

```json
{
  "Growcreate.LibraryMigrator": {
    "ContainerDocTypeAliases": [ "blogPost", "landingPage" ]
  }
}
```

| Setting | Type | Description |
|---------|------|-------------|
| `ContainerDocTypeAliases` | `string[]` | Document type aliases whose documents will show the per-container **Library Migration** workspace tab. Leave empty if you only intend to use the global dashboard. |

The package registers the settings automatically. No code changes are required in the host project beyond this configuration.

## Usage

The package exposes **two entry points** into the migration engine. Both are administrator-only — non-admin users never see either UI.

### Entry point 1 — Per-container workspace tab

Use this when you want to migrate the direct children of one specific container document (the original flow).

1. In the Umbraco backoffice, navigate to the **Content** section
2. Open a document whose document type is listed in `ContainerDocTypeAliases`
3. Click the **Library Migration** tab in the workspace view
4. Review the **Preview** report — it lists the child documents that will become Library elements, plus every picker/block property whose references will be rewritten
5. Click **Migrate** to run the migration

> **Scope:** only the container document's **direct children** are migrated to Library elements.
> Element-ness is a content-type setting, so the child document types are flipped to elements
> for the whole site — any instances of those types living outside the container become
> element-type documents and are listed as warnings in the Preview report. References to the
> migrated content (pickers, blocks, RTE blocks, and historical versions) are rewritten
> site-wide across content, media and member properties.

### Entry point 2 — Global migration dashboard

Use this when you want to migrate **every instance** of a given document type in one run, regardless of where those documents live in the content tree. No configuration is required — the dashboard discovers eligible types automatically.

1. Open the **Content** section
2. Open the **Library Migration** dashboard tab (it sits after Umbraco's built-in *Welcome* and *Redirect URL Management* dashboards)
3. The **eligible document types** table lists every doc type in the site that qualifies (see the eligibility rule below), showing instance count and current element/Library flags
4. Click **Analyse** on a row to load the preview report for that type
5. Review the impact — instance count, distinct parents, and every picker / Block List / Block Grid / member / media property that could be affected — then click **Run Migration**
6. On success, the preview is replaced by the completion summary and the type disappears from the eligible list
7. The **Past global migration runs** panel lists every completed run with a per-run **Restore** button

#### Eligibility rule

A document type appears on the global dashboard only if **both** of these are true:

- it has **no template** assigned (neither a default template nor any allowed templates), and
- it has **at least one instance** in the content tree

Doc types with templates are treated as "page" types and are intentionally excluded — the Library is for reusable element content, not routable pages.

#### Grouping in the Library

Migrated documents are grouped in the Library by their **immediate parent** — one Library folder per parent, mirroring the per-container flow. The folder is named after the parent document.

#### Blockers

The global flow refuses to run for a type if any instance has child nodes, because deleting the instance would destroy the subtree with no way to restore it. The blocker list on the preview names every offending instance so you can move or remove its children first.

### Restoring a migration

Both flows support restore. A snapshot is stored automatically during migration and is required for restore to be available.

**Per-container**

1. Open the same container document in the backoffice
2. Go to the **Library Migration** tab
3. Click **Restore** to revert all properties to their original element-type data

**Global**

1. Open the **Library Migration** dashboard tab
2. Find the run in the **Past global migration runs** panel
3. Click **Restore** on that row

Restore recreates the original documents with their original keys, unflips `IsElement` / `AllowedInLibrary` on the migrated content type, reverts converted picker data types, restores rewritten property values and historical versions, and deletes the Library elements and folders created by the run.

### Checking migration status

**Per-container** — the workspace tab shows current status on load:

- **Not migrated** — no migration has run for this document
- **Migrated** — migration completed cleanly; restore is available
- **Partially migrated** — the elements were created, but a later phase (picker data-type conversion and/or reference rewrite) hit errors and was rolled back as a unit. References may still point at the old content. Inspect the reported errors; restore is still available

**Global** — every past run is listed on the dashboard with the same three states surfaced as a tag next to it, and any reported errors are shown alongside the Restore button.

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
