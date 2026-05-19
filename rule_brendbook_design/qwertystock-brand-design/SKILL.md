---
name: qwertystock-brand-design
description: Apply the Qwertystock brand design system when Codex edits Qwertystock UI, logos, favicons, brand assets, marketing visuals, public pages, product pages, admin screens, API docs, or any design/CSS/frontend work for qwertystock.com. Use this whenever the user asks for Qwertystock design, brandbook, visual identity, logo usage, colors, dark shell, favicon, Open Graph image, or consistent UI styling.
---

# Qwertystock Brand Design

## Core Rule

Use the blue/cyan QS orbit mark as the canonical Qwertystock logo. Do not reintroduce the old pink outline `Q` mark or a plain-letter rail logo.

Primary assets live in this skill under `assets/logo/`. The canonical project brandbook lives in `R:\qwertystock\rule_brendbook_design`.

## Visual System

- Base UI: dark neutral shell, near-black backgrounds, subtle panels, restrained borders.
- Brand accent: cyan/blue/violet glow from the logo.
- Commerce accent: Qwertystock pink may stay for primary action buttons where it already exists.
- Logo use: prefer the square QS orbit mark for favicon, side rail, compact UI, app icons, and social previews.
- Header use: pair the QS orbit mark with text `Qwertystock` in white, heavy weight, zero letter spacing.
- Do not use decorative gradient orbs, beige/brown palettes, or large marketing hero cards for product/tool pages.

## Asset Map

- `assets/logo/source-qwertystock-qs-orbit-blue.jpg`: original source image.
- `assets/logo/qwertystock-mark-1024.png`: master square web logo.
- `assets/logo/qwertystock-mark-512.png`: app/social icon.
- `assets/logo/qwertystock-mark-128.png`: side rail/header mark.
- `assets/logo/favicon.ico`, `favicon-32x32.png`, `favicon-16x16.png`: browser icons.
- `assets/logo/qwertystock-og-1200x630.png`: Open Graph fallback preview.

## Implementation Checklist

When changing Qwertystock frontend design:

1. Keep all new brand assets in `client/public/img/brand/` and mirror canonical files into `R:\qwertystock\rule_brendbook_design\assets`.
2. Keep favicon files in `client/public/img/icons/`.
3. Update `site.webmanifest`, `browserconfig.xml`, and template favicon/OG tags when icon assets change.
4. Use the side rail brand mark as an image, not the literal text `Q`.
5. Run local build and browser smoke on at least `/`, `/item?id=3295318`, and `/api`.
6. Deploy only with `VERSION` bump and GitHub `main` push.
