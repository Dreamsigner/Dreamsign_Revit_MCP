# Working in this repository

Read [ARCHITECTURE.md](ARCHITECTURE.md) for component boundaries and
[CONTRIBUTING.md](CONTRIBUTING.md) for build, test and formatting commands.
User documentation is in French; C# identifiers and test names are in English.

## Required checks

- Restore with `--locked-mode`; review any intentional lock-file update.
- Run offline tests and the whitespace check before submitting changes.
- Keep formatting-only changes separate from behavior changes.
- Add a regression test for a behavior fix. Do not weaken a check to make it pass.
- Keep local paths, model files, logs, receipts and build output out of Git.

## Boundaries

- Contracts must not depend on Server, Desktop or the Autodesk API.
- Server must work and be testable without Revit or Desktop assemblies.
- Revit API calls belong in the add-in's ExternalEvent handler.
- Preserve document identity checks, transaction rollback and timeout semantics.
- Never retry a write when its outcome is unknown; inspect the model first.
- Keep test targets isolated and prefixed `Dreamsign_MCP_`.
- Do not close or restart Revit, install an add-in, or run native tests without
  explicit authorization for that operation and target.

## Documentation

Write for users and contributors, not for a past chat. Record decisions and
reproducible results; keep session transcripts and failed automation attempts
in private local records. The [documentation index](docs/README.md) identifies
current guides and historical records.
