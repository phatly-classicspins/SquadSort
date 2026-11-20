# Changelog

## 0.1.0 — 2026-11-07

- Add optional Adjust SDK 5.8.2 analytics module for PrototypeFramework 0.9.0, with OpenUPM
  installation metadata and EDM4U 1.2.190 requirement.
- Map shared record wire names to configured Adjust tokens, and AppLovin MAX paid impressions to
  Adjust ad revenue. Ignore unsupported user properties with once-per-key warnings.
- Add lazy Analytics-consent startup, DMA signals before initialization, withdrawal/regrant handling,
  provider commands, logged Null refusal, and shared 12-character app-token/environment validation
  at runtime and in read-only build preflight. Explicit startup Enable and final consent gates guard
  reentry; failed transitions latch refusal with best-effort disable.
- Forget-me refuses denied/disabled/unstarted SDKs without enabling. Active/granted acceptance
  terminally refuses ordinary work/re-enables in this instance; acceptance never proves server erasure
  or acceptance across instances/native SDK/devices.
- Include headless behavior/metadata tests and real reflected-container EditMode installer coverage.
  SDK-enabled/native/device delivery remains unverified; no vendor contents, release tag or publish.
