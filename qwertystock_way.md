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

## Публичный URL вебхука

С учётом nginx на `way.qwertystock.com`:

**`https://way.qwertystock.com/api/git/webhook`**

В GitHub: Settings → Webhooks → Payload URL как выше, Content type `application/json`, Secret — тот же, что в `github_webhook.secret`.

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
module/
  config.py                 # загрузка JSON
  api_git_webhook.py        # вебхук GitHub
  telegram_notify.py        # уведомления в Telegram
```
