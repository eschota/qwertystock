# QwertyStock web server (local)

Serves the browser UI for the Windows bootstrapper at `http://localhost:7332` by default.

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

## Dependencies

```bash
pip install -r requirements.txt
```
