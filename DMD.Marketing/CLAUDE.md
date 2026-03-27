# CLAUDE.md — DMD.Marketing

This file gives Claude context about the DMD.Marketing project so every session starts with full awareness of the codebase, conventions, and decisions already made.

---

## Project overview

**DMD.Marketing** is the public-facing marketing website for **DMD StockShopOnline** — a multi-tenant B2B SaaS POS and inventory management platform built for Canadian retail.

This is a standalone Blazor Server project (`.NET 9`) separate from the main `DMD.StockShopOnline` app. Its sole purpose is marketing, lead capture, and user account management (register, login, plan selection).

---

## Tech stack

| Layer | Choice |
|---|---|
| Framework | Blazor Server (.NET 9) |
| UI library | MudBlazor v7 |
| Fonts | Syne (headings, 700/800) + DM Sans (body, 400/600) |
| Auth | Cookie-based (`Microsoft.AspNetCore.Authentication.Cookies`) |
| Email | SendGrid via `EmailService` |
| ORM | Entity Framework Core + Npgsql (PostgreSQL) |
| Hosting target | Railway + Cloudflare DNS |
| Image storage | Cloudflare R2 (S3-compatible) |

---

## Brand palette

| Token | Hex | Usage |
|---|---|---|
| `--teal` | `#00BFA5` | Primary CTA, active states, teal accents |
| `--teal-dark` | `#00897B` | Hover states, accent text on light bg |
| `--teal-light` | `#E0F7F4` | Eyebrow badges, icon backgrounds, highlights |
| `--navy` | `#1A237E` | Headings, secondary brand color |
| `--navy-light` | `#E8EAF6` | Navy icon backgrounds |
| `--text` | `#1C1C1E` | Body text |
| `--text-muted` | `#6B7280` | Subtitles, descriptions |
| `--border` | `#E5E7EB` | Card and input borders |
| `--surface` | `#F9FAFB` | Card backgrounds, toggle cards |
| `--white` | `#FFFFFF` | Page background, card fills |

MudBlazor theme is defined in `MainLayout.razor` — `Primary = #00BFA5`, `Secondary = #1A237E`, `Tertiary = #FF6F00`.

---

## Typography rules

- **Headings** (`h1`–`h6`, `.al-title`, `.section-title`, `.sol-title`, `.hero-title`, `.plan-name`, `.price-num`): `font-family: 'Syne', sans-serif` — always
- **Body, labels, descriptions, buttons** (`p`, `.al-sub`, `.nav-desc`, `.plan-desc`, `.proof-quote`): `font-family: 'DM Sans', sans-serif` — always
- Never use Inter, Roboto, Arial, or system fonts
- MudBlazor's `Typography` object in `MainLayout.razor` enforces these globally
- Auth pages use `.al-title` and `.al-sub` with `!important` overrides in `AuthLayout.razor` to prevent MudBlazor cascade from overriding fonts

---

## Layout architecture

```
MainLayout.razor          — root shell (MudLayout, theme, mobile AppBar, drawer)
  └── NavBar component    — desktop sticky top nav (hidden on mobile)
  └── @Body               — page content
  └── Footer component    — site footer

AuthLayout.razor          — auth-only shell (centered card, no nav)
  └── @Body               — auth page content
```

### Responsive breakpoint
- `< 960px` → mobile: `MudAppBar` + `MudDrawer` slide-in
- `≥ 960px` → desktop: `NavBar` component, AppBar hidden

---

## Navigation structure

Single `Solutions` nav group in the drawer (replaces old separate `Product` + `Solutions` groups):

```
Solutions
  ├── Features            /features
  ── By industry ──
  ├── Retail Stores       /solutions/retail
  ├── Nuts & Dry Goods    /solutions/dry-goods
  └── Restaurants         /solutions/restaurants

Company
  ├── About DMD Tech      /about
  ├── Blog                /blog
  └── Contact Us          /contact

Pricing                   /pricing  (standalone link)
```

The "By industry" separator uses `.nav-section-divider` CSS defined in `MainLayout.razor`.

---

## Pages inventory

### Public marketing pages

| File | Route | Notes |
|---|---|---|
| `Home.razor` | `/` | Landing page |
| `Features.razor` | `/features` | Full feature map, 7 sections |
| `Pricing.razor` | `/pricing` | Interactive billing toggle, plan cards, FAQ |
| `SolutionRetail.razor` | `/solutions/retail` | Teal accent, retail pain points |
| `SolutionDryGoods.razor` | `/solutions/dry-goods` | Amber accent, bulk/weight-based retail |
| `SolutionRestaurants.razor` | `/solutions/restaurants` | Purple accent, includes honest fit disclaimer |
| `About.razor` | `/about` | Company page |
| `Blog.razor` | `/blog` | Blog listing |
| `Contact.razor` | `/contact` | Contact form (SendGrid) |

### Auth pages (use `AuthLayout`)

| File | Route | Notes |
|---|---|---|
| `Login.razor` | `/login` | Cookie auth, MudTextField inputs |
| `Register.razor` | `/register` | 14-day trial signup |
| `ForgotPassword.razor` | `/forgotpassword` | Sends reset link via EmailService |
| `ResetPassword.razor` | `/resetpassword` | Token + email from query string |
| `ChangePassword.razor` | `/changepassword` | Requires auth |
| `Logout.razor` | `/logout` | Signs out, redirects to `/` |
| `Profile.razor` | `/profile` | Plan selection, billing toggle, account details |

---

## Page design conventions

Every public page follows this structure:

1. **Hero** — split layout (text left, visual/card right), white background, subtle teal radial glow, eyebrow badge, Syne h1, DM Sans subtitle, CTA buttons
2. **Content sections** — `max-width: 1100px`, centered, section icon + title + description header, card grid
3. **CTA block** — light surface (`#F9FAFB`), border, rounded, centered text + buttons

### Hero background
- All heroes: `background: #FFFFFF`, `border-bottom: 1px solid #E5E7EB`
- No dark/navy hero backgrounds — intentional decision made during session
- Subtle radial teal glow via `::after` pseudo-element only

### Solution pages
Each solution page has a unique accent color for the eyebrow, quote border, and stat numbers:
- Retail → teal (`#00897B`)
- Dry Goods → amber (`#92400E`)
- Restaurants → purple (`#4A148C`)

All solution pages share: pain points grid (red-tinted), feature cards with hover effect, cross-links to other verticals, bottom CTA.

---

## Auth page conventions

- Layout: `AuthLayout.razor` — centered card on `#F5F7FA` bg, no dot-grid
- All inputs: `<MudTextField Variant="Variant.Outlined" Margin="Margin.Dense">`
- Password fields: always include show/hide eye icon via `Adornment.End`
- Form submission: SSR (`method="post"`, `[SupplyParameterFromForm]`, `[ExcludeFromInteractiveRouting]`)
- Error display: `.al-alert-error` div above the form
- Success display: `.al-alert-success` div
- Page title class: `.al-title` (Syne 1.6rem 800 navy)
- Subtitle class: `.al-sub` (DM Sans 0.9rem muted)

---

## Pricing plans

| Plan | Monthly | Annual | Key limits |
|---|---|---|---|
| Starter | $49 | $39 | 1 register, 1 warehouse, 500 products, 1 user |
| Growth | $99 | $79 | 3 registers, 3 warehouses, unlimited products, 5 users |
| Pro | $179 | $143 | Unlimited everything, multi-language |
| Enterprise | Custom | Custom | Dedicated infra, SLA, custom integrations |

Add-ons: warehouses ($15/$12 mo/annual), registers ($10/$8), Stripe Terminal reader ($399 one-time).  
Annual billing saves 20%. Toggle is interactive on both `Pricing.razor` and `Profile.razor`.

---

## Services

| Service | Namespace | Purpose |
|---|---|---|
| `UserService` | `DMD.Marketing.Services` | Create, validate, find users; password reset tokens |
| `EmailService` | `DMD.Marketing.Services` | SendGrid wrapper; sends reset emails |

Both are resolved via `HttpContext.RequestServices` in SSR form handlers.

---

## Key Blazor patterns used

- **SSR form submission**: `method="post"` + `[SupplyParameterFromForm]` + `[ExcludeFromInteractiveRouting]` on all auth pages — required because cookie auth is incompatible with SignalR interactive rendering
- **MudBlazor v7 dialogs**: `[CascadingParameter] IMudDialogInstance` (not `@inherits MudDialog`)
- **CSS escaping**: `@@media`, `@@keyframes`, `@@import` in Blazor `<style>` blocks
- **Font enforcement**: Syne/DM Sans applied via both MudBlazor `Typography` object and explicit CSS — MudBlazor's cascade can override CSS-only font declarations, so both are needed

---

## Decisions log

| Decision | Rationale |
|---|---|
| Merged Product + Solutions nav into one group | Reduces cognitive load; visitors don't think in "product vs solutions" categories |
| `/product` route dropped, links to `/features` | Avoids duplicate content; Features page is the canonical deep-dive |
| No dark/navy hero backgrounds | User preference; white heroes with teal glow are cleaner and consistent |
| Solution pages use social-proof tone | "Built for people like you" converts better than feature lists for vertical-specific pages |
| Restaurant page includes honest fit disclaimer | DMD is not a full table management system; setting expectations prevents churn |
| Auth uses SSR not interactive rendering | Cookie-based auth requires server-side `HttpContext`; SignalR circuit doesn't have access to it |
| `AuthLayout` separate from `MainLayout` | Auth pages need no nav, different background, centered card shell |
| All auth inputs use MudTextField Outlined | Consistent with MudBlazor design system; password fields get show/hide adornment |

---

## File locations (in project)

```
DMD.Marketing/
├── Layout/
│   ├── MainLayout.razor
│   ├── AuthLayout.razor
│   ├── NavBar.razor
│   └── Footer.razor
├── Pages/
│   ├── Home.razor
│   ├── Features.razor
│   ├── Pricing.razor
│   ├── Solutions/
│   │   ├── SolutionRetail.razor
│   │   ├── SolutionDryGoods.razor
│   │   └── SolutionRestaurants.razor
│   ├── Auth/
│   │   ├── Login.razor
│   │   ├── Register.razor
│   │   ├── Logout.razor
│   │   ├── ForgotPassword.razor
│   │   ├── ResetPassword.razor
│   │   └── ChangePassword.razor
│   └── Profile.razor
├── Services/
│   ├── UserService.cs
│   └── EmailService.cs
└── wwwroot/
    └── images/
        └── DMD_POS_scalable.svg
```
