# Binary assets setup for production

This repository keeps the QWERTYSTOCK landing page in a **text-only** form so PR creation from this session does not fail on binary assets.

The landing still expects these binary files to exist on the production server at the exact local paths below:

- `app/assets/fonts/*.woff2`
- `app/assets/img/icons/*.png`

Until they are copied into place, the page will still load, but browsers will fall back to system fonts and some favicon / PWA icons will be missing.

## Copy paths that must exist on the production server

From the repository root:

```bash
mkdir -p app/assets/fonts app/assets/img/icons
```

## 1) Rubik font files (`app/assets/fonts/`)

Run these commands from the repository root on the production server:

```bash
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nBrXw.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nBrXw.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nDrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nDrXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nErXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nErXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nFrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nFrXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nMrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nMrXyi0A.woff2'
curl -L 'https://fonts.gstatic.com/s/rubik/v31/iJWKBXyIfDnIV7nPrXyi0A.woff2' -o 'app/assets/fonts/iJWKBXyIfDnIV7nPrXyi0A.woff2'
```

These files are referenced by `app/assets/css/rubik.css`.

## 2) Icon PNG files (`app/assets/img/icons/`)

Run these commands from the repository root on the production server:

```bash
curl -L 'https://stocksubmitter.com/assets/img/icons/android-chrome-192x192.png' -o 'app/assets/img/icons/android-chrome-192x192.png'
curl -L 'https://stocksubmitter.com/assets/img/icons/android-chrome-256x256.png' -o 'app/assets/img/icons/android-chrome-256x256.png'
curl -L 'https://stocksubmitter.com/assets/img/icons/android-chrome-512x512.png' -o 'app/assets/img/icons/android-chrome-512x512.png'
curl -L 'https://stocksubmitter.com/assets/img/icons/apple-touch-icon.png' -o 'app/assets/img/icons/apple-touch-icon.png'
curl -L 'https://stocksubmitter.com/assets/img/icons/favicon-16x16.png' -o 'app/assets/img/icons/favicon-16x16.png'
curl -L 'https://stocksubmitter.com/assets/img/icons/favicon-32x32.png' -o 'app/assets/img/icons/favicon-32x32.png'
curl -L 'https://stocksubmitter.com/assets/img/icons/mstile-150x150.png' -o 'app/assets/img/icons/mstile-150x150.png'
```

These files are referenced by:

- `app.html`
- `app/en.html`
- `app/es.html`
- `app/uk.html`
- `app/pt.html`
- `app/zh-cn.html`
- `app/manifest.json`
- `app/browserconfig.xml`

## Verification

After copying the assets, verify that the required files exist:

```bash
find app/assets/fonts -maxdepth 1 -type f | sort
find app/assets/img/icons -maxdepth 1 -type f | sort
```

If the project is served locally from the repository root, you can also verify the icons and CSS endpoints:

```bash
python3 -m http.server 8000
curl -I http://127.0.0.1:8000/app/assets/css/rubik.css
curl -I http://127.0.0.1:8000/app/assets/img/icons/favicon-32x32.png
```
