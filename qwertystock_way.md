# Qwertystock way

Единая точка входа: `qwertystock_way.py` — HTTP-сервер (health, вебхук GitHub), настройки в `qwertystock_way.json`.

## Конфигурация (`qwertystock_way.json`)

| Раздел | Поля |
|--------|------|
| **server** | `bind` — адрес (например `0.0.0.0`), `port` — порт (по умолчанию `8765`). |
| **git** | `branch` — ветка для pull и вебхука; `systemd_unit` — unit для перезапуска после деплоя; `repo_path` — абсолютный путь к клону репозитория на сервере. |
| **github_webhook** | `secret` — общий секрет с настройками вебхука на GitHub (подпись `X-Hub-Signature-256`). |
| **telegram** | `bot_token` — токен бота; `notify_chat_id` — чат для уведомлений (личный id или id группы/форума); `notify_on_server_start` — слать ли сообщение при каждом старте процесса; `startup_message_template` — опционально, HTML, плейсхолдеры `{revision}`, `{repo_root}`. |

При старте сервиса отправляется одно сообщение в Telegram: фиксируется запуск после деплоя (в том числе после `git pull` + `systemctl restart`).

## HTTP

- `GET /health`, `GET /api/git/health` — `200` и тело `ok`.
- `POST /api/git/webhook` — события GitHub (`push` в настроенную ветку → `git pull --ff-only` + перезапуск unit).

## Документация авторизации локального кабинета (way)

Статика из репозитория: каталог `docs/auth/` — **манифест OAuth2/OIDC** и **полное описание протокола** для разработчика материнского сайта.

Публичные URL (на сервере way в `sites-available/main` добавлен `location ^~ /docs/` → `alias /home/debian/qwertystock/docs/`):

- **`https://way.qwertystock.com/docs/auth/`** — оглавление ([index.html](docs/auth/index.html))
- **`https://way.qwertystock.com/docs/auth/auth-manifest.json`** — машиночитаемый манифест
- **`https://way.qwertystock.com/docs/auth/AUTH_PROTOCOL.md`** — спецификация (Markdown)

Пример nginx:

```nginx
location ^~ /docs/ {
    alias /home/debian/qwertystock/docs/;
    try_files $uri =404;
    charset utf-8;
}
```

Для `index.html` и `.md` при необходимости задать `default_type` (`text/html` / `text/markdown`).

## Лендинг StockSubmitter (зеркало)

После `./scripts/fetch_binary_assets.sh` и блоков nginx для `/app.html` и `/app/` (см. `app/BINARY_ASSETS_SETUP.md`):

- **`https://way.qwertystock.com/app.html`** — EN
- **`https://way.qwertystock.com/app/ru.html`** — RU

## Публичный URL вебхука

С учётом nginx на `way.qwertystock.com`:

**`https://way.qwertystock.com/api/git/webhook`**

В GitHub: Settings → Webhooks → Payload URL как выше, **Secret** — тот же, что в `github_webhook.secret`.

**Content type:** предпочтительно **`application/json`**. Если выбран **`application/x-www-form-urlencoded`**, сервер разбирает поле `payload` (поддержано с версии обработчика form-urlencoded). При несовпадении подписи ответ `401`, при неразобранном теле — `400` (см. Recent Deliveries в настройках вебхука).

## Сервис systemd

- Имя: `qwertystock-way.service`
- Рабочая директория: корень репозитория на сервере
- Перезапуск без пароля для пользователя `debian`: `sudoers` разрешает `systemctl restart qwertystock-way.service`

```bash
sudo systemctl status qwertystock-way
sudo journalctl -u qwertystock-way -f
```

## nginx

Префикс `location ^~ /api/git/` проксируется на `127.0.0.1:<port>` из конфига, блок стоит **выше** общего `location /api/` приложения RF2.

## Структура репозитория

```
qwertystock_way.py          # точка входа
qwertystock_way.json        # настройки
qwertystock_way.md          # эта документация
module/
  config.py                 # загрузка JSON
  api_git_webhook.py        # вебхук GitHub
  telegram_notify.py        # уведомления в Telegram
```

## Push в GitHub (Deploy key на сервере)

На сервере пользователя `debian` создана пара ключей:

- приватный: `~/.ssh/deploy_qwertystock_ed25519` (не копировать, не коммитить);
- публичный: `~/.ssh/deploy_qwertystock_ed25519.pub` — его содержимое нужно добавить в репозиторий: **Settings → Deploy keys → Add deploy key**, включить **Allow write access**, если с сервера нужны `git push`.

В `~/.ssh/config` задан хост-алиас `github.com-qwertystock`, `origin` в клоне указывает на `git@github.com-qwertystock:eschota/qwertystock.git`.

Проверка после добавления ключа:

```bash
sudo -u debian ssh -T git@github.com-qwertystock
# ожидается приветствие вида: Hi ...! You've successfully authenticated...
```

Альтернатива — **PAT** в HTTPS или личный SSH-ключ в аккаунте GitHub.
