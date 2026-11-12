#!/usr/bin/env bash
# يبني حزمتي أربش لـLinux x64: ‎.deb للتثبيت عبر dpkg/apt و‎.tar.gz للتشغيل دون تثبيت.
# الاستخدام: scripts/build_linux_packages.sh [الإصدار]
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -n 1)}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]]; then
    echo "Version must look like 1.2.3 or 1.2.3-label: $version" >&2
    exit 2
fi
# في Debian تفصل الشرطة المراجعة، و«~» يجعل الإصدار التجريبي أقدم من النهائي.
deb_version="${version/-/\~}-1"

dist="$root/dist"
payload="$dist/linux-payload"
rm -rf "$payload"
mkdir -p "$dist/packages"

dotnet publish "$root/src_csharp/ArbSh.Console/ArbSh.Console.csproj" \
    -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true \
    "-p:Version=$version" -p:DebugType=none -o "$payload/bin"
dotnet publish "$root/src_csharp/ArbSh.Terminal/ArbSh.Terminal.csproj" \
    -c Release -r linux-x64 --self-contained true \
    "-p:Version=$version" -p:DebugType=none -o "$payload/terminal"

if [[ "$(ls -A "$payload/bin")" != "arbsh" ]]; then
    echo "The command directory must contain only arbsh." >&2
    exit 1
fi
reported="$("$payload/bin/arbsh" --version | head -n 1)"
if [[ "$reported" != "arbsh $version" ]]; then
    echo "Published arbsh reports '$reported', expected 'arbsh $version'." >&2
    exit 1
fi

mkdir -p "$payload/docs"
cp "$root/README.md" "$root/docs/CHANGELOG.md" "$root/docs/USAGE_EXAMPLES.md" \
    "$root/docs/ARBSH_HOST_V1.md" "$payload/docs/"

# ‎.tar.gz: المجلد نفسه، يُفك في أي مكان ويُشغَّل bin/arbsh مباشرة.
tarball="$dist/packages/arbsh-$version-linux-x64.tar.gz"
tar -C "$dist" --transform "s:^linux-payload:arbsh-$version:" \
    --owner=0 --group=0 -czf "$tarball" linux-payload

# ‎.deb: الحمولة في ‎/opt/arbsh، والأمر عبر رابط في ‎/usr/bin.
stage="$dist/deb-stage"
rm -rf "$stage"
mkdir -p "$stage/DEBIAN" "$stage/opt" "$stage/usr/bin" \
    "$stage/usr/share/applications" "$stage/usr/share/icons/hicolor/512x512/apps"
cp -a "$payload" "$stage/opt/arbsh"
ln -s /opt/arbsh/bin/arbsh "$stage/usr/bin/arbsh"
cp "$root/src_csharp/ArbSh.Terminal/Assets/Brand/ArbShIcon.png" \
    "$stage/usr/share/icons/hicolor/512x512/apps/arbsh.png"
cat > "$stage/usr/share/applications/arbsh.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=ArbSh
Name[ar]=أربش
Comment=Arabic-first shell and terminal
Comment[ar]=صدفة وطرفية عربية
Exec=/opt/arbsh/terminal/ArbSh.Terminal --working-dir ~
Icon=arbsh
Terminal=false
Categories=System;TerminalEmulator;
DESKTOP
installed_size="$(du -sk "$stage" | cut -f1)"
cat > "$stage/DEBIAN/control" <<CONTROL
Package: arbsh
Version: $deb_version
Architecture: amd64
Maintainer: Omar Aglan
Installed-Size: $installed_size
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libicu74 | libicu72 | libicu70 | libicu67, libfontconfig1, libx11-6, libice6, libsm6
Section: shells
Priority: optional
Homepage: https://github.com/RuqoomTech/ArbSh
Description: Arabic-first shell and standalone terminal
 ArbSh is an Arabic-first command shell with a standalone terminal. The
 arbsh command implements the arbsh-host-v1 contract for scripts and tools.
CONTROL
deb="$dist/packages/arbsh_${deb_version}_amd64.deb"
dpkg-deb --root-owner-group -Zxz --build "$stage" "$deb" >/dev/null

for artifact in "$tarball" "$deb"; do
    (cd "$(dirname "$artifact")" && sha256sum "$(basename "$artifact")" | sed 's/  / */' > "$(basename "$artifact").sha256")
    echo "$artifact"
done
