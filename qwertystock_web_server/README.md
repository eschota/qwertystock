# QwertyStock web server (local)

Serves the browser UI for the Windows bootstrapper at `http://localhost:3000` by default.

## Run

From this directory:

```bash
python main.py
```

Or with explicit port:

```bash
set PORT=3000
python main.py
```

On Unix:

```bash
PORT=3000 python main.py
```

Environment:

- `PORT` — listen port (default **3000**)
- `HOST` — bind address (default **127.0.0.1**)

## Dependencies

```bash
pip install -r requirements.txt
```
