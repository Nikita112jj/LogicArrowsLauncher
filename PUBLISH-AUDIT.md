# Publication audit

| Component | Source / version | License status | Reviewed scope | Integrity | Dependency status | Decision |
|---|---|---|---|---|---|---|
| Logic Arrows Launcher | This repository, current working tree, v1.0.5 patch | No separate launcher license declared | `src/`, `smoke/`, `assets/`, `tools/`, README, docs, platform folders | Reviewed before publish; no secrets found | `Microsoft.Web.WebView2` 1.0.4129.50 pinned in csproj | PASS |
| `.map` integration | `src/MapFileService.cs`, `src/MapBridgeScript.cs` | Launcher-owned format; embedded Logic Arrows data remains game-owned | JSON envelope validation, Base64 boundary, origin-checked WebView2 message bridge | Round-trip and invalid-format smoke tests PASS; JS syntax PASS | No new package added | PASS |
| Microsoft.Web.WebView2 | NuGet package `Microsoft.Web.WebView2` 1.0.4129.50 | Registry package metadata; verify upstream terms before redistribution | Used by `src/LogicArrowsLauncher.csproj` | Pinned version | Explicit package version, no floating range | PASS |
| Logic Arrows runtime resources | `https://logic-arrows.io/`, fetched at launcher runtime | Belong to Logic Arrows правообладатели | Resource allowlist in `src/ResourceCatalog.cs`; downloaded game source is not committed | Origin restricted to `https://logic-arrows.io` | No cookies or tokens copied | PASS |
| Logic Arrows favicon | `https://logic-arrows.io/res/favicon512.png` | Belongs to Logic Arrows правообладатели | `assets/logic-arrows-favicon.png`, generated `assets/logic-arrows.ico` | Original 512×512 PNG and seven-size ICO reviewed | Static asset included for app branding | PASS |
| Platform source layout | This repository: `windows/`, `linux/`, `mac/` | Launcher source status documented per platform | Platform READMEs reviewed; no fake binaries | Windows build is available; Linux/macOS are source/porting notes | No unpinned platform packages added | PASS |

## Release artifact

`LogicArrowsLauncher.exe` is a Windows x64 self-contained single-file publish for v1.0.5 map patch.

SHA-256: `1edf8b149166c8f56f03118482ae2574287de7e923be60df5de73cb1119389e6`

Size: 72,028,602 bytes.

The Release EXE is generated from the reviewed source. Generated binaries, research captures, local logs and caches are excluded from the source commit and uploaded only as release assets where intended.

## Security scope

The launcher uses an HTTPS allowlist for Logic Arrows static resources, does not copy browser cookies, keeps the official WebView2 origin, and does not commit user map data or local profiles.

## Sources

- https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.acceleratorkeypressed?view=webview2-dotnet-1.0.4129.50
- https://www.nuget.org/packages/Microsoft.Web.WebView2/
- https://logic-arrows.io/
- https://logic-arrows.io/res/favicon512.png
