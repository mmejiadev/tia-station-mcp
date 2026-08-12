# tia-station-mcp

An MCP server for Siemens TIA Portal aimed at verified PLC code generation and at
coordinating multi-station cells.

Final project for the CFGS in Industrial Automation and Robotics.

## What it is for

Closing the whole loop, not just generating code:

```
specification → generate SCL → import into TIA → compile → read errors
                      ↑                                        │
                      └──────────────── fix ←──────────────────┘
                                          │
                                          ▼
                              test on PLCSIM Advanced
                                          │
                                          ▼
                                    export to Git
```

An LLM generating PLC code does not get it right 100 % of the time. Reliability does not
come from the model: it comes from **the compiler and the tests**. That is why the closed
loop is the core of the design and not an add-on.

## Status

See [`docs/STATUS.md`](docs/STATUS.md).

## Base and attribution

Built on [heilingbrunner/tiaportal-mcp](https://github.com/heilingbrunner/tiaportal-mcp)
(MIT), from which we inherit the architecture, code conventions and error model.

Analysis of the seven reference repositories in
[`docs/REFERENCE-REPOS.md`](docs/REFERENCE-REPOS.md).

## Requirements

- Windows x64
- TIA Portal V20 with the Openness component
- User in the `Siemens TIA Openness` Windows group
- .NET Framework 4.8
- PLCSIM Advanced (for the test phase)

## What this adds on top of the base

What `tiaportal-mcp` does not cover and we add here:

- Tag tables (`PlcTagTable`) — export/import
- Writing SCL directly through an external source
- PLCSIM Advanced integration for automated tests
- Full project snapshot to text for Git
- An instantiable "station" pattern generator

## Safety

By default, **every deployment targets PLCSIM Advanced**. Downloading to a physical PLC
requires explicit confirmation. See [`CLAUDE.md`](CLAUDE.md).
