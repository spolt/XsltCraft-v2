# Contributing to XsltCraft

## Branch Strategy

We use a simplified Git Flow with the following branch structure:

```
main (production)
├── develop (integration)
    ├── feature/* (new features)
    ├── bugfix/* (non-urgent fixes)
    └── chore/* (refactoring, docs)
└── hotfix/* (critical fixes, branched from main)
```

### Main Rules

- **`main`** — Production-ready code. Only merged from `develop` or `hotfix` via PR.
- **`develop`** — Integration branch. All feature PRs target this.
- **`feature/*`** — Feature branches: `feature/template-designer`, `feature/s3-storage`
- **`bugfix/*`** — Bug fixes: `bugfix/auth-token-expiry`
- **`hotfix/*`** — Critical production fixes: `hotfix/xslt-injection-vulnerability`
- **`chore/*`** — Refactoring, docs, tooling: `chore/update-dependencies`

## Branching Workflow

### Starting a Feature

```bash
git checkout develop
git pull origin develop
git checkout -b feature/my-feature
```

### Pushing & Creating a PR

```bash
git push origin feature/my-feature
```

Then create a PR on GitHub:
- Target: `develop`
- Title: `[Feature] Short description` or `[Fix] ...`, `[Docs] ...`
- Link related issues: Closes #123

### Code Review

- Minimum 1 approval required before merge
- CI/CD pipeline must pass
- No force-push after PR is open

### Merging

Use **"Squash and merge"** for features to keep `develop` history clean:

```bash
git merge --squash feature/my-feature
git commit -m "feat: add template designer (#123)"
```

## Commit Message Convention

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat** — New feature (`feat: add XSLT validation`)
- **fix** — Bug fix (`fix: auth token expiry`)
- **docs** — Documentation (`docs: update README`)
- **style** — Code style, formatting (no logic change)
- **refactor** — Code refactoring (`refactor: extract template logic`)
- **perf** — Performance improvement
- **test** — Adding/updating tests
- **chore** — Build, deps, tooling (`chore: upgrade React 19.1`)
- **ci** — CI/CD changes

### Examples

```
feat(editor): add real-time XSLT validation

- Validate XSLT syntax as user types
- Show validation errors inline
- Update template preview on valid changes

Closes #42
```

```
fix(auth): handle expired refresh tokens gracefully

Previously, expired tokens caused a 401 without redirect to login.
Now we detect expiry and silently refresh or redirect.

Fixes #89
```

## Release Workflow (Admin Only)

Releases are triggered **manually** by creating a git tag:

```bash
# Ensure main is up-to-date
git checkout main
git pull origin main

# Create annotated tag (triggers GitHub Actions)
git tag -a v0.2.0 -m "Release v0.2.0: Template designer + S3 storage"
git push origin v0.2.0
```

This automatically:
1. Runs CI/CD
2. Creates a GitHub Release
3. Populates release notes from CHANGELOG.md

See [VERSION_STRATEGY.md](./VERSION_STRATEGY.md) for versioning rules.

## Pull Request Checklist

Before submitting:

- [ ] Branch is up-to-date with `develop`
- [ ] Commit messages follow Conventional Commits
- [ ] Code passes linting: `npm run lint`
- [ ] Tests pass: `npm run test` (frontend), dotnet test (backend)
- [ ] No console errors or warnings
- [ ] TypeScript strict mode passes
- [ ] CHANGELOG.md updated (if user-facing change)

## Code Style

- **Frontend**: ESLint + Prettier (auto-format on save)
- **Backend**: C# conventions (PascalCase types, camelCase members)
- **Formatting**: Prettier (100 char line length)

Run locally:

```bash
# Frontend
npm run lint
npm run format

# Backend
dotnet format
```

## Questions?

- Open an issue for bugs or feature requests
- Start a discussion for architectural questions
- Tag maintainers: @spolt
