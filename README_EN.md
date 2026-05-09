# ClothingRecycler Desktop

[English](README_EN.md) | [简体中文](README_CN.md)

ClothingRecycler Desktop is a local Windows application for daily clothing recycling operations. It supports purchase intake, outbound sales, inventory layers, order confirmation, customer records, exports, and business review workflows for recycling, sorting, and resale teams.

Current repository version: `v1.0.7`

## Highlights in v1.0.7

### AI(Beta) Workbench

- The left navigation adds an `AI(Beta)` module for describing inbound or outbound tasks in natural language.
- The agent detects the business route automatically, so users do not need to choose intake or outbound mode manually.
- When required fields such as customer, category, quantity, or price are missing, the agent asks follow-up questions.
- Once the information is complete, the agent enters the existing confirmation flow: intake opens the intake confirmation sheet, and outbound continues into the matching creation flow.
- Key agent steps are shown in the page so users can inspect understanding, slot filling, tool choice, and execution results.

### API Model Calling Beta

- A new `DeepSeek API (Beta)` provider can be configured locally with `base_url`, `model`, and API key.
- API keys are stored in local user configuration and are not committed to this repository.
- The current test model is `deepseek-v4-flash`, used to turn natural language into structured order drafts.

### Voice Input and ASR

- A standalone ASR module handles `audio input -> speech recognition -> text cleanup -> agent input`.
- ASR providers use an adapter design so the implementation can later switch to local Whisper, cloud ASR, Windows speech recognition, or other engines.
- The AI page supports audio-file recognition and live voice input.
- Live recognition continuously fills the editor; users can review and correct the transcript before sending it to the agent.
- The UI exposes Windows recognizer status to avoid pretending transcription is working when no text is being returned.

### Local Offline AI Path

- The Hugging Face Ultravox + transformers path remains available for continued offline inference and speech-model experiments.
- The Ollama provider entry remains in place for future local LLM integration.

## Core Features

### Transaction Entry

- Intake supports form entry followed by a confirmation sheet; stock changes only after the user confirms intake.
- Outbound entry can expand inventory by category, which fits high-frequency workflows where one customer usually buys one category.
- Intake and outbound transactions can override the counting unit at transaction time. For example, a category may default to kilograms while a specific transaction is recorded in jin.
- The system converts quantities automatically and keeps the inventory page aligned to each category's default unit.

### Order Flow

- Order names follow the pattern `入库/出库_yyyy_MM_dd_HH_mm_customer`.
- Orders can be viewed, edited, deleted, and exported again.
- Editing an order preserves the original transaction unit to avoid treating `20 jin` as `20 kg`.
- Intake and outbound confirmation sheets can be exported as `PDF` and `PNG`.

### Inventory and Business Analysis

- One category can keep multiple purchase-price layers.
- Inventory separates cost layers instead of flattening everything into one price.
- Stocktaking and manual adjustments are supported.
- Estimated revenue uses a net-profit view and exposes the calculation formula.
- Dashboard, business analysis, and estimated revenue pages show operating summaries and profit rankings.

## Main Pages

- `Dashboard`: today's intake amount, today's outbound amount, inventory cost, estimated revenue, and business overview.
- `Intake`: choose a customer, enter category, quantity, and price, then confirm before stock is changed.
- `Outbound`: browse category inventory, expand a category, enter outbound details, and confirm.
- `Inventory`: review quantities, cost, price layers, estimated net profit, and stocktaking records.
- `Orders`: view intake/outbound details, edit, delete, and export confirmation sheets.
- `Customers`: inspect customer records, recent orders, and remembered prices.
- `AI(Beta)`: turn text, audio files, or live voice into business order drafts.
- `Settings`: manage categories, local backup and restore, import/export, AI providers, and diagnostics.

## Installation and Data

### Install

- Download the latest installer from GitHub Releases.
- The installer supports in-place upgrades.
- The default install path is `%LOCALAPPDATA%\Programs\ClothingRecycler`.

### Data Location

- Application data is stored under `%LOCALAPPDATA%\ClothingRecycler` by default.
- The uninstaller does not automatically remove databases, backups, exports, or logs.

## FAQ

### Does business data upload to a server?

Core business data stays on the local machine. If an API provider is enabled, the text instruction sent to the AI provider is transmitted to the configured model service. API keys remain in local configuration.

### Can AI change inventory directly?

No. AI creates a reviewable order draft and enters the existing confirmation flow. The final intake or outbound operation still requires user confirmation.

### Why can live voice input show no text?

Live speech recognition depends on Windows speech recognition, microphone permission, the default input device, and installed language packs. If the session is running but no transcript appears, Windows likely did not return candidate text.

### What if an order is entered incorrectly?

Find the order in the order center and edit or delete it. The system synchronizes the related inventory rollback and derived data.

### Can confirmation sheets be sent to customers?

Yes. Confirmation sheets can be exported as `PDF` and `PNG` for printing, archiving, or sending to customers.

## Quick Start for Users

A shorter end-user guide is available in [README_BEGINNER.md](./README_BEGINNER.md).

