# QwertyStock web server (local)

Serves the browser UI for the Windows bootstrapper at `http://localhost:7332` by default.

The root page (`/`) is **`static/cabinet.html`** — a styled user-cabinet stub (Qwertystock look: Rubik, logo/favicons from `https://qwertystock.com/`). The placeholder `{{BUILD_STAMP}}` is replaced at startup; set **`QS_CABINET_BUILD`** to any string to confirm updates after deploy without editing the HTML.

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
- `QS_CABINET_BUILD` — label shown on the cabinet stub (default set in `main.py`) to verify the installer picked up the new files
- `QS_DAEMON_SETTINGS_PATH` — path to `daemon_settings.json` (set by the bootstrapper on Windows)

## Dependencies

```bash
pip install -r requirements.txt
```
