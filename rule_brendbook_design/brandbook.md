# Qwertystock Brandbook

## Canonical Mark

The canonical logo is the blue/cyan QS orbit mark from `assets/logo/source-qwertystock-qs-orbit-blue.jpg`.

Use generated web assets from `assets/logo/` and mirror them into the main site:

- `client/public/img/brand/qwertystock-mark-1024.png`
- `client/public/img/brand/qwertystock-mark-512.png`
- `client/public/img/brand/qwertystock-mark-128.png`
- `client/public/img/brand/qwertystock-og-1200x630.png`
- `client/public/img/icons/favicon.ico`
- `client/public/img/icons/favicon-32x32.png`
- `client/public/img/icons/favicon-16x16.png`

## Colors

- Brand background: `#020307`
- Brand cyan: `#24d8ff`
- Brand blue: `#2378ff`
- Brand violet: `#8a64ff`
- UI text: `#ffffff`
- Muted text: `#a9adbd`
- Existing action pink may remain for commerce CTAs: `#f93f82`

## Usage

- Side rail brand must use the image mark, never the literal `Q`.
- Favicon/app icons must use the blue QS orbit mark.
- Open Graph fallback image must use `qwertystock-og-1200x630.png`.
- Keep the product and API UI dark, compact, and marketplace-like.
- Do not reintroduce the old pink outline logo as primary brand.

## Production Copy

- Production pages must hide internal implementation details. Public copy should explain the customer-facing process and result, not the servers, workers, queues, repos, env vars, aliases, provider plumbing, or deployment mechanics behind it.
- Do not use names such as `WAY`, `qwertydata`, `qwertysearch`, `PM2`, `nginx`, `Mongo`, `PostgreSQL`, `Namecheap API`, `OpenAI prompt`, `Detectron`, `IndexNow worker`, `sitemap watcher`, or `Telegram bot` in buyer/author-facing pages unless the page is explicitly admin/ops documentation.
- Preferred wording examples:
  - "Проверяем доступность и итоговую цену домена" instead of "проверяем через WAY/Namecheap API".
  - "Готовим файл для скачивания" instead of "server fallback/transcode job".
  - "AI-поиск по смыслу" or "семантический поиск" instead of raw vector-distance or embedding internals.
- Status text should tell users what is happening, what is ready, what failed, and what to do next. It must not expose the internal path used to complete the task.

## Asset Generation

Generate web assets from the source with ImageMagick. Keep generated files in both:

- `R:\M_plus_front\qwertystock\client\public\img\brand`
- `R:\qwertystock\rule_brendbook_design\assets\logo`
