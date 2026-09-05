# Installing tia-station-mcp

This is a desktop tool, not a service. It runs on one Windows PC, the same one that has TIA Portal
V20 installed and licensed, and nothing it produces leaves that machine.

That is not a limitation waiting to be engineered away. Openness is an in-process API against an
installed TIA Portal, so there is no container, no Linux host and no cloud version. It also means no
audit trail travels anywhere and nothing phones home.

Read this from the top. Two of the steps fail in ways that point at the wrong thing, and both are
called out where they happen.

---

## 1. What this machine has to have

| | Needed for | If it is missing |
|---|---|---|
| **TIA Portal V20**, with the Openness option, licensed | Everything | Nothing runs |
| Membership of the Windows group **`Siemens TIA Openness`** | Everything | The server refuses to start |
| **.NET Framework 4.8** | The server | It will not launch |
| **PLCSIM Advanced V20**, with its own licence | Downloading and reading tags | The server still compiles and exports; every simulation tool reports the runtime as unavailable |
| **Node 22.6 or newer** | The harness, the dashboard, the documentation lookup | The MCP server itself is unaffected |

**V21 will not do.** This server targets V20, and the Openness API is versioned: a V21 installation
is a different API, not a newer one.

### Check it rather than assume it

`Test-Preconditions.ps1` ships inside the release artefact and lives in `scripts/` in the
repository. Unzip first (step 2), then from that folder:

```powershell
.\scripts\Test-Preconditions.ps1
```

It reads the machine and changes nothing — it does not install, grant a group, or write a setting.
Each thing it cannot find comes with the sentence that fixes it. It exits 0 when everything required
is there and 1 when it is not, so it can gate a script.

If TIA Portal is somewhere other than the default:

```powershell
.\scripts\Test-Preconditions.ps1 -TiaPortalLocation 'D:\Siemens\Automation\Portal V20'
```

### The group is granted at sign-in, not when you are added

This is the first thing most installations get wrong.

```powershell
# as an administrator
net localgroup "Siemens TIA Openness" "%USERNAME%" /add
```

Then **sign out of Windows and back in**. Windows puts group membership into the token it builds at
sign-in, so until you do, the account is a member and the server still refuses to start. The
precondition check tests the token this session actually holds rather than the group's member list,
for exactly this reason: reading the list would tell you everything is fine while nothing works.

---

## 2. Install the server

Take the release artefact — `tia-station-mcp-<version>.zip` — and unzip it wherever you keep tools.
There is no installer and nothing is written outside the folder you choose.

It unpacks into a folder carrying its version — `tia-station-mcp-0.0.18\` and so on — so two releases can
sit side by side. That is deliberate: the paths you are about to put into a host configuration name
a specific build, and an upgrade should be a change you make rather than one that happens to you.

**Check the hash before you run it.** The release publishes a SHA-256; compare it:

```powershell
Get-FileHash .\tia-station-mcp-<version>.zip -Algorithm SHA256
```

This matters more here than it usually would. TIA Portal binds its Openness whitelist to the exact
executable, so a different build is a different program as far as TIA is concerned — see the next
section.

---

## 3. First run, and the trap that comes with it

The first time a given executable connects, **TIA Portal shows a confirmation dialog** asking
whether to trust it. While that dialog is open, the connection blocks, and after a while the client
reports:

> Request timed out

That message points at the server. The cause is a dialog on your screen, possibly behind another
window. Bring TIA Portal to the front, confirm, and connect again.

It happens once per executable. Upgrade to a new release and it happens again, which is why
releases are versioned and few rather than rebuilt on every change.

---

## 4. Wire it into a host

The server speaks **MCP over stdio**. All of its logging goes to stderr, because with stdio the
standard output *is* the protocol channel.

### Claude Code

```powershell
claude mcp add tia-station -- "C:\tools\tia-station-mcp\TiaMcpServer.exe" --policy "C:\projects\my-cell\.tia-mcp\policy.json" --audit "C:\projects\my-cell\.tia-mcp\audit.jsonl" --backups "C:\projects\my-cell\.tia-mcp\backups"
```

### Claude Desktop

In `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "tia-station": {
      "command": "C:\\tools\\tia-station-mcp\\TiaMcpServer.exe",
      "args": [
        "--policy", "C:\\projects\\my-cell\\.tia-mcp\\policy.json",
        "--audit", "C:\\projects\\my-cell\\.tia-mcp\\audit.jsonl",
        "--backups", "C:\\projects\\my-cell\\.tia-mcp\\backups"
      ]
    }
  }
}
```

Restart the host after editing the file.

---

## 5. Configuration

Every path is an argument or a default. Nothing is compiled in.

| Argument | Default | What it is |
|---|---|---|
| `--policy` | `.tia-mcp/policy.json` | Which targets may be written |
| `--audit` | `.tia-mcp/audit.jsonl` | The trail of everything that changed |
| `--backups` | `.tia-mcp/backups` | Where the previous state is exported before a write |
| `--knowledge-index` | `.tia-mcp/harness/knowledge.db` | The documentation index, when there is one |
| `--knowledge-lookup` | `harness/src/knowledge/hardwareLookup.ts` | The lookup the server runs through Node |
| `--logging` | off | `1` stderr, `2` debug output, `3` Windows event log |

The defaults are **relative to the working directory**, which means they land beside the project you
are working on rather than beside the executable. A backup of one project's blocks belongs with that
project.

### The write policy, and why an empty machine can do nothing

**With no `policy.json`, every write is refused.** That is deliberate and it is the rule the whole
governance layer is built on: the absence of a decision is a refusal, never a permission.

Copy `.tia-mcp/policy.example.json` to `.tia-mcp/policy.json` and edit it. The example explains the
four families of target and the matching rules. In short: `*` stands for any run of characters and
is not a regular expression, deny beats allow, and a target matching neither list is refused.

Reads are not affected. Listing blocks, exporting, compiling and reading tags need no policy at all.

---

## 6. The smoke path

Four steps, in this order. If all four work, the installation is good.

1. **Connect.** Ask the host to call `Connect`. First time, expect the whitelist dialog from step 3.
2. **Open a project** with `OpenProject`, giving the full path to the `.ap20` file.
3. **Compile** with `CompileSoftware` on a PLC software path such as `PLC_0`.
4. **Download to PLCSIM and read a tag** with `DownloadToSimulation` and then `ReadSimulationTags`.

Step 4 needs PLCSIM Advanced and a virtual controller whose address is set. A new instance reports
`0.0.0.0` until one is, and TIA Portal cannot download to it in that state — `DescribeSimulationConnection`
exists to say which of those is the problem.

**Steps 3 and 4 need `policy.json`.** Connecting and opening a project are reads, but compiling
goes through the same guard every write does — it changes what is in the project — so `PLC_0` has to
be allowed, and downloading needs both `PLC_0` and `simulation/*`. The example policy allows exactly
those, which is why it is the one to start from.

If step 3 comes back refused, the policy is the first thing to look at, not the compiler.

---

## 7. What runs where

Everything on this one machine.

| Piece | What it needs |
|---|---|
| `TiaMcpServer.exe` | TIA Portal V20, .NET 4.8, the group |
| The harness | Node, and the server built or installed |
| The API and dashboard | Node, loopback only |
| The knowledge index | Node, and documents you supply yourself |

The dashboard and its API listen on loopback and nowhere else. That is not a default to relax: the
API serves the audit trail and the location of every backup, and exposing it publishes a record of
everything the server has changed on this machine.

---

## When something does not work

| What you see | What it usually is |
|---|---|
| `Request timed out` on the first connect | The whitelist dialog, waiting behind a window |
| The server exits immediately, saying so on stderr | The group, and a sign-out still pending |
| Every write refused | No `policy.json`, or the target is in neither list |
| Simulation tools report the runtime unavailable | PLCSIM Advanced is not installed |
| A tag list comes back empty | The controller holds no program: download first |
| `Cross-thread operation is not valid in Openness within STA` | The executable was launched in a way that overrides its apartment state |

The log is the first place to look, and it is off by default. Add `--logging 1` to send it to stderr,
which is where your host will show it.
