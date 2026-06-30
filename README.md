# Growcreate.LibraryMigrator

A backoffice package for Umbraco 18+ that migrates content saved as Element Types to the new Library (reusable elements) feature introduced in Umbraco 18.

## Features

- Workspace view tab on configured document types for in-context migration
- Preview report showing exactly what will change before committing
- Full migration of element type content to Library elements across all scopes (content, media, and member properties)
- Support for nested blocks and Rich Text Editor block content
- Historical version rewriting so published and draft history reflects the new Library element keys
- Snapshot and restore — roll back any migration to the original element data
- Migration status check per document
- Admin-only access enforced at both API and UI layers
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

### Running a migration

1. In the Umbraco backoffice, navigate to the **Content** section
2. Open a document whose document type is in `ContainerDocTypeAliases`
3. Click the **Library Migration** tab in the workspace view
4. Review the **Preview** report — it lists every element type property that will be converted and the Library elements that will be created
5. Click **Migrate** to run the migration

Only administrators can run migrations or restores. The tab is visible to all backoffice users but actions are gated.

### Restoring a migration

If a migration needs to be undone:

1. Open the same document in the backoffice
2. Go to the **Library Migration** tab
3. Click **Restore** to revert all properties to their original element type data

A snapshot is stored automatically during migration and is required for restore to be available.

### Checking migration status

The tab shows current status on load:

- **Not migrated** — no migration has run for this document
- **Migrated** — migration has completed; restore is available
- **Partially migrated** — migration ran but some properties could not be converted (check logs)

## Requirements

- Umbraco 18.0.0 or later
- .NET 10.0 or later

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

Built for Umbraco 18+ by [Growcreate](https://growcreate.co.uk).
