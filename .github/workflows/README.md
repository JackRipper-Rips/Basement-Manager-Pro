# GitHub Workflows

## Creating a New Release

Releases are **only created for tagged versions**, not on every commit. This keeps the release list clean and meaningful.

### Method 1: Create Tag Locally (Recommended)

```bash
# Create and push a version tag
git tag v1.0.0
git push origin v1.0.0
```

The build workflow will automatically:
1. Build the self-contained single-file executable
2. Include `depots.ini` for depot name lookups
3. Create a GitHub release with the files
4. Generate release notes from commits

### Method 2: Manual Workflow Dispatch

1. Go to **Actions** → **Build and Release**
2. Click **Run workflow**
3. Enter version tag (e.g., `v1.0.0`)
4. Click **Run workflow**

## Version Naming Convention

Use semantic versioning: `v{major}.{minor}.{patch}`

Examples:
- `v1.0.0` - Initial release
- `v1.1.0` - New features
- `v1.1.1` - Bug fixes

## What Gets Published

Each release includes:
- **SolusManifestApp.exe** - Self-contained single executable (~70-80MB)
  - No .NET runtime required
  - Everything embedded in one file
  - Compressed for smaller size
  - Resources are embedded and extracted automatically
- **depots.ini** - Depot name database (must be in same folder as exe)

## Auto-Update System

The app automatically:
1. Checks for new releases on startup
2. Downloads `SolusManifestApp.exe` directly (no zip extraction needed)
3. Replaces the old exe with the new one
4. Restarts the application

Users can configure auto-update behavior in Settings:
- **Disabled** - Never check for updates
- **Check Only** - Notify but don't download
- **Auto Download and Install** - Fully automatic

## Workflow Files

- **build.yml** - Main build and release workflow (runs on version tags only)
- **release.yml** - Legacy workflow (deprecated, kept for reference)

## Important Notes

- ✅ The workflow **only** triggers on version tags (e.g., `v1.0.0`)
- ✅ **No automatic releases** on every commit
- ✅ Direct .exe downloads (no more zip files)
- ✅ Build artifacts retained for 90 days
- ✅ Includes `depots.ini` for depot name lookups
