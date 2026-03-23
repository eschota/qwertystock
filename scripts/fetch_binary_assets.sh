#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p app/assets/fonts app/assets/img/icons app/assets/img/header

BASE='https://qwertystock.com'

echo "==> Rubik woff2"
curl -fsSL 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nBrXw.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nBrXw.woff2'
curl -fsSL 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nDrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nDrXyi0A.woff2'
curl -fsSL 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nErXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nErXyi0A.woff2'
curl -fsSL 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nFrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nFrXyi0A.woff2'
curl -fsSL 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nMrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nMrXyi0A.woff2'
curl -fsSL 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nPrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nPrXyi0A.woff2'

echo "==> Icons & header logos (qwertystock.com)"
curl -fsSL "${BASE}/img/icons/android-chrome-192x192.png" -o 'app/assets/img/icons/android-chrome-192x192.png'
curl -fsSL "${BASE}/img/icons/android-chrome-512x512.png" -o 'app/assets/img/icons/android-chrome-512x512.png'
curl -fsSL "${BASE}/img/icons/apple-touch-icon.png" -o 'app/assets/img/icons/apple-touch-icon.png'
curl -fsSL "${BASE}/img/icons/favicon-16x16.png" -o 'app/assets/img/icons/favicon-16x16.png'
curl -fsSL "${BASE}/img/icons/favicon-32x32.png" -o 'app/assets/img/icons/favicon-32x32.png'
curl -fsSL "${BASE}/img/icons/mstile-150x150.png" -o 'app/assets/img/icons/mstile-150x150.png'
curl -fsSL "${BASE}/img/icons/safari-pinned-tab.svg" -o 'app/assets/img/icons/safari-pinned-tab.svg'
curl -fsSL "${BASE}/img/qwerty_logo.svg" -o 'app/assets/img/header/qwerty_logo.svg'
curl -fsSL "${BASE}/img/qwerty_logo_mobile.svg" -o 'app/assets/img/header/qwerty_logo_mobile.svg'

echo "OK. Verify:"
find app/assets/fonts -maxdepth 1 -type f | sort
find app/assets/img/icons -maxdepth 1 -type f | sort
ls -la app/assets/img/header/qwerty_logo*.svg
