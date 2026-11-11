# TiaMcpServer.Test

MSTest project verifying portal connectivity, project handling, devices, and MCP server behavior.

## Environment Prerequisites

- .NET Framework 4.8 installed
- Siemens TIA Portal V21 installed, and closed while the suite runs: it starts its own
- User in Windows group "Siemens TIA Openness"
- Env var `TiaPortalLocation` set to `C:\\Program Files\\Siemens\\Automation\\Portal V21`, or unset

The fixtures are V21 archives, so the suite runs against TIA Portal V21 only. A V20 build would need
V20 archives of the same projects.

## Test Assets
- `assets/TestProject1.zap21`, `UnifiedProject.zap21`, `FaceplateProject.zap21` – the projects the tests retrieve.
- `assets/TestProject1.zap20` – kept at V20 on purpose: the only archive that proves an upgrade.
- `assets/FaceplateLibrary.zip` – a global library, zipped, as a library is a folder.
- `TestSession1.als21` – create this multi-user session manually for session tests.

See `Settings.cs` for configuration options such as project paths and timeouts.

## Test Execution Policy

- Offer to run tests, but only execute them after explicit user confirmation. See root `AGENTS.md` for details.
