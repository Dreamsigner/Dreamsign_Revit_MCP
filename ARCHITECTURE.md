# Architecture

Dreamsign Revit MCP has two processes: an MCP server launched by the assistant,
and an add-in hosted by Revit. They communicate through a local named pipe.

```text
MCP client --stdio--> Server --named pipe--> Revit add-in --ExternalEvent--> Revit API
                        |                       |
                    Contracts <-------------- Desktop (WPF)
```

## Components

| Project | Responsibility | Project dependencies |
|---|---|---|
| `src/Dreamsign.Contracts` | Tool catalogue, bounded schema validation, target identity, local state and client configuration | None |
| `src/Dreamsign.Server` | MCP SDK host, target discovery, dispatch and operation reports | Contracts |
| `src/Dreamsign.Desktop` | WPF project selection, assistant configuration and diagnostics | Contracts |
| `src/Dreamsign.Revit` | Pipe endpoint, Revit thread dispatch, transactions and BIM operations | Contracts, Desktop; local Autodesk API |
| `installer/Guard` | MSI action that waits for Revit sessions to exit | No project dependency; links the shared theme |
| `tests/Dreamsign.Tests` | Contract, transport, state, UI and opt-in native tests | Server, Desktop; links guard sources |

Server, Contracts and the add-in use .NET 10. Desktop targets Windows/WPF.
The MSI guard targets .NET Framework 4.8 because it is hosted by WiX DTF.

## Request path

1. `ToolCatalog` selects the profile and loads the shared `tool-catalog.json` resource.
2. `BridgeRuntime` validates arguments and acquires the local-state lease.
3. `TargetResolver` matches the saved document title and full path. Multiple
   matches are rejected; selecting another Revit tab does not authorize it.
4. `NamedPipeTransport` sends a request with a unique ID and expected target.
5. The add-in validates the request again and schedules an `ExternalEvent`.
   The handler checks the active document and performs the Revit operation.
6. The server checks response identity and retains the detailed operation report
   before returning a compact response.

The pipe is restricted to the current Windows user. Requests are limited to
256 KiB and responses to 1 MiB. No write is automatically replayed after a timeout.

## State and writes

- Each user has a separate state directory under `%LOCALAPPDATA%\DreamsignRevitMCP`.
- A file lease excludes concurrent operations using the same state directory.
- State replacement is atomic. Selecting a different model backs up state and
  resets model-specific mappings; changing only the PID preserves them.
- Writes require a document token. Prepared plans expire after five minutes,
  are bound to document/revision/view, and can be applied once.
- Revit transactions own model changes. A successful edit and a successful save
  are distinct outcomes; `SAVE_FAILED` must not trigger another write.

## Installation and compatibility

The EXE bundles a machine-wide MSI. Shared binaries live under Program Files;
configuration, model permissions and reports remain per user. The installer
does not configure assistants or traverse other users' profiles.

The historical `AstraRevitBridge-2027-` pipe prefix and add-in identifiers are
retained for compatibility. Generated test/project names use `Dreamsign_MCP_`.
Autodesk DLLs are referenced locally and are not distributed.

See [installation](docs/MACHINE-INSTALLATION.md), [security](SECURITY.md) and
[release verification](docs/RELEASING.md) for the operational boundaries.
