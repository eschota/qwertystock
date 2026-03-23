# Деплой QwertyStock (way.qwertystock.com)

**Единственный клон на прод-сервере way:** `/home/debian/qwertystock`  
(nginx и скрипты рассчитаны на этот путь; других «копий» под прод не ведём.)

## Релиз инсталлера: bump + сборка + `/var/www/installer` + commit + push

Выполнять **на way**, из **корня** репозитория:

```bash
cd /home/debian/qwertystock && ./scripts/release-installer-and-push.sh
```

Делает: patch-версия в `VERSION` и `installer/QwertyStock.Bootstrapper/QwertyStock.Bootstrapper.csproj` → `dotnet publish` → обновление `installer/version.json` (SHA256) → копирование `qwertystock.exe`, `qwertystock-en.exe`, `qwertystock-ru.exe` и `version.json` в **`/var/www/installer/`** → `git commit` + `git push` в `origin`.

Без git (только сборка и файлы на диске web):

```bash
cd /home/debian/qwertystock && RELEASE_SKIP_GIT=1 ./scripts/release-installer-and-push.sh
```

Только пересобрать и залить **без** bump версии (тот же номер в `.csproj`):

```bash
cd /home/debian/qwertystock && ./scripts/publish_installer_to_www.sh
```

## Проверка

```bash
curl -sS https://way.qwertystock.com/installer/version.json
sha256sum /var/www/installer/qwertystock.exe
```

Хеш в JSON и у файла на диске должны совпадать.

## Прочее

- Зеркала pip/git/python: `sudo INSTALLER_WWW=/var/www/installer ./scripts/sync_installer_mirrors_to_www.sh`
- Статика `docs/`, лендинг `app/`: см. `qwertystock_way.md`, `app/BINARY_ASSETS_SETUP.md`
- Сервис после правок: `sudo systemctl restart qwertystock-way`
