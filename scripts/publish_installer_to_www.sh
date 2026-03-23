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
  "sha256": "${HASH}",
  "pythonEmbedZipUrl": "https://way.qwertystock.com/installer/mirrors/python-3.11.9-embed-amd64.zip",
  "pythonEmbedZipSha256": "009d6bf7e3b2ddca3d784fa09f90fe54336d5b60f0e0f305c37f400bf83cfd3b",
  "minGitZipUrl": "https://way.qwertystock.com/installer/mirrors/MinGit-2.44.0-64-bit.zip",
  "minGitZipSha256": "ed4e74e171c59c9c9d418743c7109aa595e0cc0d1c80cac574d69ed5e571ae59",
  "getPipUrl": "https://way.qwertystock.com/installer/mirrors/get-pip.py",
  "getPipSha256": "feba1c697df45be1b539b40d93c102c9ee9dde1d966303323b830b06f3fbca3c"
}
EOF
WWW="${INSTALLER_WWW:-/var/www/installer}"
echo "Wrote installer/version.json (version ${VER}, sha256 ${HASH})"
echo "If you use CDN mirrors, refresh files: sudo INSTALLER_WWW=$WWW $ROOT/scripts/sync_installer_mirrors_to_www.sh"
if [[ -d "$(dirname "$WWW")" ]] && [[ -w "$(dirname "$WWW")" || "$(id -u)" -eq 0 ]]; then
  mkdir -p "$WWW"
  cp -f "$EXE" "$WWW/qwertystock.exe"
  cp -f "$EXE" "$WWW/qwertystock-en.exe"
  cp -f "$EXE" "$WWW/qwertystock-ru.exe"
  cp -f "$ROOT/installer/version.json" "$WWW/version.json"
  echo "Copied to $WWW/ (qwertystock.exe + qwertystock-en.exe + qwertystock-ru.exe)"
else
  echo "Install to web root (requires sudo):"
  echo "  sudo mkdir -p $WWW && sudo cp -f \"$EXE\" $WWW/qwertystock.exe && sudo cp -f \"$EXE\" $WWW/qwertystock-en.exe && sudo cp -f \"$EXE\" $WWW/qwertystock-ru.exe && sudo cp -f \"$ROOT/installer/version.json\" $WWW/version.json"
fi
