# macOS application

The macOS application is `src/AIUsageWidget.Desktop`. It keeps the current compact 260×52 and expanded 620×330 layouts, colors, rounded corners, usage/reset lines, settings, history, opacity and scaling. Refresh, provider mapping, alerts, persistence and the settings ViewModel reuse the Windows implementation. Existing Windows entry points and installers remain available.

## Build and run

Use a Mac with the .NET 10 SDK and Xcode Command Line Tools (`xcode-select --install`). No .NET macOS or MAUI workload is required. The packaging target is macOS 15+. Test on the actual operating systems to be supported before declaring full compatibility.

From the repository root:

```bash
dotnet test AIUsageWidget.Mac.slnf -c Release
bash scripts/Build-Mac.sh osx-arm64
open 'publish/osx-arm64/AI Usage Widget.app'
```

For Intel, substitute `osx-x64`. The resulting self-contained app does not require .NET on the destination Mac. Install the application in a permanent location, normally `/Applications`, before enabling login startup. Build into a fresh checkout for release packaging.

Each build creates `publish/<runtime>/AI Usage Widget.app`, `artifacts/macos/AIUsageWidget-<runtime>.zip` and `artifacts/macos/AIUsageWidget-<runtime>.dmg`. The DMG contains the app and an Applications shortcut for drag-and-drop installation. It is created and verified with macOS `hdiutil`; when configured, it is also signed and notarized. Temporary DMG staging directories remain under `publish/<runtime>/dmg.*`.

You do not need a local Mac to obtain these packages: push the changes to GitHub and open **Actions → macOS build and tests → Run workflow**. GitHub's macOS runners build Apple Silicon and Intel packages. After a successful run, download the corresponding artifact and extract the DMG. This requires the workflow and all application changes to be present in the remote repository; an unpushed local checkout cannot be built by GitHub Actions.

`dotnet run` alone does not produce the Swift helper or application bundle: use the packaging script when testing Claude's WebKit fallback and notifications.

The packaging script recreates the generated app bundle for the selected runtime before publishing. Native executables live in `Contents/MacOS`, native libraries in `Contents/Frameworks`, and managed DLLs, JSON and other data in `Contents/Resources`. Relative symlinks from `MacOS` preserve .NET's file lookup paths. Native libraries also have relative symlinks in `Resources`: .NET resolves the entry DLL's symlink to that directory and searches there for its self-contained runtime. The published apphost searches only the bundled runtime (`AppHostDotNetSearch=AppLocal`), and the script checks the hosting libraries from both lookup directories before signing. Libraries and the helper are signed first; the outer application and its main executable are signed last with the application entitlements. This prevents `codesign` from treating managed DLLs as unsigned nested native code.

## Platform integrations

| Feature | macOS implementation |
| --- | --- |
| Secure credentials | Login Keychain through Security.framework; no secret files or command-line arguments |
| Login startup | A user LaunchAgent, `~/Library/LaunchAgents/com.aiusagewidget.desktop.plist`, opening the installed app through LaunchServices |
| Data and logs | `~/Library/Application Support/AIUsageWidget/` |
| Tray actions | Native menu-bar icon: show/hide, compact/expanded, Dock placement, refresh, settings, quit |
| Taskbar visibility | macOS application activation policy controls Dock visibility |
| Always on top | Avalonia native topmost window; fullscreen/Spaces behavior is governed by macOS |
| Window placement | Screen working areas and native pixel coordinates, accounting for Retina scaling |
| Claude browser fallback | A bundled Swift helper using an ephemeral WKWebView with the session cookie |
| Notifications | UserNotifications framework, subject to the user's authorization and Focus settings |

The helper receives a JSON request through an anonymous stdin pipe and returns JSON through stdout. Provider credentials never appear in its arguments or diagnostics. The browser uses a private ephemeral cookie store, restricted Claude navigation, a hard timeout and cancellation that terminates the helper. Keychain errors and denied permissions are reported in settings without displaying underlying exception details.

The `startWithWindows` setting name is preserved in persisted JSON to retain compatibility with existing settings. Its macOS label is “Iniciar al iniciar sesion”. Windows and macOS use separate per-user data directories; copying JSON does not copy credentials.

## Signing and distribution

Local builds are ad-hoc signed. Public distribution should use a Developer ID certificate and Apple's notarization. The script accepts:

```bash
export MACOS_SIGN_IDENTITY='Developer ID Application: Your Name (TEAMID)'
export MACOS_NOTARY_PROFILE='your-existing-notarytool-keychain-profile'
bash scripts/Build-Mac.sh osx-arm64
```

The certificate and notary profile must already be installed/configured on the build Mac. Do not put certificates, passwords or API keys in the repository. The script signs native libraries and the helper before the outer bundle, verifies the bundle, and optionally submits and staples the notarization ticket. No App Store distribution is configured.

Ad-hoc signatures support local development, but do not establish a trusted Developer ID identity for Gatekeeper or guarantee stable Keychain permissions across rebuilds. Gatekeeper acceptance and notarization must be tested with the intended release identity.

## Validation

`AIUsageWidget.Mac.slnf` excludes WPF so shared logic and the macOS interface can be built/tested on a Mac. `AIUsageWidget.sln` retains both applications for Windows development.

Headless desktop tests render the production views, exercise compact/expanded mode and scaling, consume Demo usage, and check credential replacement/deletion and permission failures. Screenshots are written to `artifacts/ui/`. These tests verify layout and bindings but do not replace testing AppKit, WebKit, Keychain or notifications on macOS.

For an explicit Keychain integration test using a randomly named temporary entry:

```bash
AIUSAGEWIDGET_MAC_INTEGRATION=1 dotnet test AIUsageWidget.Mac.slnf -c Release
```

The entry is removed in a `finally` block. This test is skipped on Windows and unless explicitly enabled on macOS.

`.github/workflows/macos.yml` builds and tests on Apple Silicon and Intel runners, compiles the Swift helper, packages/signs each app, and launches the real app in an isolated Demo smoke test. It uploads a ZIP per architecture. This workflow must run after these changes are pushed; adding it does not mean it has already passed.

Before release, verify on both architectures:

1. App launch/quit, menu-bar restoration after hiding, compact/expanded mode and all controls.
2. Retina/external monitors, dragging, restored position, Dock placement and display disconnection.
3. Opacity, scaling, topmost, Dock visibility, Spaces and fullscreen behavior.
4. Keychain save/update/delete, denied/locked access, and persistence across restarts.
5. Login startup after moving the app into `/Applications`, plus disabling it.
6. Notification authorization, delivery, rejection and Focus mode.
7. Real Claude login/session, direct HTTP and WebKit fallback, expired sessions and network cancellation.
8. Gatekeeper and notarized distribution with the release certificate.

Claude's integration remains unofficial. WebKit cannot guarantee bypassing Cloudflare, MFA or future provider changes. No live account or provider credentials are used in automated tests.
