# Publication audit

| Component | Source / version | License status | Reviewed scope | Integrity | Dependency status | Decision |
|---|---|---|---|---|---|---|
| Logic Arrows Launcher | This repository, current working tree, v1.0.7 dark UI polish, toolbar tooltip correction, WebView2 bind-focus recovery, stale-key reset, canvas focus-ring fix and grid-tile redraw correction, selected-arrow overlay and zoom-stable preview sampling | No separate launcher license declared | `src/`, `smoke/`, `assets/`, `tools/`, README, docs, platform folders | Reviewed before publish; no secrets found | `Microsoft.Web.WebView2` 1.0.4129.50 pinned in csproj | PASS |
| `.map` integration | `src/MapFileService.cs`, `src/MapBridgeScript.cs`, `src/LauncherForm.cs`; v1.0.7 UI-only dark bridge, toolbar tooltip styling and alt-tab bind-focus recovery | Launcher-owned format; embedded Logic Arrows data remains game-owned | JSON envelope validation, Base64 boundary, origin-checked WebView2 message bridge | Round-trip, invalid-format, adaptive TPS/theme, page focus, stale-key reset, canvas focus-ring fix, grid-tile redraw correction, selected-arrow overlay, zoom-stable preview sampling and v1.0.7 UI smoke tests PASS; JS syntax PASS | No new package added | PASS |
| Microsoft.Web.WebView2 | NuGet package `Microsoft.Web.WebView2` 1.0.4129.50 | Registry package metadata; verify upstream terms before redistribution | Used by `src/LogicArrowsLauncher.csproj` | Pinned version | Explicit package version, no floating range | PASS |
| Logic Arrows runtime resources | `https://logic-arrows.io/`, fetched at launcher runtime | Belong to Logic Arrows правообладатели | Resource allowlist in `src/ResourceCatalog.cs`; downloaded game source is not committed | Origin restricted to `https://logic-arrows.io` | No cookies or tokens copied | PASS |
| Logic Arrows favicon | `https://logic-arrows.io/res/favicon512.png` | Belongs to Logic Arrows правообладатели | `assets/logic-arrows-favicon.png`, generated `assets/logic-arrows.ico` | Original 512×512 PNG and seven-size ICO reviewed | Static asset included for app branding | PASS |
| Platform source layout | This repository: `windows/`, `linux/`, `mac/` | Launcher source status documented per platform | Platform READMEs reviewed; no fake binaries | Windows build is available; Linux/macOS are source/porting notes | No unpinned platform packages added | PASS |

## Release artifact

`LogicArrowsLauncher.exe` is a Windows x64 self-contained single-file publish for v1.0.7 dark UI polish, toolbar tooltip correction, WebView2 bind-focus recovery, stale-key reset, canvas focus-ring fix and grid-tile redraw correction, selected-arrow overlay and zoom-stable preview sampling.

SHA-256: `ae233e6d44ae8eae3e94d45fe28836d651bc002a4a8c5aa9b9b2d7f7b5a42bc1`

Size: 72,064,315 bytes.

The Release EXE is generated from the reviewed source. Generated binaries, research captures, local logs and caches are excluded from the source commit and uploaded only as release assets where intended.

## Security scope

The launcher uses an HTTPS allowlist for Logic Arrows static resources, does not copy browser cookies, keeps the official WebView2 origin, and does not commit user map data or local profiles.

## Sources

- https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.acceleratorkeypressed?view=webview2-dotnet-1.0.4129.50
- https://www.nuget.org/packages/Microsoft.Web.WebView2/
- https://logic-arrows.io/
- https://logic-arrows.io/res/favicon512.png
