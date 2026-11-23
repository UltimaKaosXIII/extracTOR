#!/usr/bin/env bash
#
# Builds extracTOR for Linux as a single AppImage in extracTOR.Linux/dist/.
#
# The AppImage is self-contained (bundles the .NET runtime, Avalonia and Skia), so the target machine
# needs no .NET install. It uses the static type2 AppImage runtime, so it also does not need libfuse2.
# It runs on Arch-based and Debian/Ubuntu-based distros (x86_64, glibc 2.27+; e.g. Debian 10+, Ubuntu 18.04+).
#
# Usage:  ./build-appimage.sh            (from anywhere)
# Needs:  .NET SDK 8+, curl, sha256sum. Internet access on first run (NuGet packages + appimagetool).
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/extracTOR.Linux.csproj"
DIST_DIR="$SCRIPT_DIR/dist"
WORK_DIR="$SCRIPT_DIR/obj/appimage"
APPDIR="$WORK_DIR/extracTOR.AppDir"
TOOLS_DIR="$WORK_DIR/tools"

RID="linux-x64"
ARCH="x86_64"

# Pinned appimagetool release; checksum taken from the GitHub release's published digest.
APPIMAGETOOL_VERSION="1.9.1"
APPIMAGETOOL_URL="https://github.com/AppImage/appimagetool/releases/download/${APPIMAGETOOL_VERSION}/appimagetool-${ARCH}.AppImage"
APPIMAGETOOL_SHA256="ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0"

# Version shown in the AppImage file name, read from the version label in the shared WPF window (e.g. "[v1.2.1]").
VERSION="$(grep -oP '\[v\K[0-9][0-9.]*(?=\])' "$SCRIPT_DIR/../extracTOR/MainWindow.xaml" | head -n1 || true)"
VERSION="${VERSION:-dev}"

echo "==> Building extracTOR ${VERSION} AppImage (${ARCH})"

# --- 1. Fetch appimagetool (cached between builds) --------------------------------------------------
mkdir -p "$TOOLS_DIR"
APPIMAGETOOL="$TOOLS_DIR/appimagetool-${APPIMAGETOOL_VERSION}-${ARCH}.AppImage"
if [[ ! -x "$APPIMAGETOOL" ]]; then
    echo "==> Downloading appimagetool ${APPIMAGETOOL_VERSION}"
    curl -fsSL -o "$APPIMAGETOOL.tmp" "$APPIMAGETOOL_URL"
    echo "${APPIMAGETOOL_SHA256}  $APPIMAGETOOL.tmp" | sha256sum -c --quiet -
    chmod +x "$APPIMAGETOOL.tmp"
    mv "$APPIMAGETOOL.tmp" "$APPIMAGETOOL"
fi

# --- 2. Publish a self-contained build into the AppDir ---------------------------------------------
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/512x512/apps"

echo "==> dotnet publish (${RID}, self-contained)"
# - Self-contained: no .NET runtime needed on the target distro.
# - Not single-file: inside an AppImage the files are already bundled, and single-file would
#   extract native libraries to $HOME on every start.
# - InvariantGlobalization: don't depend on the distro's libicu (package names/versions differ
#   between Arch and Debian, and minimal installs may lack it).
dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:InvariantGlobalization=true \
    -p:DebugType=none \
    -o "$APPDIR/usr/bin"

# --- 3. AppDir metadata: AppRun, .desktop, icon ----------------------------------------------------
cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/extracTOR" "$@"
EOF
chmod +x "$APPDIR/AppRun"

cat > "$APPDIR/usr/share/applications/extractor.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=extracTOR
Comment=Extract files from SWTOR .tor asset archives
Exec=extracTOR
Icon=extractor
Terminal=false
Categories=Utility;
X-AppImage-Version=${VERSION}
EOF
cp "$APPDIR/usr/share/applications/extractor.desktop" "$APPDIR/extractor.desktop"

ICON_SRC="$SCRIPT_DIR/../extracTOR/Resources/Img/Logo_whiteTrans500.png"
cp "$ICON_SRC" "$APPDIR/usr/share/icons/hicolor/512x512/apps/extractor.png"
cp "$ICON_SRC" "$APPDIR/extractor.png"
ln -sf extractor.png "$APPDIR/.DirIcon"

# --- 4. Pack the AppImage --------------------------------------------------------------------------
mkdir -p "$DIST_DIR"
OUTPUT="$DIST_DIR/extracTOR-${VERSION}-${ARCH}.AppImage"
rm -f "$OUTPUT"

echo "==> Packing $OUTPUT"
# --appimage-extract-and-run lets appimagetool itself run on machines without FUSE (containers, CI).
ARCH="$ARCH" "$APPIMAGETOOL" --appimage-extract-and-run --no-appstream "$APPDIR" "$OUTPUT"

echo
echo "Done: $OUTPUT"
