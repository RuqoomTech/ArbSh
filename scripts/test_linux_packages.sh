#!/usr/bin/env bash
# بوابة دورة حياة حزمة أربش على Linux: تثبيت إصدار سابق، ثم ترقية، ثم إصلاح بعد
# حذف ملفات، ثم إزالة لا تترك ملفات، إضافة إلى فحص الأرشيف ‎.tar.gz.
# يحتاج sudo وxvfb-run. الاستخدام:
#   scripts/test_linux_packages.sh <new.deb> <previous.deb> <new.tar.gz>
set -euo pipefail

deb="$(realpath "$1")"
previous_deb="$(realpath "$2")"
tarball="$(realpath "$3")"

fail() { echo "FAIL: $*" >&2; exit 1; }

for artifact in "$deb" "$previous_deb" "$tarball"; do
    (cd "$(dirname "$artifact")" && sed 's/ \*/  /' "$(basename "$artifact").sha256" | sha256sum --check --quiet) \
        || fail "SHA-256 verification failed for $artifact"
done

upstream_of() { dpkg-deb --field "$1" Version | sed 's/-[^-]*$//; s/~/-/'; }
version="$(upstream_of "$deb")"
previous_version="$(upstream_of "$previous_deb")"
[[ "$version" != "$previous_version" ]] || fail "the upgrade check needs two different versions"
dpkg --compare-versions "$(dpkg-deb --field "$deb" Version)" gt "$(dpkg-deb --field "$previous_deb" Version)" \
    || fail "the new package does not sort after the previous one"
if dpkg-query -W -f='${Status}' arbsh 2>/dev/null | grep -q "installed"; then
    fail "refusing to run over an existing arbsh package"
fi

greeting="مرحبا"
assert_installed() {
    local expected="$1" stage="$2"
    [[ -x /opt/arbsh/bin/arbsh && -x /opt/arbsh/terminal/ArbSh.Terminal ]] || fail "$stage: payload missing"
    [[ "$(readlink /usr/bin/arbsh)" == /opt/arbsh/bin/arbsh ]] || fail "$stage: /usr/bin/arbsh link missing"
    [[ -f /usr/share/applications/arbsh.desktop ]] || fail "$stage: desktop entry missing"
    local reported
    reported="$(arbsh --version | tr '\n' '/')"
    [[ "$reported" == "arbsh $expected/arbsh-host-v1/" ]] || fail "$stage: arbsh --version printed '$reported'"
    dpkg --verify arbsh || fail "$stage: dpkg --verify reports changed files"
}

cleanup() {
    if dpkg-query -W -f='${Status}' arbsh 2>/dev/null | grep -q "installed"; then
        sudo dpkg --purge arbsh >/dev/null || true
    fi
}
trap cleanup EXIT

sudo apt-get install -y --no-install-recommends "$previous_deb" >/dev/null
assert_installed "$previous_version" "install"

sudo apt-get install -y --no-install-recommends "$deb" >/dev/null
assert_installed "$version" "upgrade"

sudo rm -f /opt/arbsh/bin/arbsh /opt/arbsh/terminal/ArbSh.Terminal
sudo dpkg -i "$deb" >/dev/null
assert_installed "$version" "repair"

printed="$(env -i PATH=/usr/bin:/bin HOME="$HOME" arbsh --command "اطبع $greeting")"
[[ "$printed" == "$greeting" ]] || fail "installed arbsh --command printed '$printed'"
set +e
env -i PATH=/usr/bin:/bin HOME="$HOME" arbsh --command "arbsh-missing-program-$RANDOM" 2>/dev/null
code=$?
set -e
[[ $code -eq 127 ]] || fail "installed arbsh returned $code for a missing program, expected 127"

# الطرفية الرسومية تبدأ وتبقى حية خمس ثوانٍ تحت خادم عرض وهمي.
set +e
timeout 5 xvfb-run -a /opt/arbsh/terminal/ArbSh.Terminal --working-dir ~ >/dev/null 2>&1
code=$?
set -e
[[ $code -eq 124 ]] || fail "installed ArbSh.Terminal exited early with code $code"

sudo dpkg --purge arbsh >/dev/null
for leftover in /opt/arbsh /usr/bin/arbsh /usr/share/applications/arbsh.desktop \
    /usr/share/icons/hicolor/512x512/apps/arbsh.png; do
    [[ ! -e "$leftover" && ! -L "$leftover" ]] || fail "removal left $leftover"
done

work="$(mktemp -d)"
tar -C "$work" -xzf "$tarball"
reported="$("$work/arbsh-$version/bin/arbsh" --version | head -n 1)"
[[ "$reported" == "arbsh $version" ]] || fail "tarball arbsh --version printed '$reported'"
[[ -x "$work/arbsh-$version/terminal/ArbSh.Terminal" ]] || fail "tarball terminal missing"
rm -rf "$work"

echo "ArbSh Linux package lifecycle passed: install $previous_version, upgrade to $version, repair, purge, tarball."
