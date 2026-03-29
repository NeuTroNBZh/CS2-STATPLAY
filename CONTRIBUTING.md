# Contributing

Thanks for contributing to CS2-STATPLAY.

## Development Workflow

1. Fork and create a feature branch.
2. Keep changes scoped and documented.
3. Run local validation before opening a PR.

## Local Validation

```powershell
dotnet restore CSStat.sln
dotnet build CSStat.sln
dotnet test CSStat.sln
```

If your change affects packaging or release behavior, also validate:

```powershell
./scripts/package-release.ps1 -Configuration Release -Version 1.0.1 -PackageId CS2-STATPLAY -RuntimeIdentifier linux-x64
```

## Coding Rules

- Keep event handlers thin.
- Put business logic in dedicated services.
- Keep schema evolution additive when possible.
- Document architecture and decisions in `docs/journals`.

## Pull Request Checklist

- [ ] Scope and intent clearly described
- [ ] Build and tests passing
- [ ] Journals updated (`worklog`, `decisions`, `architecture`, `sources` as relevant)
- [ ] No unrelated file churn
