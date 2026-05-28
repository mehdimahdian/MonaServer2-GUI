# Contributing to MonaServer2 GUI

Thank you for your interest in contributing!  
**Maintainer:** Mehdi Mahdian ([m.mahdian@gmail.com](mailto:m.mahdian@gmail.com))

---

## Before You Start

- Check [existing issues](https://github.com/mehdimahdian/MonaServer2-GUI/issues) to avoid duplicates
- For significant changes, open an issue first to discuss the approach
- All contributions must comply with the **GPL-3.0** license

---

## Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and [pnpm](https://pnpm.io/installation)
- [MonaServer2 binary](https://github.com/MonaSolutions/MonaServer2) (bundled in `tools/monaserver2/`)
- For OBS plugin: CMake 3.28+, libsrt, libcurl (see [obs-plugin/README.md](obs-plugin/README.md))

### Setup

```bash
git clone https://github.com/mehdimahdian/MonaServer2-GUI
cd MonaServer2-GUI

dotnet restore
cd webui && pnpm install && pnpm build && cd ..

# Terminal 1 — service
dotnet run --project src/MonaServer2.Service

# Terminal 2 — desktop app
dotnet run --project src/MonaServer2.Desktop
```

---

## Project Structure

See [DEVELOPMENT.md](DEVELOPMENT.md) for a full breakdown of the solution structure, key file locations, and design decisions.

---

## Pull Request Process

1. Fork the repository and create a branch from `main`
2. Naming: `feature/short-description` or `fix/issue-number-description`
3. Write or update tests for your change
4. Ensure `dotnet build` and `dotnet test` pass with zero errors
5. Ensure the web UI builds: `cd webui && pnpm build`
6. Submit the PR against `main` with a clear description of what changed and why

---

## Code Style

- C# 12, `enable` nullable, implicit usings
- No comments unless the **why** is genuinely non-obvious
- Run `dotnet format` before committing
- Follow existing patterns in the codebase
- No new dependencies without discussion

---

## Reporting Bugs

Use the [bug report template](https://github.com/mehdimahdian/MonaServer2-GUI/issues/new?template=bug_report.yml). Include:

- OS and version
- MonaServer2 GUI version
- MonaServer2 binary version
- Steps to reproduce
- Expected vs. actual behaviour
- Relevant logs (from the Log Viewer or `%APPDATA%\MonaServer2-GUI\logs\`)

---

## MonaServer2 Credit Requirement

Any UI text, documentation, or release notes that mention streaming functionality **must** acknowledge that this is provided by [MonaServer2](https://github.com/MonaSolutions/MonaServer2) (MonaSolutions / Haivision).

---

## Security

See [SECURITY.md](SECURITY.md) for how to report security vulnerabilities.
