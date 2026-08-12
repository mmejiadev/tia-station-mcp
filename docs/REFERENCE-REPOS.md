# Reference repositories

Cloned into `../repos/`. Analysis of what each one contributes.

## 1. `tiaportal-mcp` — heilingbrunner ⭐ PROJECT BASE

**MIT licence.** MCP server in C# .NET 4.8 for TIA Portal V20.

This is our base. See `STATUS.md` for the full architecture and tool analysis.

Rule files we adopted:

- `AGENTS.md` — test policy, confirmation, environment, encoding
- `style.md` — C# conventions, MSTest tests, markdown
- `docs/error-model.md` — error model and exception decoration
- `gemini.md`, `TODO.md` — additional context

Ships `tests/TiaMcpServer.Test/assets/TestProject1.zap20`: **a real TIA V20 project for testing.**

## 2. `vscode-tiaportal-mcp` — heilingbrunner

A VS Code extension packaging the server above for GitHub Copilot.
TypeScript + esbuild. Useful as a reference for **packaging and distribution**:
how to launch the server `.exe` from an extension and expose it to the MCP client.

Relevant if we later want to distribute our work to classmates.

## 3. `CodeGeneratorOpenness` — mking2203

A code generator through Openness with a GUI. Full solution
(`CodeGeneratorOpenness.sln`) with `OPNS` and `Sample` folders.

Contributes: patterns for **importing blocks and data types**, manipulating the project
folder tree, and exporting/importing project texts. A good reference for phase 3.

## 4. `TiaExportBlocks` — cezar1

The smallest and most readable: a single `Program.cs`. Connects to TIA and exports:

- SCL functions → `.scl`
- data blocks → `.db`
- UDTs → `.udt`
- PLC tag tables → `.xml`

**Required reading before phase 2.** It is exactly the bulk export for Git, solved in one
file. It also includes the `dll` folder with the references.

Note: it exports **tag tables**, something `tiaportal-mcp` does not do. A direct source for
that gap.

## 5. `TIA-Openness-From-Python` — JL00001

Python, 7 files. Generates **SCL and LAD** and imports the logic into TIA Portal.
It also creates devices in "Devices & Network".

Key files: `xmlHeader.py`, `XML_Objects.py`, `SclObject.py`, `fb_block.py`, `FC_Object.py`.

Contributes: the **SimaticML XML structure** broken down into manageable objects. Even
though we do not use Python, it is the best practical documentation of the XML format in
these repos. Consult it when we get to generating LAD.

## 6. `tia-portal-openness-unified-library` — tia-portal-applications

A base library for Openness tools. Includes `UnifiedOpennessConnector`, an `IDisposable`
object meant to be used with `using` to guarantee the TIA Portal object is released.

Contributes: the correct **connection and lifecycle pattern**. Ships a `.gitlab-ci.yml`,
useful as a CI reference. It also covers HMI access.

## 7. `TIAOpennessManager` — StaniB88

⚠️ **Only 4 files: there is no source code.** It is a binary distribution repository
(`update.xml`, `CHANGELOG.md`). The application is closed source.

Still useful as a **reference for target functionality**: an SCL editor with highlighting,
inline diff, Git integration (status, commit, push, pull, diff). It is roughly the final
product we want, but we will have to implement it ourselves.

---

## Where to look, by phase

| I need... | Look in |
|---|---|
| Connection and lifecycle | `tia-portal-openness-unified-library`, `tiaportal-mcp/Siemens/Openness.cs` |
| Bulk export to text (phase 2) | `TiaExportBlocks/Program.cs` |
| Tag tables | `TiaExportBlocks/Program.cs` |
| Block import (phase 3) | `CodeGeneratorOpenness`, `tiaportal-mcp` |
| SimaticML XML format | `TIA-Openness-From-Python/XML_Objects.py` |
| Packaging and distribution | `vscode-tiaportal-mcp` |
| Product target | `TIAOpennessManager/README.md` |
