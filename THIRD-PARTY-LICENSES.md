# Third‑party licenses

This file lists third‑party libraries and other third‑party data found in the `licenses/` folder of this repository. Each entry links to the license text or other license artifacts that are included in the `licenses` directory.

If you add or update dependencies, please also add the corresponding license files into the `licenses/` folder and update this document.

- Project license
  - `LICENSE` (project main license) — (please check project root for SPDX id)

- MetadataExtractor
  - Files:
    - `licenses/MetadataExtractor/MetadataExtractor-2.9.0-license.txt`
    - `licenses/MetadataExtractor/MetadataExtractor-2.9.0-license-page.html`
    - `licenses/MetadataExtractor/MetadataExtractor-api.json`
  - License: Apache License 2.0
  - SPDX: `Apache-2.0`
  - Type: Permissive open-source

- Microsoft.Web.WebView2
  - Files:
    - `licenses/Microsoft.Web.WebView2/Microsoft.Web.WebView2-1.0.3719.77-license-page.html`
    - `licenses/Microsoft.Web.WebView2/Microsoft.Web.WebView2-api.json`
    - `licenses/Microsoft.Web.WebView2/Microsoft.Web.WebView2-1.0.3719.77-license.txt`
  - License: Microsoft/Proprietary (packaged license) — check vendor page
  - SPDX: `NOASSERTION` (explicit SPDX id not available in NuGet metadata)
  - Type: Vendor-specific / packaged license — consult NuGet or vendor terms for details

- Ookii.Dialogs.Wpf
  - Files:
    - `licenses/Ookii.Dialogs.Wpf/Ookii.Dialogs.Wpf-5.0.1-license.txt`
    - `licenses/Ookii.Dialogs.Wpf/Ookii.Dialogs.Wpf-5.0.1-license-page.html`
    - `licenses/Ookii.Dialogs.Wpf/Ookii.Dialogs.Wpf-api.json`
  - License: BSD 3-Clause
  - SPDX: `BSD-3-Clause`
  - Type: Permissive open-source

- SixLabors.Fonts
  - Files:
    - `licenses/SixLabors.Fonts/SixLabors.Fonts-2.1.3-license.txt`
    - `licenses/SixLabors.Fonts/SixLabors.Fonts-2.1.3-license-page.html`
    - `licenses/SixLabors.Fonts/SixLabors.Fonts-api.json`
  - License: SixLabors split license (may be Apache-2.0 or commercial for certain uses)
  - SPDX: `NOASSERTION` (complex dual/commercial model — check SixLabors terms)
  - Type: Source/usage conditional (see vendor terms)

- SixLabors.ImageSharp
  - Files:
    - `licenses/SixLabors.ImageSharp/SixLabors.ImageSharp-3.1.12-license.txt`
    - `licenses/SixLabors.ImageSharp/SixLabors.ImageSharp-3.1.12-license-page.html`
    - `licenses/SixLabors.ImageSharp/SixLabors.ImageSharp-api.json`
  - License: SixLabors split license (open/Apache-2.0 or commercial)
  - SPDX: `NOASSERTION` (use vendor license details; older versions used Apache-2.0)
  - Type: Source/usage conditional — verify project usage against SixLabors policy

- SixLabors.ImageSharp.Drawing
  - Files:
    - `licenses/SixLabors.ImageSharp.Drawing/SixLabors.ImageSharp.Drawing-2.1.7-license.txt`
    - `licenses/SixLabors.ImageSharp.Drawing/SixLabors.ImageSharp.Drawing-2.1.7-license-page.html`
    - `licenses/SixLabors.ImageSharp.Drawing/SixLabors.ImageSharp.Drawing-api.json`
  - License: SixLabors split license (see SixLabors)
  - SPDX: `NOASSERTION`
  - Type: Source/usage conditional

- XmpCore
  - Files:
    - `licenses/XmpCore/XmpCore-6.1.10.1-license.txt`
    - `licenses/XmpCore/XmpCore-6.1.10.1-license-page.html`
    - `licenses/XmpCore/XmpCore-api.json`
  - License: BSD 3-Clause
  - SPDX: `BSD-3-Clause`
  - Type: Permissive open-source

- LeafFleat / Leaffleat
  - Files:
    - `licenses/LeafFleat/Leaffleat.txt`
  - License: BSD 2‑Clause
  - SPDX: `BSD-2-Clause`
  - Type: Permissive open-source

- OpenStreetView / Open data
  - Files:
    - `licenses/OpenStreetView/attribution.txt`
    - `licenses/OpenStreetView/odbl-10.txt`
    - `licenses/OpenStreetView/dbcl-10.txt`
    - `licenses/OpenStreetView/Textdokument (neu).txt`
  - License: OpenStreetMap / Open data licenses (ODbL / DBCL variants)
  - SPDX: `ODbL-1.0` / `CC-BY-4.0` (check each file for exact terms)
  - Type: Open-data licenses — attribution required

- Misc / other files in `licenses/`
  - `licenses/dependency-licenses-summary.json` (summary of detected dependency licenses — uses relative paths)
  - `licenses/get-Licencses.ps1` (script used to collect license files)


