# Version Strategy

XsltCraft uses **Semantic Versioning (SemVer 2.0.0)** for all releases.

## Version Format

```
MAJOR.MINOR.PATCH-PRERELEASE+BUILD

Example: v1.2.3-beta.1+20240101
```

### Rules

- **MAJOR** — Increment on breaking changes (breaking API, migration required)
  - Example: v0.1.0 → v1.0.0 (public release, major feature set)

- **MINOR** — Increment on backward-compatible feature additions
  - Example: v1.0.0 → v1.1.0 (add S3 storage, new block types)

- **PATCH** — Increment on backward-compatible bug fixes
  - Example: v1.1.0 → v1.1.1 (fix token expiry bug, hotfix)

- **PRERELEASE** (optional) — For alpha, beta, RC versions
  - Format: `-alpha.1`, `-beta.2`, `-rc.1`
  - Example: v1.2.0-beta.1

## Current Release Policy

### From `develop` → `main` (Minor/Major Release)

1. Update `CHANGELOG.md` with all changes since last release
2. Update `version` in `package.json` and `.csproj` files
3. Create PR from `develop` → `main`
4. After merge, tag main:

```bash
git checkout main
git pull origin main
git tag -a v1.2.0 -m "Release v1.2.0: Feature X, Feature Y"
git push origin v1.2.0
```

GitHub Actions automatically creates a **GitHub Release** from the tag.

### From `main` (Hotfix/Patch Release)

Hotfixes branch directly from `main` and are tagged immediately after merge:

```bash
git checkout main
git pull origin main
git checkout -b hotfix/critical-bug
# ... fix code ...
git commit -m "fix: critical security vulnerability"
git push origin hotfix/critical-bug
# Create & merge PR to main
git checkout main
git pull origin main
git tag -a v1.1.1 -m "Release v1.1.1: Hotfix critical bug"
git push origin v1.1.1
```

## Tag Format

All tags must follow this format:

```
v<MAJOR>.<MINOR>.<PATCH>[-PRERELEASE]

Valid: v1.0.0, v1.2.3, v2.0.0-beta.1, v0.1.0-alpha.2
Invalid: 1.0.0, version-1.0.0, v1.0.0-beta
```

## Release Notes (GitHub)

When a tag is pushed, GitHub Actions:

1. Reads `CHANGELOG.md`
2. Extracts the section for that version
3. Creates a GitHub Release with the changes

### Example Changelog Entry

```markdown
## [1.2.0] - 2024-02-15

### Added
- S3 storage integration for XSLT templates
- Real-time XSLT validation in editor
- Export templates as ZIP

### Changed
- Improved template preview rendering
- Refactored authentication to use refresh tokens

### Fixed
- Auth token expiry handling (#89)
- XSLT namespace resolution in nested templates (#92)

### Security
- Added rate limiting to API endpoints
```

## Version Stability

- **v0.x.y** — Alpha/Beta phase. Breaking changes allowed with minor version bump.
- **v1.0.0+** — Stable release. Breaking changes only on MAJOR bump.

## Files to Update on Release

When releasing a new version:

1. `CHANGELOG.md` — Add new section with changes
2. `package.json` — Update `"version"`
3. `.csproj` files — Update `<Version>`
4. Git tag — `git tag -a v<VERSION>`

## Automations

### GitHub Actions (release.yml)

On tag push matching `v*`:

```yaml
on:
  push:
    tags:
      - 'v*'
```

1. ✅ Run CI pipeline
2. ✅ Parse CHANGELOG for that version
3. ✅ Create GitHub Release with release notes
4. ✅ Optional: Publish to registries (npm, NuGet)

---

**Last Updated**: 2024-02-15
