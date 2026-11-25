# Changelog

What each release of the server brings. A release is the `AssemblyVersion` of `src/TiaMcpServer/TiaMcpServer.csproj`, an
annotated git tag `vX.Y.Z` on the merge commit in `main` that carries it, and a GitHub release with the artefact
`scripts/New-Release.ps1` packs from that tag and its SHA-256. The detail of every change, with what was measured, is in
`docs/STATUS.md`; this file says what a user gets.

## 0.0.21 - 2026-11-25

Built for **TIA Portal V21** (the V20 build remains possible with `-p:TiaVersion=20` on a machine that has it). **188 tools**,
from 98 in 0.0.20. Pull requests #46 to #88.

- **TIA Portal V21.** The server, its tests and every Openness fact it relies on were moved to V21 and measured again there
  (#56, #68 and every `work/v21-*` branch after it); it attaches to, or works beside, a TIA Portal a person has open (#55).
- **Classic WinCC:** devices, connections and events of HMI screens (#46-#48).
- **WinCC Unified:** events, animations, text lists, tags, alarms, logging, log settings and trends (#49-#54, #59, #61);
  faceplates and their library types (#60); global script modules, read, written and checked, including calls into them
  (#64, #80); Custom Web Controls (#75); some items of a screen changed and the rest left (#78, #81).
- **Libraries and master copies:** library types and versions, global libraries of one's own, faceplates moved between
  versions (#60, #62); master copies (#63); types from SIMATIC SD documents (#65); library maintenance (#76); and closing
  roadmap 2.2 - block types into a program, instances moved to another version, versions compared by content, new versions to
  a global library, global libraries archived and retrieved, type folders, master copies deleted (#84).
- **Programs:** SCL with a `TYPE` (#59); backups of uncompiled SCL (#57); single rows of a tag table and single members of a
  global DB edited (#82, #85); `CreateTag` and `EditTagTable` refuse an address that is not one; PEEK and POKE calls that
  compile and fail quietly reported when SCL is written, and shown in the plan before the write (#79, #85).
- **Hardware and networks:** IP settings (#73), connections between PLCs (#74), HMI and PLC access (#72), devices created in a
  device group, and a PLC with its HMI and their connection placed from a device group template (#67, #69, #87).
- **Online and tables:** going online through PLCSIM again (#83), watch and force tables (#71).
- **Version control:** TIA Portal's VCI and git in its workspace (#70).
- **Secrets:** kept encrypted with Windows DPAPI and set from a terminal (#66); a template placed with a stored secret is
  governed on that secret (`secret/NAME`).
- **Studies:** PEEK and POKE (#77), drives, servos and technology objects (#86).

**Upgrading from 0.0.20.** TIA Portal asks again to trust the new executable (the Openness whitelist is bound to the exact
file). Write tools added since 0.0.20 are refused until the write policy names their targets - among them `secret/NAME` to
place a device group template with a stored secret, and `master-copy-delete/<path>` to delete master copies.
`.tia-mcp/policy.example.json` now allows, in Study Mode, the devices TIA Portal names by default (`HMI_*`, `PLC_*`,
`PC-System_*`), so a device the server has just created can be written without naming it first.

## 0.0.20 - 2026-10-16

The server reports its own version (`GetState`), and the harness's telemetry records it. 98 tools. Earlier releases were not
recorded here.
