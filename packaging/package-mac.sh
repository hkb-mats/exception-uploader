#!/bin/bash
# Wraps a published macOS build in Exception Uploader.app, on a disk image with a link to Applications.
#   package-mac.sh <publish folder> <version> <output dmg>
# Signs with $MAC_SIGN_IDENTITY when set ("Developer ID Application: ..."), else ad hoc, which Apple
# Silicon requires to run at all. Notarizing is a separate step, after this one.
set -euo pipefail

publish=$1
version=$2
output=$3

here=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
app="$work/Exception Uploader.app"

mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp "$publish/ExceptionUploader" "$app/Contents/MacOS/"
sed "s/__VERSION__/$version/g" "$here/Info.plist" > "$app/Contents/Info.plist"

iconset="$work/icon.iconset"
mkdir "$iconset"
for size in 16 32 128 256; do
  sips -z "$size" "$size" "$here/../src/Exception.Uploader/Assets/icon.png" --out "$iconset/icon_${size}x${size}.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/icon.icns"

identity=${MAC_SIGN_IDENTITY:--}
timestamp=--timestamp=none
[ "$identity" != "-" ] && timestamp=--timestamp
codesign --force --deep --options runtime "$timestamp" \
  --entitlements "$here/entitlements.plist" --sign "$identity" "$app"
codesign --verify --deep --strict "$app"

stage="$work/stage"
mkdir "$stage"
ditto "$app" "$stage/Exception Uploader.app"
ln -s /Applications "$stage/Applications"

mkdir -p "$(dirname "$output")"
hdiutil create -quiet -volname "Exception Uploader" -srcfolder "$stage" -ov -format UDZO "$output"
[ "$identity" = "-" ] || codesign --force --timestamp --sign "$identity" "$output"
rm -rf "$work"
echo "Packaged $output"
