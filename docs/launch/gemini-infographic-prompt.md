# Gemini image prompts — LinkedIn infographics

How to use: paste each numbered prompt into Gemini's image generation as its
own message (don't merge them — one image per prompt). Ask for **1:1 square**
(LinkedIn feed). If any text renders garbled, reply "same design, fix the text
spelling" or simplify the wording — image models struggle with long strings, so
every text line below is intentionally short.

House style (already baked into each prompt): flat vector, rounded white cards
on a pale blue background, one blue accent (#007AFF), generous margins, clean
geometric sans-serif (Segoe UI / Inter feel), no photos, no watermarks, no
logos other than the simple shield.

---

## Image 1 — the hero card (attach this one to the post)

> Design a square (1:1) flat vector infographic for a software launch post.
> Background: soft pale blue (#ECF2F9). Centered: a large rounded white card
> with a thin light border and a soft drop shadow. Inside the card, top-center:
> a bold blue shield icon (#007AFF) with a white check mark inside it. Below
> the shield, large bold dark text (#1D1D1F): "Shields Up". Under it, medium
> gray text (#86868B): "The clean-Windows button". At the bottom of the card,
> three small rounded pill chips with light blue fill (#E5F1FF) and blue text:
> "Free" · "Open source" · "Nothing to install". Clean geometric sans-serif
> type (like Segoe UI or Inter), generous spacing, flat minimal style, no
> photos, no watermark.

## Image 2 — the four buttons

> Square (1:1) flat vector infographic, pale blue background (#ECF2F9). Title
> across the top in bold dark text (#1D1D1F): "One afternoon of cleanup.
> Four buttons." Below: a 2x2 grid of rounded white cards, each with a thin
> light border and one simple line icon and a short label:
> top-left: broom icon, label "Remove the bloat";
> top-right: shield icon, label "Set the firewall";
> bottom-left: door with padlock icon, label "Close the backdoors";
> bottom-right: muted phone icon, label "Silence the phone-home".
> Icons and labels in blue (#007AFF) and dark gray, flat vector style, clean
> sans-serif, generous margins, no photos, no watermark.

## Image 3 — the trust card (use in comments when someone asks "is it safe?")

> Square (1:1) flat vector infographic, pale blue background (#ECF2F9). One
> large rounded white card, thin border, soft shadow. Title at top in bold
> dark text (#1D1D1F): "Nothing hidden". Below, four rows, each with a small
> blue check circle (#007AFF) and one short dark text line:
> "Every action asks first";
> "Almost everything has an undo";
> "No telemetry — read the code";
> "Delete the folder to uninstall".
> Clean flat vector style, sans-serif type, generous spacing, no photos, no
> watermark.

---

## Rendering notes

- Sizes: Gemini defaults are fine; request square. LinkedIn will crop to ~1:1
  in feed — keep everything inside a safe margin.
- Text accuracy: keep every string under ~30 characters (they already are).
  If the model garbles a line, regenerate — do not ship a misspelled card.
- Consistency trick: generate Image 1 first, then tell Gemini "same visual
  style as the previous image" before pasting prompts 2 and 3.
- Alternatives: for a link-preview banner (1200x628), reuse Image 1's prompt
  but ask for a wide landscape layout with the shield left and text right.
