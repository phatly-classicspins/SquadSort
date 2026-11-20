# Adjust Analytics 0.1.0

Optional attribution events and ad revenue behind PrototypeFramework 0.9.0's existing
`IAnalyticsProvider` and `IAnalyticsCommandTarget`. Provider id: `analytics-adjust`.
Both ports resolve one provider singleton. There is no boot node and gameplay never names Adjust.

## Install in a consuming SKU

1. Add this package through **Framework/Setup Wizard ▸ Modules**. The framework range is
   `>=0.9.0 <0.10.0`; the package pins only framework `0.9.0`.
2. Click **Install** to add OpenUPM scopes `com.adjust` and
   `com.google.external-dependency-manager`, with `com.adjust.sdk` **5.8.2** and EDM4U
   **1.2.190**. This module contains no vendor payload. Avoid another imported Adjust/EDM4U copy.
3. Configure the bundled defaults below. Create `AdjustAnalyticsInstaller.asset` with the module
   installer menu and slot it on RootLifetimeScope in the Master Scene through the live Editor.
4. Resolve native dependencies with EDM4U and perform Android/iOS device validation before release.

Only `Sdk` is constrained by `PF_ADJUST`; runtime, SDK and Editor roles have matching
`com.adjust.sdk` version defines. SDK absence still installs this provider with a logged, refusing
Null bridge. It never reports delivery through the framework fake. Doctor and Wizard report
installation state. Build preflight refuses SDK unavailability, missing/invalid bundled app token, and invalid explicit environment;
remote-only tokens cannot satisfy the build. Probe notes are reported. Unrelated absence is inert.
An unmapped event is a runtime once-per-name warning, not a strict token build gate.

## Configuration

| Key | Value |
| --- | --- |
| `analytics.adjust.appToken` | Nonblank 12-character Adjust app-token string, required in bundled `ConfigDefaults`. |
| `analytics.adjust.environment` | Absent defaults to `sandbox`; explicit values must be exactly `sandbox` or `production`. |
| `analytics.adjust.token.<wire_name>` | Adjust event token for that record wire name. Read on every call, so config updates take effect. Missing/blank drops the event and warns once per name. |
| `analytics.adjust.adSignals` | Absent grants ad signals. Numeric zero or string `"0"` denies them; other values grant them. Read with `ConfigKey<object>` at first eligible startup. |

App token, environment and ad signals are read for the first initialization attempt. Updating them
after start does not restart the SDK. Bad startup config or a start failure is latched for this
provider lifetime; subsequent work is refused. Keep one module installer and one Adjust initialization
owner per process. Do not add an auto-starting Adjust component or initialize Adjust elsewhere.

A mapped record becomes one `AdjustEvent` with its callback parameters from `GameEvent.WriteParams`.
`WireValues` supplies invariant numeric text and bool `1`/`0`; null strings are omitted. Serialization
or SDK failures drop that call without retry. User properties are unsupported: ignored with one warning
per key, with no SDK startup and no mapping onto event parameters.

Only `AdRevenuePaid` from `applovin_max` becomes an `AdjustAdRevenue`, source
`applovin_max_sdk`, with amount, currency, network, unit and placement. It never also becomes a token
event, even if mapped. Unknown mediators, nonfinite/negative revenue and currencies other than three
uppercase ASCII letters are logged and dropped. Zero revenue is valid. No purchase-revenue mapping
ships in this version.

## Consent and startup

All APIs are synchronous main-thread calls and no-throw. Resolved, granted Analytics consent is
required for ordinary work. Startup is lazy and synchronous: before `InitSdk`, the module calls
`TrackThirdPartySharing` with `google_dma` `ad_personalization` and `ad_user_data` set to `1` or `0`.
It never asserts `eea` or reads PersonalizedAds consent. After `InitSdk`, it explicitly calls `Enable` only while current Analytics consent remains granted before dispatching first work. Adjust requires explicit `InitSdk`, so this
module adds no native auto-start collection flag. It refuses SDK-shell calls in the Editor and on
unsupported platforms instead of treating the vendor's void no-ops as acceptance.

Consent callbacks never initialize Adjust. Withdrawal after start calls `Disable`; regrant calls
`Enable` on the same SDK unless this instance is failed or terminal after accepted forget-me. Startup
and the final gate immediately before ordinary work recheck current consent and state after external
config, serialization and SDK calls. Reentrant withdrawal during `Enable` settles with `Disable`;
callbacks during a transition cannot recursively re-enable/disable. There is no artificial async
queue; reentrant initialization/transition work is refused. Failed work is never replayed. SDK calls returning normally mean accepted by the shell, never proven
network delivery. An SDK failure during consent transitions latches refusal and best-effort disables.

## Commands

Call `IAnalyticsCommands.Execute("analytics-adjust", command, args)` from a SKU Application or
Composition service/forwarder. The command router and provider require granted Analytics consent
for ordinary commands. `true` means accepted now; `false` means refused. Arguments are ordinal,
nonblank, and must include exactly the named keys once each, in any order. Unknown commands,
unknown/duplicate/missing keys and malformed forms are logged and refused.

| Command | Arguments | Behavior |
| --- | --- | --- |
| `track_token` | `token` | One token event without event-specific parameters. May lazily start the SDK. |
| `add_callback_param` | `key`, `value` | Calls `AddGlobalCallbackParameter`, affecting subsequent SDK activity. May lazily start. |
| `add_partner_param` | `key`, `value` | Calls `AddGlobalPartnerParameter`, affecting subsequent SDK activity. May lazily start. |
| `set_user_id` | `id` | Explicitly unsupported, always `false`; no global-parameter substitute. |
| `privacy.forget_me` | None (null or empty list) | Calls `GdprForgetMe` only for an already-started, active SDK under current granted consent. Returns `false` while denied, disabled, unstarted, failed or terminal. Never initializes or re-enables for privacy. |

The two global-parameter commands run after synchronous startup. They are not session callbacks
retroactively attached to the first install/session. Erasure through this command while consent is
denied/collection disabled is explicitly unsupported: callers receive `false`, and the SDK stays
disabled. If the active/granted bridge accepts forget-me, this provider instance becomes terminal:
subsequent ordinary work and re-enables are refused. A refused/thrown forget does not mark terminal.
Acceptance means only that this bridge accepted the command; it never proves server deletion
finished, and it makes no acceptance claim across provider instances, native SDK state or devices.
Follow Adjust guidance and provide an appropriate separate erasure process for unsupported cases.

## Verification boundaries and source references

The headless suite covers fake-bridge behavior, Null refusal, preflight rules, metadata and assembly
shape. Shared fake enabled-state coverage across provider instances is simulation only, not native
persistence or cross-instance forget acceptance evidence. The reflected EditMode installer fixture checks the real container exposes the same singleton
on both ports and reaches the refusing Null bridge. Adopted preflight fixtures create temporary
scene/installer assets through Editor APIs and restore build settings, proving the real enabled-scene
and bundled-default probes report missing SDK/token findings. Source shape checks do not compile the SDK shell.

API/source inspection used official pinned Adjust Unity **v5.8.2** sources:
[Adjust.cs](https://github.com/adjust/unity_sdk/blob/v5.8.2/Assets/Adjust/Scripts/Adjust.cs),
[AdjustConfig.cs](https://github.com/adjust/unity_sdk/blob/v5.8.2/Assets/Adjust/Scripts/AdjustConfig.cs),
[AdjustEvent.cs](https://github.com/adjust/unity_sdk/blob/v5.8.2/Assets/Adjust/Scripts/AdjustEvent.cs),
[AdjustAdRevenue.cs](https://github.com/adjust/unity_sdk/blob/v5.8.2/Assets/Adjust/Scripts/AdjustAdRevenue.cs), and
[AdjustThirdPartySharing.cs](https://github.com/adjust/unity_sdk/blob/v5.8.2/Assets/Adjust/Scripts/AdjustThirdPartySharing.cs).
`AdjustAdRevenue(string source)` accepts a string; it declares no AppLovin constant. The official
[Adjust AppLovin integration guide](https://dev.adjust.com/en/sdk/unity/integrations/applovin/)
explicitly uses `applovin_max_sdk` and the revenue/network/unit/placement APIs used here. This is
source inspection, not real SDK execution.

SDK-enabled compilation, EDM4U/native resolution, Android/iOS exports, native consent persistence,
attribution, dashboard delivery, PlayMode, Addressables content and IL2CPP remain unverified unless
separately executed and recorded. No vendor SDK was imported and no tag was cut for this story.
