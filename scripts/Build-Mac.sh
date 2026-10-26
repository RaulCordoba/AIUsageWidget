#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then
  echo 'Build-Mac.sh requires macOS and the Xcode command line tools (Swift/WebKit).' >&2
  exit 1
fi
rid="${1:-osx-arm64}"
case "$rid" in
  osx-arm64) arch=arm64 ;;
  osx-x64) arch=x86_64 ;;
  *) echo 'Use osx-arm64 or osx-x64.' >&2; exit 1 ;;
esac
app="$PWD/publish/$rid/AI Usage Widget.app"
binary="$app/Contents/MacOS"
resources="$app/Contents/Resources"
frameworks="$app/Contents/Frameworks"
helper="$app/Contents/Helpers/AIUsageWidget.MacHelper.app"
# Recreate only this runtime's generated bundle, including any old signatures
# and symlinks, so repeated builds have the same layout as a fresh checkout.
rm -rf "$app"
mkdir -p "$binary" "$resources" "$frameworks" "$helper/Contents/MacOS" artifacts/macos
dotnet publish src/AIUsageWidget.Desktop/AIUsageWidget.Desktop.csproj \
  -c Release -r "$rid" --self-contained true -p:PublishSingleFile=false -o "$binary"
# Apple treats files in MacOS as nested native code. Managed DLLs, JSON,
# symbols and localization directories must instead be sealed as resources.
# Relative symlinks preserve the layout expected by the .NET apphost/runtime.
while IFS= read -r -d '' entry; do
  name="$(basename "$entry")"
  if [[ -f "$entry" ]] && [[ "$(file -b "$entry")" == *Mach-O* ]]; then
    case "$name" in
      *.dylib)
        mv "$entry" "$frameworks/$name"
        ln -s "../Frameworks/$name" "$entry"
        ;;
    esac
  else
    mv "$entry" "$resources/$name"
    ln -s "../Resources/$name" "$entry"
  fi
done < <(find "$binary" -mindepth 1 -maxdepth 1 -print0)
cp installer/macos/Info.plist "$app/Contents/Info.plist"
cp installer/macos/Helper-Info.plist "$helper/Contents/Info.plist"
xcrun swiftc -swift-version 5 -O -target "$arch-apple-macos12.0" \
  src/AIUsageWidget.Desktop/Native/MacHelper.swift \
  -o "$helper/Contents/MacOS/AIUsageWidget.MacHelper"
chmod +x "$binary/AIUsageWidget.Desktop" "$helper/Contents/MacOS/AIUsageWidget.MacHelper"
# Build the Finder/Dock icon from the same source as the menu-bar icon.
iconset="$PWD/publish/$rid/AppIcon.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" src/AIUsageWidget.Desktop/Assets/app.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" src/AIUsageWidget.Desktop/Assets/app.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/AppIcon.icns"
plutil -lint "$app/Contents/Info.plist" "$helper/Contents/Info.plist"
# Ad-hoc signing supports local builds; Developer ID enables external distribution.
identity="${MACOS_SIGN_IDENTITY:--}"
find "$binary" "$frameworks" -type f -print0 |
  while IFS= read -r -d '' library; do
    # Signing the main executable also signs its containing bundle. Defer it
    # until all libraries and the helper have been signed, with entitlements.
    [[ "$library" == "$binary/AIUsageWidget.Desktop" ]] && continue
    # Include runtime executables such as createdump, not just shared libraries.
    case "$(file -b "$library")" in *Mach-O*) ;; *) continue ;; esac
    if [[ "$identity" == - ]]; then
      codesign --force --sign - "$library"
    else
      codesign --force --options runtime --timestamp --sign "$identity" "$library"
    fi
  done
if [[ "$identity" == - ]]; then
  codesign --force --sign - "$helper"
  codesign --force --sign - --entitlements installer/macos/Entitlements.plist "$app"
else
  codesign --force --options runtime --timestamp --sign "$identity" "$helper"
  codesign --force --options runtime --timestamp --sign "$identity" \
    --entitlements installer/macos/Entitlements.plist "$app"
fi
codesign --verify --deep --strict "$app"
if [[ -n "${MACOS_NOTARY_PROFILE:-}" ]]; then
  if [[ "$identity" == - ]]; then echo 'Notarization requires MACOS_SIGN_IDENTITY.' >&2; exit 1; fi
  ditto -c -k --keepParent "$app" "$PWD/artifacts/macos/notarize-$rid.zip"
  xcrun notarytool submit "$PWD/artifacts/macos/notarize-$rid.zip" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
  xcrun stapler staple "$app"
fi
ditto -c -k --keepParent "$app" "$PWD/artifacts/macos/AIUsageWidget-$rid.zip"
echo "Created artifacts/macos/AIUsageWidget-$rid.zip"

# Installer image: drag the application onto the Applications shortcut.
dmg_stage="$(mktemp -d "$PWD/publish/$rid/dmg.XXXXXX")"
ditto "$app" "$dmg_stage/AI Usage Widget.app"
ln -s /Applications "$dmg_stage/Applications"
dmg="$PWD/artifacts/macos/AIUsageWidget-$rid.dmg"
hdiutil create -volname "AI Usage Widget" -srcfolder "$dmg_stage" \
  -fs HFS+ -format UDZO -ov "$dmg"
if [[ "$identity" != - ]]; then
  codesign --force --timestamp --sign "$identity" "$dmg"
fi
if [[ -n "${MACOS_NOTARY_PROFILE:-}" ]]; then
  xcrun notarytool submit "$dmg" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
  xcrun stapler staple "$dmg"
fi
hdiutil verify "$dmg"
echo "Created artifacts/macos/AIUsageWidget-$rid.dmg"
