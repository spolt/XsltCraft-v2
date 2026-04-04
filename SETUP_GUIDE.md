# XsltCraft Versioning & Release Setup

Complete guide for implementing Git Workflow, Versioning, and Automated Release Management.

---

## 📋 Checklist

- [ ] Clone/pull latest `develop` branch
- [ ] Copy new files to repo root
- [ ] Create `.github/workflows/` directory if missing
- [ ] Add GitHub Actions workflows
- [ ] Configure branch protection rules
- [ ] Test release workflow
- [ ] Document in team wiki

---

## 🚀 Implementation Steps

### Step 1: Add Documentation Files

Copy these to **repo root**:

```bash
# From /home/claude/
cp CONTRIBUTING.md <repo-root>/CONTRIBUTING.md
cp VERSION_STRATEGY.md <repo-root>/VERSION_STRATEGY.md
cp CHANGELOG.md <repo-root>/CHANGELOG.md
```

**Files:**
- **CONTRIBUTING.md** — Branching strategy, PR process, commit conventions
- **VERSION_STRATEGY.md** — SemVer rules, tagging format, release policy
- **CHANGELOG.md** — Keep a Changelog format with v0.1.0 as first entry

### Step 2: Add GitHub Actions Workflows

Create `.github/workflows/` in repo root:

```bash
mkdir -p .github/workflows
cp release.yml <repo-root>/.github/workflows/release.yml
cp ci.yml <repo-root>/.github/workflows/ci.yml
```

**Workflows:**

#### `release.yml` (New)
- **Trigger:** Tag push matching `v*` (e.g., `v1.2.0`)
- **Actions:**
  1. Extract version from tag
  2. Parse CHANGELOG.md for that version
  3. Create GitHub Release with release notes
  4. Mark as prerelease if version contains `-` (alpha, beta, rc)

#### `ci.yml` (Enhanced)
- **Trigger:** Pushes to `main`/`develop`, pull requests, manual
- **Actions:**
  1. Frontend: lint, build, test, coverage
  2. Backend: format check, build, test
  3. Validate CHANGELOG.md structure on main branch

### Step 3: Configure Branch Protection Rules

Go to **Settings → Branches → Add rule**

#### For `main` branch:

```
Rule pattern: main

Require a pull request before merging:
  ✓ Require approvals: 1
  ✓ Require review from code owners (optional)

Require status checks to pass before merging:
  ✓ CI / frontend (required)
  ✓ CI / backend (required)
  ✓ require-up-to-date (before merge)

Other protections:
  ✓ Include administrators in restrictions
  ☐ Allow force pushes: NO
  ☐ Allow deletions: NO
```

#### For `develop` branch:

```
Rule pattern: develop

Require a pull request before merging:
  ✓ Require approvals: 1

Require status checks to pass before merging:
  ✓ CI / frontend (required)
  ✓ CI / backend (required)
  ✓ require-up-to-date (before merge)

Other protections:
  ✓ Include administrators in restrictions
  ☐ Allow force pushes: NO
  ☐ Allow deletions: NO
```

---

## 📝 Git Workflow Examples

### Starting a Feature

```bash
# Update develop
git checkout develop
git pull origin develop

# Create feature branch
git checkout -b feature/template-import

# Work...
git add .
git commit -m "feat: add XSLT template import from file"

# Push and create PR
git push origin feature/template-import
```

PR settings:
- Base: `develop`
- Title: `[Feature] Add XSLT template import from file`
- Merge method: Squash and merge

### Creating a Release (Admin)

```bash
# 1. Update develop & main
git checkout develop
git pull origin develop
git checkout main
git pull origin main

# 2. Merge develop → main
git merge develop

# 3. Update version files
# - CHANGELOG.md: move [Unreleased] → [X.Y.Z]
# - package.json: "version": "X.Y.Z"
# - .csproj: <Version>X.Y.Z</Version>

# 4. Commit
git add .
git commit -m "chore: release v0.2.0"
git push origin main

# 5. Create tag (triggers release.yml)
git tag -a v0.2.0 -m "Release v0.2.0: Template import + S3 storage"
git push origin v0.2.0

# GitHub Actions automatically:
# ✅ Runs CI
# ✅ Creates GitHub Release with CHANGELOG
```

### Hotfix (from main)

```bash
# 1. Create hotfix branch from main
git checkout main
git pull origin main
git checkout -b hotfix/auth-token-bug

# 2. Fix + commit
git commit -m "fix: handle expired refresh tokens correctly"
git push origin hotfix/auth-token-bug

# 3. Create PR to main, merge
# (PR should be reviewed & approved)

# 4. Tag immediately
git checkout main
git pull origin main
git tag -a v0.1.1 -m "Release v0.1.1: Hotfix auth token expiry"
git push origin v0.1.1
```

---

## 🔖 Tagging Conventions

**Required format:** `v<MAJOR>.<MINOR>.<PATCH>[-PRERELEASE]`

### Valid Examples
- `v1.0.0` — Stable release
- `v1.2.3` — Patch release
- `v2.0.0-alpha.1` — Alpha
- `v2.0.0-beta.2` — Beta
- `v2.0.0-rc.1` — Release candidate

### Invalid (will NOT trigger release)
- `1.0.0` (missing `v`)
- `version-1.0.0`
- `v1.0.0-beta` (missing sequence number)

---

## 📚 SemVer Quick Reference

| Type | When | Example |
|------|------|---------|
| **MAJOR** | Breaking change | v0.1.0 → v1.0.0 |
| **MINOR** | New feature (backward-compatible) | v1.0.0 → v1.1.0 |
| **PATCH** | Bug fix | v1.1.0 → v1.1.1 |
| **Prerelease** | Alpha/beta/rc | v1.2.0-beta.1 |

---

## ✅ Testing the Setup

### 1. Test PR → CI workflow

```bash
git checkout develop
git checkout -b feature/test-workflow
echo "# Test" >> README.md
git add README.md
git commit -m "chore: test workflow"
git push origin feature/test-workflow
```

- Create PR to `develop`
- Verify CI runs ✓
- Verify require 1 approval ✓
- Merge with "Squash and merge" ✓

### 2. Test Release Workflow

```bash
git checkout main
git pull origin main

# Create lightweight tag for testing (delete after)
git tag v0.0.1-test.1
git push origin v0.0.1-test.1

# Check: https://github.com/spolt/XsltCraft-v2/actions
# Should see: Release workflow running
# Check: https://github.com/spolt/XsltCraft-v2/releases
# Should see: Draft release with CHANGELOG
```

Then delete test tag:
```bash
git tag -d v0.0.1-test.1
git push origin :v0.0.1-test.1
```

---

## 📖 References

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Semantic Versioning](https://semver.org/)
- [Keep a Changelog](https://keepachangelog.com/)
- [GitHub Actions: Push Events](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows#push)

---

**Last Updated**: 2024-02-15
**Status**: Ready for implementation
