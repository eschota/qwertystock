#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/installer"
dotnet publish QwertyStock.Bootstrapper/QwertyStock.Bootstrapper.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
EXE="$ROOT/installer/QwertyStock.Bootstrapper/bin/Release/net9.0-windows/win-x64/publish/qwertystock.exe"
HASH="$(sha256sum "$EXE" | awk '{print $1}')"
VER="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$ROOT/installer/QwertyStock.Bootstrapper/QwertyStock.Bootstrapper.csproj" | head -1)"
cat > "$ROOT/installer/version.json" <<EOF
{
  "version": "${VER}",
  "url": "https://way.qwertystock.com/installer/qwertystock.exe",
  "sha256": "${HASH}"
}
EOF
echo "Wrote installer/version.json (version ${VER}, sha256 ${HASH})"
WWW="${INSTALLER_WWW:-/var/www/installer}"
if [[ -d "$(dirname "$WWW")" ]] && [[ -w "$(dirname "$WWW")" || "$(id -u)" -eq 0 ]]; then
  mkdir -p "$WWW"
  cp -f "$EXE" "$WWW/qwertystock.exe"
  cp -f "$ROOT/installer/version.json" "$WWW/version.json"
  echo "Copied to $WWW/"
else
  echo "Install to web root (requires sudo):"
  echo "  sudo mkdir -p $WWW && sudo cp -f \"$EXE\" $WWW/qwertystock.exe && sudo cp -f \"$ROOT/installer/version.json\" $WWW/version.json"
fi
