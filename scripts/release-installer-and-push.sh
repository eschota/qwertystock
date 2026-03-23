#!/usr/bin/env bash
# Bump patch → dotnet publish → /var/www/installer → git commit + push всего репозитория
# (инсталлер + Python/requirements и любые другие изменения; новая версия на way и в origin).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

read_ver() {
  tr -d '\r\n ' <"$ROOT/VERSION"
}

bump_patch() {
  local v="$1" major minor patch
  IFS='.' read -r major minor patch <<<"$v"
  major=${major:-0}
  minor=${minor:-0}
  patch=${patch:-0}
  patch=$((patch + 1))
  echo "${major}.${minor}.${patch}"
}

OLD_VER="$(read_ver)"
NEW_VER="$(bump_patch "$OLD_VER")"

echo "Версия: ${OLD_VER} → ${NEW_VER}"
echo "$NEW_VER" >"$ROOT/VERSION"

CSPROJ="$ROOT/installer/QwertyStock.Bootstrapper/QwertyStock.Bootstrapper.csproj"
ASM_VER="${NEW_VER}.0"
sed -i "s/<Version>[^<]*<\/Version>/<Version>${NEW_VER}<\/Version>/" "$CSPROJ"
sed -i "s/<AssemblyVersion>[^<]*<\/AssemblyVersion>/<AssemblyVersion>${ASM_VER}<\/AssemblyVersion>/" "$CSPROJ"
sed -i "s/<FileVersion>[^<]*<\/FileVersion>/<FileVersion>${ASM_VER}<\/FileVersion>/" "$CSPROJ"

"$ROOT/scripts/publish_installer_to_www.sh"

if [[ "${RELEASE_SKIP_GIT:-}" == "1" ]]; then
  echo "RELEASE_SKIP_GIT=1 — коммит и push пропущены."
  exit 0
fi

# Весь проект в один коммит: не только VERSION/csproj/version.json, но и qwertystock_web_server и т.д.
git add -A
git commit -m "release: ${NEW_VER}"

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
git push origin "$BRANCH"

echo "Готово: ${NEW_VER} на way; в origin (${BRANCH}) запушен весь закоммиченный рабочий каталог."
