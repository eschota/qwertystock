#!/usr/bin/env bash
# Downloads official Python embed, MinGit, get-pip.py and copies them to the web installer tree
# so clients load from https://way.qwertystock.com/installer/mirrors/ (see installer/version.json).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WWW="${INSTALLER_WWW:-/var/www/installer}"
MIRROR="$WWW/mirrors"

PY_URL='https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip'
GIT_URL='https://github.com/git-for-windows/git/releases/download/v2.44.0.windows.1/MinGit-2.44.0-64-bit.zip'
PIP_URL='https://bootstrap.pypa.io/get-pip.py'

expect_py='009d6bf7e3b2ddca3d784fa09f90fe54336d5b60f0e0f305c37f400bf83cfd3b'
expect_git='ed4e74e171c59c9c9d418743c7109aa595e0cc0d1c80cac574d69ed5e571ae59'
expect_pip='feba1c697df45be1b539b40d93c102c9ee9dde1d966303323b830b06f3fbca3c'

tmpdir="$(mktemp -d)"
trap 'rm -rf "$tmpdir"' EXIT

curl -fsSL "$PY_URL" -o "$tmpdir/python-3.11.9-embed-amd64.zip"
curl -fsSL "$GIT_URL" -o "$tmpdir/MinGit-2.44.0-64-bit.zip"
curl -fsSL "$PIP_URL" -o "$tmpdir/get-pip.py"

check() {
  local f="$1" want="$2"
  local got
  got="$(sha256sum "$f" | awk '{print $1}')"
  if [[ "$got" != "$want" ]]; then
    echo "SHA256 mismatch for $f (got $got, want $want)" >&2
    exit 1
  fi
}

check "$tmpdir/python-3.11.9-embed-amd64.zip" "$expect_py"
check "$tmpdir/MinGit-2.44.0-64-bit.zip" "$expect_git"
check "$tmpdir/get-pip.py" "$expect_pip"

if [[ ! -d "$(dirname "$WWW")" ]]; then
  echo "Parent of INSTALLER_WWW does not exist: $WWW" >&2
  exit 1
fi

if [[ "$(id -u)" -eq 0 ]] || [[ -w "$(dirname "$WWW")" ]]; then
  mkdir -p "$MIRROR"
  cp -f "$tmpdir/python-3.11.9-embed-amd64.zip" "$MIRROR/"
  cp -f "$tmpdir/MinGit-2.44.0-64-bit.zip" "$MIRROR/"
  cp -f "$tmpdir/get-pip.py" "$MIRROR/"
  echo "Mirrors installed under $MIRROR"
else
  echo "Need write access to $WWW (e.g. sudo INSTALLER_WWW=$WWW $0)" >&2
  exit 1
fi
