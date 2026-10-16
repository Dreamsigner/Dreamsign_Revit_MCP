# Third-party notices

Dreamsign Revit MCP's own source is MIT licensed (Copyright 2026 Dreamsigner).
Dependency licenses remain applicable to their respective components.

- **ModelContextProtocol and ModelContextProtocol.Core 2.2.0**: Apache License 2.0;
  copyright © Model Context Protocol a Series of LF Projects, LLC.
  Source: https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.2.0
  License: `licenses/MCP-Apache-2.0.txt`.
- **Microsoft.Extensions.*, System.Diagnostics.EventLog and .NET runtime**:
  Microsoft and contributors; MIT license and the additional third-party notices
  supplied with .NET. Copies: `licenses/DOTNET-LICENSE.txt` and
  `licenses/DOTNET-ThirdPartyNotices.txt`.
  Source: https://github.com/dotnet/runtime and https://github.com/dotnet/extensions

Resolved package versions and integrity hashes are recorded in each project's
`packages.lock.json`. The server's `Dreamsign.Revit.Mcp.deps.json` identifies the
components used by a particular build. A self-contained build includes the .NET
runtime; a framework-dependent build uses the locally installed runtime.

Autodesk Revit and its SDK assemblies are proprietary products. They are not
distributed in this repository or copied into the plugin package. A licensed
local Revit 2027 installation is required. This is an independent project and
does not imply endorsement by Autodesk or the MCP SDK maintainers.

- **Tomlyn 0.19.0**, copyright Alexandre Mutel: BSD 2-Clause, used for preserving
  and updating client TOML configuration. Source: https://github.com/xoofx/Tomlyn/tree/0.19.0
  License: `licenses/Tomlyn-BSD-2-Clause.txt`.
- **WiX Toolset / Burn 6.0.2**, .NET Foundation and contributors: Microsoft
  Reciprocal License, with its terms reproduced in
  `licenses/WiX-MS-RL.txt`. Corresponding source:
  https://github.com/wixtoolset/wix/tree/b3f340393117094a75ea8ced77f2357e4aa095e7
  The development tools' binary distribution also includes an Open Source
  Maintenance Fee agreement, reproduced in `installer/OSMFEULA.txt` in the source
  repository. Developers must review those terms for their use of the build
  tools. No paid service or purchase is performed by the application installer.
  The MSI also embeds the DTF custom-action host and WindowsInstaller library
  from the same WiX 6.0.2 source and license. The C# uninstall guard uses the
  Windows-provided .NET Framework 4.8 runtime; it does not redistribute it.
