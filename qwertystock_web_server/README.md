# QwertyStock web server (local)

Serves the browser UI for the Windows bootstrapper at `http://localhost:7332` by default.

The cabinet UI is **`static/cabinet.html`**, served at **`/`** (bootstrapper opens this URL). **`/cabinet`** is an optional alias (same HTML) after `git pull` updates `main.py`. OAuth redirects use **`/`** so older server builds without `/cabinet` still work. (Qwertystock look: Rubik, logo/favicons from `https://qwertystock.com/`). Placeholders `{{PRODUCT_VERSION}}`, `{{PYTHON_VERSION}}`, `{{GIT_REVISION}}` are filled in `main.py`. **`GET /api/version`** returns JSON: `productVersion` (same human-readable number as the Windows installer and repo root **`VERSION`**), `python`, optional `gitRevision`.

**Daemon settings** (shared with the Windows tray host): **`GET/PUT /api/settings`** JSON with `repoPollIntervalSeconds` (30–86400). The bootstrapper sets **`QS_DAEMON_SETTINGS_PATH`** to `%LocalAppData%\QwertyStock\daemon_settings.json` so the Python process and `qwertystock.exe` use the same file.

## Run

From this directory:

```bash
python main.py
```

Or with explicit port:

```bash
set PORT=7332
python main.py
```

On Unix:

```bash
PORT=7332 python main.py
```

Environment:

- `PORT` — listen port (default **7332**)
- `HOST` — bind address (default **127.0.0.1**)
- `QS_PRODUCT_VERSION` — set by the bootstrapper from `%LocalAppData%\\QwertyStock\\repo\\VERSION` after clone (same line as **`VERSION`** at repo root); overrides file read when set
- `QS_GIT_REVISION` — optional short commit hash for display and `/api/version` (if the launcher sets it)
- `QS_DAEMON_SETTINGS_PATH` — path to `daemon_settings.json` (set by the bootstrapper on Windows)

## Dependencies

```bash
pip install -r requirements.txt
```

## Troubleshooting

**Старая «заглушка» вместо кабинета с OAuth.** Инсталлер обновляет только **`qwertystock.exe`**. Разметка кабинета — это **`%LocalAppData%\QwertyStock\repo\qwertystock_web_server\static\cabinet.html`**, она попадает туда **только из git** (clone/sync). Новый exe **не копирует** HTML сам по себе. Проверка: `GET /api/version` → поле **`cabinetUi`**: нужно **`oauth-v2`**; если **`stub-legacy`** — в каталоге репозитория старый файл, выполните вручную `git fetch` + `git reset --hard origin/main` (или удалите папку `repo` и дайте установщику заново клонировать). Затем перезапуск трея / `qwertystock.exe`. Кэш браузера: **Ctrl+F5** или приватное окно.
