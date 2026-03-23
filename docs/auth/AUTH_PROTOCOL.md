# Протокол авторизации: локальный кабинет QwertyStock ↔ материнский сайт

**Версия документа:** 1.0.0  

**Каноническая страница для разработчика (всё на одной странице):** [https://way.qwertystock.com/docs/auth/](https://way.qwertystock.com/docs/auth/) — манифест JSON подгружается автоматически + полный протокол.

**Манифест (машиночитаемый):** [auth-manifest.json](auth-manifest.json)

---

## 1. Цель

Пользователь входит в **учётную запись qwertystock.com** через браузер на материнском сайте. После успешного входа браузер возвращается в **локальный кабинет** (приложение на `http://127.0.0.1:7332` / `http://localhost:7332`), которое получает токены и может отображать профиль и вызывать API от имени пользователя.

Отдельного пароля в локальном приложении нет: используется **стандартный поток OAuth 2.0 Authorization Code с PKCE** (публичный клиент, без `client_secret` в бинарнике).

---

## 2. Участники

| Участник | Роль |
|----------|------|
| **Браузер пользователя** | Перенаправления между материнским сайтом и локальным кабинетом. |
| **Материнский сайт** (`www.qwertystock.com`) | Страница входа, выдача `authorization_code`, эндпоинт `token`, при OIDC — `userinfo`, JWKS, discovery. |
| **Локальный кабинет** (FastAPI на localhost:7332) | Генерация PKCE, приём `code` на `/auth/callback`, обмен `code` на токены (сервер-сервер к `token`), хранение сессии. |

---

## 3. Нормативные ссылки

- RFC 6749 — The OAuth 2.0 Authorization Framework  
- RFC 7636 — Proof Key for Code Exchange (PKCE)  
- OpenID Connect Core 1.0 (рекомендуется для `openid`, `id_token`, `userinfo`)

---

## 4. Регистрация клиента (на стороне материнского сайта)

Зарегистрировать **публичный** OAuth2/OIDC клиент:

| Параметр | Значение |
|----------|-----------|
| **client_id** | `qwertystock-local-cabinet` |
| **Тип** | Public (native / desktop / browser) |
| **Grant** | `authorization_code` |
| **PKCE** | Обязателен, метод `S256` |
| **redirect_uri** (строгое совпадение) | `http://127.0.0.1:7332/auth/callback`, `http://localhost:7332/auth/callback` |
| **client_secret** | Не выдаётся (не используется) |

**token_endpoint_auth_method:** `none`

---

## 5. Эндпоинты (целевая реализация на материнском сайте)

Ниже — канонические URL из [auth-manifest.json](auth-manifest.json). Разработчик может вынести их на API-поддомен, **но тогда нужно обновить манифест и OIDC discovery**.

| Назначение | Метод | URL (пример) |
|------------|-------|----------------|
| Авторизация (редирект пользователя) | GET | `https://www.qwertystock.com/oauth/authorize` |
| Обмен кода на токены | POST | `https://www.qwertystock.com/oauth/token` |
| Профиль (OIDC) | GET | `https://www.qwertystock.com/oauth/userinfo` |
| JWKS (проверка подписи JWT) | GET | `https://www.qwertystock.com/.well-known/jwks.json` |
| OIDC Discovery | GET | `https://www.qwertystock.com/.well-known/openid-configuration` |

**Рекомендация:** реализовать **OpenID Provider Metadata** (`openid-configuration`) со ссылками на `authorization_endpoint`, `token_endpoint`, `userinfo_endpoint`, `jwks_uri`, `issuer`.

---

## 6. Поток (Authorization Code + PKCE)

### 6.1. Подготовка (локальный кабинет)

1. Сгенерировать криптографи́чески случайный **`code_verifier`** (43–128 символов из набора `[A-Za-z0-9-._~]`).  
2. Вычислить **`code_challenge`** = BASE64URL(SHA256(`code_verifier`)) без `=` padding.  
3. Сгенерировать **`state`** (непредсказуемая строка, минимум 128 бит энтропии). Сохранить в сессии (cookie) до ответа callback.

### 6.2. Редирект пользователя на материнский сайт

Браузер открывает (GET):

```
https://www.qwertystock.com/oauth/authorize
  ?response_type=code
  &client_id=qwertystock-local-cabinet
  &redirect_uri=<URL-encoded redirect>
  &scope=openid%20profile%20email
  &state=<state>
  &code_challenge=<code_challenge>
  &code_challenge_method=S256
```

Где `redirect_uri` — один из зарегистрированных (например `http://127.0.0.1:7332/auth/callback`).

Если пользователь не залогинен — материнский сайт показывает форму входа **на своём домене**, затем продолжает поток.

### 6.3. Успешная авторизация

Редирект браузера на:

```
http://127.0.0.1:7332/auth/callback?code=<authorization_code>&state=<state>
```

Локальный кабинет:

1. Проверяет, что `state` совпадает с сохранённым.  
2. Отклоняет ответ при несовпадении или отсутствии `code`.

### 6.4. Обмен кода на токены (сервер локального кабинета → материнский сайт)

`POST https://www.qwertystock.com/oauth/token`  
`Content-Type: application/x-www-form-urlencoded`

Параметры:

| Параметр | Значение |
|----------|----------|
| `grant_type` | `authorization_code` |
| `code` | Код из callback |
| `redirect_uri` | Тот же, что в шаге authorize |
| `client_id` | `qwertystock-local-cabinet` |
| `code_verifier` | Секрет PKCE из шага 6.1 |

Успешный ответ (JSON), минимум:

```json
{
  "access_token": "...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "...",
  "id_token": "..."
}
```

- **`id_token`** — обязателен при scope `openid` (JWT; проверка подписи по JWKS, `aud` = client_id, `iss` = issuer).  
- **`refresh_token`** — желателен для обновления без повторного логина (политика срока жизни — на стороне материнского сайта).

Ошибки — по RFC 6749 (`error`, `error_description`).

### 6.5. UserInfo (опционально, при OIDC)

`GET https://www.qwertystock.com/oauth/userinfo`  
`Authorization: Bearer <access_token>`

Ответ JSON с полями вроде `sub`, `name`, `email` (зависит от scope).

---

## 7. CORS и вызовы с localhost

- Редиректы браузера **не требуют** CORS для authorize/callback.  
- Запрос **`POST /oauth/token`** идёт **с сервера локального кабинета** (не из JS на странице материнского сайта) — CORS к материнскому сайту для этого запроса не нужен.  
- Если когда-либо токен будет запрашивать **только frontend** на localhost к API материнского сайта — тогда нужен явный CORS для `https://www.qwertystock.com` с origin `http://127.0.0.1:7332` (отдельное решение).

---

## 8. Безопасность (обязательные требования)

1. **PKCE** — обязателен; без него публичный клиент на localhost уязвим к перехвату `code`.  
2. **state** — обязателен; защита от CSRF.  
3. **redirect_uri** — только точное совпадение с whitelist.  
4. Не передавать **`client_secret`** в локальный клиент.  
5. **HTTPS** на материнском сайте для authorize/token.  
6. Локальный кабинет — HTTP localhost (без TLS) допустим для redirect по [RFC 8252](https://datatracker.ietf.org/doc/html/rfc8252) для loopback.

---

## 9. Чеклист для разработчика материнского сайта

- [ ] Зарегистрирован клиент `qwertystock-local-cabinet` (public, PKCE S256).  
- [ ] Whitelist redirect URI: `http://127.0.0.1:7332/auth/callback`, `http://localhost:7332/auth/callback`.  
- [ ] Реализованы `GET /oauth/authorize`, `POST /oauth/token`.  
- [ ] Поддержка `grant_type=authorization_code` с `code_verifier`.  
- [ ] (Рекомендуется) OIDC: `id_token`, `GET /oauth/userinfo`, `/.well-known/openid-configuration`, JWKS.  
- [ ] Документированы коды ошибок и срок жизни токенов.

---

## 10. Версионирование

- **`manifestVersion`** в [auth-manifest.json](auth-manifest.json) — версия контракта.  
- Изменение эндпоинтов или `client_id` — новая минорная/мажорная версия манифеста и уведомление команды локального кабинета.

---

## 11. English summary (for implementers)

The **QwertyStock Local Cabinet** (FastAPI on `http://127.0.0.1:7332`) uses **OAuth 2.0 Authorization Code with PKCE** against **www.qwertystock.com**. Register public client `qwertystock-local-cabinet`, whitelist the two localhost redirect URIs, expose **authorize** and **token** endpoints, optionally full **OIDC** (userinfo, id_token, discovery, JWKS). No client secret. See [auth-manifest.json](auth-manifest.json) for canonical URLs.

---

## 12. Пример nginx (way) для раздачи документации

```nginx
location ^~ /docs/ {
    alias /home/debian/qwertystock/docs/;
    try_files $uri =404;
    default_type text/plain;
    charset utf-8;
}
location = /docs/auth/AUTH_PROTOCOL.md {
    alias /home/debian/qwertystock/docs/auth/AUTH_PROTOCOL.md;
    default_type text/markdown;
    charset utf-8;
}
```

Для `index.html` использовать `default_type text/html`.
