# Goal: Qwertystock sitemap crawler cron observability

Track the moment when major search engines start fetching Qwertystock sitemap
files so the daily internal sitemap/indexing schedule can be aligned with their
real crawl windows.

## Target crawlers

- Googlebot and Google inspection/crawl agents.
- Bingbot and Microsoft indexing agents.
- YandexBot and Yandex image/search agents.

## Required signal

- Detect reads of `https://qwertystock.com/sitemap.xml`.
- Detect reads of `https://qwertystock.com/sitemaps/*.xml.gz`.
- Group sequential chunk downloads into one crawler session by crawler family,
  IP, and User-Agent.
- Send the first Telegram notification immediately when a target crawler starts
  reading sitemap files.
- Edit the same Telegram message as more chunks are fetched. Do not send one
  Telegram message per chunk.

## Daily use

- Record start time, last read time, request count, distinct sitemap files,
  status codes, bytes, crawler family, IP, and User-Agent.
- Include Google/Bing/Yandex sitemap fetch status in the daily QS indexing
  Telegram message, using `ждём` for engines that have not fetched yet.
- Include embedding health in the same daily QS indexing Telegram message:
  coverage percent, missing-embedding queue, configured limit, utilization, and
  remaining capacity or overflow.
- Use the observed Google/Bing/Yandex fetch windows to tune the daily
  `qwertystock_daily_indexer.py` cron time.
- The target state is: sitemap regeneration finishes before the regular
  crawler fetch window, with enough buffer to avoid serving stale or half-built
  sitemap indexes.

## Production implementation

- Primary watcher service:
  `qwertystock-sitemap-access-watcher.service`.
- Source:
  `R:\M_plus_front\qwertystock\ops\qwertystock_sitemap_access_watcher.py`.
- Production unit:
  `/etc/systemd/system/qwertystock-sitemap-access-watcher.service`.
- Telegram topic:
  chat `-1003904421498`, topic id `514`.

## Non-goals

- Do not submit the whole catalog to IndexNow every day.
- Do not infer actual indexing from a sitemap read. Sitemap reads only prove
  discovery/crawl of sitemap files.
- Do not trust User-Agent alone for high-stakes security decisions. For
  reporting it is enough; for enforcement add reverse DNS or ASN verification.
