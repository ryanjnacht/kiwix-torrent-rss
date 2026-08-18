# Kiwix Torrent RSS Service

A .NET 10 minimal API service that scrapes the Kiwix OPDS catalog, generates an RSS feed of ZIM file torrents, and
proxies torrent downloads to work around old BitTorrent client TLS compatibility issues.

## Use the hosted service

Don't want to self-host? The feed is publicly available:

```
https://kiwix-torrent-rss.duckduckdev.nl/kiwix.rss
```

Add this URL as a torrent RSS feed in your BitTorrent client (uTorrent and others) and it will keep finding new and
updated ZIM file torrents for you. All of the query parameters documented below (`?q=`, `?proxy=true`, `?format=`)
work on the hosted instance. The rest of this README describes how to build and run your own copy.

## Overview

This service periodically fetches the [Kiwix OPDS catalog](https://opds.library.kiwix.org/catalog/v2/entries?count=-1) —
a live listing of all available ZIM files (offline Wikipedia, documentation, and more) — and serves it as a
torrent-compatible RSS feed. Torrent download URLs are rewritten to route through this application, which streams the
files directly from the Kiwix servers. This solves compatibility issues with older BitTorrent clients (like uTorrent)
that cannot negotiate modern TLS connections with the Kiwix download server.

## Features

- **Live catalog scraping** — Fetches the full Kiwix OPDS catalog (3,600+ entries) on a configurable interval (default:
  every 24 hours)
- **RSS 2.0 feed** — Compact, single-line XML output compatible with uTorrent and other torrent clients
- **Searchable feed** — Filter results via the `?q=` query parameter with multi-word AND logic
- **Entry classification** — Each entry is tagged as:
    - **UNIQUE** — only version of this ZIM file
    - **LATEST** — newest version of a multi-version series
    - **OBSOLETE** — superseded by a newer version
- **Optional torrent proxy** — Pass `?proxy=true` to rewrite torrent URLs through this app, bypassing TLS issues with old clients
- **Customizable item titles** — Shape RSS item titles with a field template via the `?format=` query parameter or the `FeedItemTitleFormat` setting
- **Zero external dependencies** — No NuGet packages beyond the built-in ASP.NET Core SDK

## Endpoints

| Endpoint                         | Description                                                          |
|----------------------------------|----------------------------------------------------------------------|
| `GET /kiwix.rss`                 | RSS feed with original torrent URLs                                  |
| `GET /kiwix.rss?proxy=true`      | RSS feed with torrent URLs proxied through this app                  |
| `GET /kiwix.rss?q=wikipedia eng` | Filtered RSS — matches entries containing both "wikipedia" AND "eng" |
| `GET /kiwix.rss?q=2026 eng GB`   | Multi-word filter — all terms must match                             |
| `GET /kiwix.rss?format=title+language` | RSS feed with a custom item title format                    |
| `GET /proxy/torrent?u=<url>`     | Proxies a torrent file download through the app                      |
| Any other path                   | 404 Not Found                                                        |

### Query parameter examples

| Query                   | Meaning                                     |
|-------------------------|---------------------------------------------|
| (none)                  | All entries                                 |
| `?q=eng`                | English-language entries                    |
| `?q=2026`               | Entries from 2026                           |
| `?q=2026 eng`           | English entries from 2026 (both must match) |
| `?q=wikipedia eng 2026` | English Wikipedia entries from 2026         |
| `?q=GB`                 | Large files (1+ GB)                         |
| `?q=LATEST`             | Only the latest version of each series      |
| `?q=UNIQUE`             | Entries with no older/newer versions        |
| `?q=OBSOLETE`           | Superseded entries                          |

### Proxy parameter

| Query                   | Meaning                                                                |
|-------------------------|------------------------------------------------------------------------|
| (none)                  | Original torrent URLs (direct to Kiwix server)                         |
| `?proxy=true`           | Torrent URLs rewritten to route through this app (`/proxy/torrent`)    |

Use `?proxy=true` when your BitTorrent client cannot negotiate modern TLS with the Kiwix download server (e.g. older uTorrent versions).

### RSS feed item format

Item titles are built from a `+`-separated field template, configured either per request
with the `?format=` query parameter or as a default with the `FeedItemTitleFormat` setting.
Fields that are empty for a given entry are omitted, and the remaining values are joined
with a single space.

Supported fields:

| Field      | Example              |
|------------|----------------------|
| `title`    | Python PEPs          |
| `name`     | peps                 |
| `language` | eng                  |
| `version`  | 2026-08              |
| `status`   | UNIQUE               |
| `category` | wikipedia            |
| `flavour`  | nopic                |
| `size`     | 8 MB                 |

With the default template (`title+name+language+version+status+size`):

```
Python PEPs peps eng 2026-08 UNIQUE 8 MB
```

If no template is set, items fall back to a detailed format that includes all available
metadata:

```
Python PEPs - peps.python_en_all_2026-08.zim (eng, 2026-08) [UNIQUE] - 8 MB; other (standard)
```

Components: **Display Title** — **filename.zim** (**language**, **version**) **[STATUS]** — **size; category (flavour)**

Note: `?q=` filtering always matches against the detailed format, regardless of the title template in use.

## Configuration

Edit `appsettings.json`:

```json
{
  "KiwixRss": {
    "ScrapingIntervalHours": 24,
    "CatalogUrl": "https://opds.library.kiwix.org/catalog/v2/entries?count=-1",
    "DownloadBaseUrl": "https://lb.download.kiwix.org",
    "FeedTitle": "Kiwix Torrent RSS",
    "FeedLink": "https://kiwix.org",
    "FeedDescription": "Kiwix ZIM Files - Torrent Downloads",
    "FeedItemTitleFormat": "title+name+language+version+status+size"
  }
}
```

| Setting                 | Default                                | Description                                        |
|-------------------------|----------------------------------------|----------------------------------------------------|
| `ScrapingIntervalHours` | 24                                     | How often to re-scrape the catalog                 |
| `CatalogUrl`            | (see above)                            | OPDS catalog API endpoint                          |
| `FeedTitle`             | Kiwix Torrent RSS                      | RSS channel title                                  |
| `FeedLink`              | https://kiwix.org                      | RSS channel link                                   |
| `FeedDescription`       | (see above)                            | RSS channel description                            |
| `FeedItemTitleFormat`   | title+name+language+version+status+size | `+`-separated field template for RSS item titles  |

## Building and Running

### Local development

```bash
dotnet run
```

### Docker

```bash
docker build -t kiwix-rss .
docker run -p 80:80 kiwix-rss
```

## Architecture

```
Kiwix OPDS API ──► KiwixScraper ──► KiwixClassifier ──► KiwixRssCache
                                                        ▲
uTorrent ──► /kiwix.rss ──► KiwixRssBuilder ──► XML response
                ▲                                      ▲
                │           /proxy/torrent ──► KiwixTorrentProxy
                │                                       │
                └───────────────────────────────────────┘
                              (proxied torrent URLs)
```

### Key components

| Component           | Role                                                          |
|---------------------|---------------------------------------------------------------|
| `KiwixRssService`   | `IHostedService` background worker — scrapes on a schedule    |
| `KiwixScraper`      | Fetches and parses the OPDS Atom XML catalog                  |
| `KiwixClassifier`   | Groups entries by base name, marks UNIQUE / LATEST / OBSOLETE |
| `KiwixRssBuilder`   | Generates compact RSS 2.0 XML with optional query filtering   |
| `KiwixRssCache`     | Thread-safe singleton cache of scraped entries                |
| `KiwixTorrentProxy` | Streams torrent files from Kiwix servers through the app      |

## File structure

```
kiwix-rss/
├── kiwix-rss.csproj                          # .NET 10 Web SDK project
├── Program.cs                                # Kestrel config, routing, DI
├── appsettings.json                          # Configuration
├── Dockerfile                                # Multi-stage Docker build
├── .gitignore                                # Git ignore rules
├── LICENSE                                   # MIT License
├── README.md                                 # This file
├── Models/
│   ├── KiwixEntry.cs                         # ZIM file entry model
│   └── KiwixRssSettings.cs                   # Configuration POCO
└── Services/
    ├── KiwixScraper.cs                       # OPDS catalog scraper
    ├── KiwixClassifier.cs                    # UNIQUE/LATEST/OBSOLETE classification
    ├── KiwixRssBuilder.cs                    # RSS XML generator with query filter
    ├── KiwixRssCache.cs                      # Thread-safe entry cache
    ├── KiwixRssService.cs                    # Background hosted service
    └── KiwixTorrentProxy.cs                  # Torrent download proxy
```

## Notes

- The service starts scraping immediately on startup, then repeats at the configured interval
- If scraping fails, the previous cached data is preserved (stale-but-serving)
- The proxy endpoint has a 5-minute timeout to accommodate large torrent metadata files
- No external NuGet dependencies — uses only built-in ASP.NET Core and `System.Xml`
