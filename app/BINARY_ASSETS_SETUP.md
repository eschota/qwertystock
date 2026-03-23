# Binary assets setup for production

This repository keeps the QWERTYSTOCK landing page in a **text-only** form so PR creation from this session does not fail on binary assets.

The landing still expects these binary files to exist on the production server at the exact local paths below:

- `app/assets/fonts/*.woff2`
- `app/assets/img/icons/*.png` and `safari-pinned-tab.svg`
- `app/assets/img/header/qwerty_logo.svg`, `qwerty_logo_mobile.svg`

Until they are copied into place, the page will still load, but browsers will fall back to system fonts and some favicon / PWA icons will be missing.

## One-shot fetch (recommended)

From the repository root:

```bash
./scripts/fetch_binary_assets.sh
```

This downloads fonts, favicons, and header logos from [https://qwertystock.com/](https://qwertystock.com/) (same assets as the main gallery).

## Manual: Rubik font files (`app/assets/fonts/`)

```bash
mkdir -p app/assets/fonts app/assets/img/icons app/assets/img/header
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nBrXw.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nBrXw.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nDrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nDrXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nErXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nErXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nFrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nFrXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nMrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nMrXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nPrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nPrXyi0A.woff2'
```

These files are referenced by `app/assets/css/rubik.css`.

## Manual: icons and logos

```bash
BASE='https://qwertystock.com'
curl -L "${BASE}/img/icons/android-chrome-192x192.png" -o 'app/assets/img/icons/android-chrome-192x192.png'
curl -L "${BASE}/img/icons/android-chrome-512x512.png" -o 'app/assets/img/icons/android-chrome-512x512.png'
curl -L "${BASE}/img/icons/apple-touch-icon.png" -o 'app/assets/img/icons/apple-touch-icon.png'
curl -L "${BASE}/img/icons/favicon-16x16.png" -o 'app/assets/img/icons/favicon-16x16.png'
curl -L "${BASE}/img/icons/favicon-32x32.png" -o 'app/assets/img/icons/favicon-32x32.png'
curl -L "${BASE}/img/icons/mstile-150x150.png" -o 'app/assets/img/icons/mstile-150x150.png'
curl -L "${BASE}/img/icons/safari-pinned-tab.svg" -o 'app/assets/img/icons/safari-pinned-tab.svg'
curl -L "${BASE}/img/qwerty_logo.svg" -o 'app/assets/img/header/qwerty_logo.svg'
curl -L "${BASE}/img/qwerty_logo_mobile.svg" -o 'app/assets/img/header/qwerty_logo_mobile.svg'
```

Referenced by `app.html`, `app/ru.html`, `app/manifest.json`, `app/browserconfig.xml`.

## Verification

```bash
find app/assets/fonts -maxdepth 1 -type f | sort
find app/assets/img/icons -maxdepth 1 -type f | sort
ls -la app/assets/img/header/qwerty_logo*.svg
```

## Production nginx (`way.qwertystock.com`)

Статика из репозитория: корень `app.html`, префикс `/app/` → каталог `app/` в клоне.

```nginx
location = /app.html {
    alias /home/debian/qwertystock/app.html;
    default_type text/html;
    charset utf-8;
}
location ^~ /app/ {
    alias /home/debian/qwertystock/app/;
    try_files $uri =404;
}
```

Публично: `https://way.qwertystock.com/app.html`
