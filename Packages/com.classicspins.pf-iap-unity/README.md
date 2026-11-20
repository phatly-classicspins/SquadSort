# com.classicspins.pf-iap-unity

An optional **service module**: Unity IAP 5 (`com.unity.purchasing` — App Store and Google Play)
behind the framework's unchanged `IPurchaseService` port. A SKU that sells real products installs this
package; every other SKU keeps the framework's `FakePurchaseService` and never sees the dependency.

`pf-module.json` is the single source of truth for what the module needs. Install it from
**`Framework/Setup Wizard ▸ Modules`**, which writes the pinned `com.unity.purchasing` into
`Packages/manifest.json` (it comes from the Unity registry — no scoped registry) and then prints the
human-zone checklist (`manualSteps`).

## Layout — four roles (AD-MOD-1)

| Role | Folder | Assembly | Holds |
|---|---|---|---|
| Core | `Runtime/Core/` | `ClassicSpins.PrototypeFramework.Iap.Unity.Core` — engine-free, ungated | `IUnityIapBridge`, `UnityIapStoreProduct`, `UnityIapPurchaseService`, `UnityIapInitNode`, `UnityIapResultMapping`, `UnityIapPreflightRules` |
| Sdk | `Runtime/Sdk/` | `ClassicSpins.PrototypeFramework.Iap.Unity.Sdk` — gated on `PF_UNITY_IAP` | `UnityIapBridge` (the only file naming a Unity IAP / UGS type), the `PfModuleMarker_PF_UNITY_IAP` marker |
| runtime | `Runtime/` | `ClassicSpins.PrototypeFramework.Iap.Unity` — ungated | `UnityIapInstaller`, `NullUnityIapBridge` |
| Editor | `Editor/` | `ClassicSpins.PrototypeFramework.Iap.Unity.Editor` — editor-only, ungated (`#if PF_UNITY_IAP` inside) | `UnityIapPreflight : IModulePreflight` (fact gathering only) |

`PF_UNITY_IAP` is armed by a `versionDefines` entry on `com.unity.purchasing` (expression `5.0.0`) in
the `Sdk`, runtime and Editor asmdefs — never a Player Settings symbol. The module is written against the
**IAP 5 API** (`UnityIAPServices.StoreController()`, `OnPurchasePending` → `ConfirmPurchase`); IAP 4's
`IStoreListener` API is gone there, so a 4.x package leaves the define unarmed and the preflight says so.

## Absence refuses — it never falls back to the fake

The installer is **not** define-gated. It always registers `UnityIapPurchaseService` as
`IPurchaseService` and replaces the framework's `IapInit` node. Only the bridge behind them depends on
`PF_UNITY_IAP`:

- **Unity IAP resolved** ⇒ `UnityIapBridge`, a thin shell over `StoreController`.
- **Unity IAP absent** (or older than 5.0.0) ⇒ `NullUnityIapBridge`: `IapInit` degrades (`IapReady`
  is still emitted), every `BuyAsync` resolves `Failed`, and each refusal logs a warning naming
  `pf-iap-unity`. The framework's `FakePurchaseService` — which grants every purchase for free — is never
  left resolved.

## The catalog — `Resources/Config/products.json`

The framework's `BakedCatalogSource` reads the SKU's catalog; this module adds nothing to it. Per
product the framework reads `id`, `disabled`, `grants` and — new in the framework alongside this module —
the optional **`type`**: `"Consumable"` (default when absent) or `"NonConsumable"`. Every other field a
SKU adds (display key, section, art…) is ignored by the framework and stays the SKU's.

```jsonc
{ "products": [
  { "id": "com.brainbucks.sushi.sort.gold_1k",     "type": "Consumable",
    "grants": [ { "kind": "coin", "id": "coins", "amount": 1000 } ] },
  { "id": "com.brainbucks.sushi.sort.remove_ads",  "type": "NonConsumable",
    "grants": [ { "kind": "entitlement", "id": "remove_ads" } ] },
  { "id": "com.brainbucks.sushi.sort.starter_pack", "type": "NonConsumable",
    "grants": [ { "kind": "entitlement", "id": "starter_pack" }, { "kind": "coin", "id": "coins", "amount": 500 } ] }
] }
```

- **The `id` is the store Product ID, verbatim**, on both stores (one id, no per-store ids).
- **A product that must survive a restore is `NonConsumable` *and* grants an entitlement.** The store
  returns only non-consumables on a restore or a new device, and only entitlement legs are replayed —
  the starter pack's coins are not granted twice. A `Consumable` that grants a permanent entitlement is
  warned at init (it could never be restored).
- A `disabled` product is never registered with the store and never sells.

## Behaviour

- **Init (`IapInit` replacement).** Initializes Unity Gaming Services, connects the store, registers every
  enabled catalog product with its store type, then fills `ProductInfo.LocalizedPrice`
  (`metadata.localizedPriceString`) and `Available` (`availableToPurchase`). **UGS init is a step of its
  own** (bridge `InitializeServicesAsync`, skipped when something already initialized UGS), bounded at
  10 s and best-effort: a UGS error or timeout is logged as a warning and the store connects anyway, so a
  UGS fault never skips the store init. Connect and product fetch are
  each bounded at 30 s on a realtime delay; a failure or timeout **degrades** `IapReady` (still emitted)
  and the shop shows the baked catalog with "loading" prices. A successful init runs once per process
  (reruns are free); a failed one is retried by the next `InitializeAsync`. After init it fetches the
  player's purchases (see *pending* and *reinstall* below).
- **`BuyAsync`.** `Failed` without a store call for a null, disabled, not-in-catalog or store-unavailable
  product, before init, or while another purchase is in flight. Otherwise it starts the store flow and
  resolves from the store's answer:

  | Store answer | Result | Funnel event |
  |---|---|---|
  | pending order (paid) | `Success` — granted, then confirmed | `PurchaseSucceeded` |
  | `UserCancelled` | `Cancelled` | — |
  | `ExistingPurchasePending` | `Pending` | — |
  | deferred (Ask to Buy) | `Deferred` — granted when the approval arrives | `PurchaseSucceeded` then |
  | any other failure reason | `Failed` | `PurchaseFailed` |

  A cancelled token stops the *caller* waiting (rethrows `OperationCanceledException`); the store flow
  continues, and a payment that completes later is still granted.
- **Grants — once per transaction.** Every paid order arrives through one pending callback: a purchase
  just made, an approval that came later, an order an earlier session left unconfirmed. Each is granted
  through the framework's `GrantInterpreter` (`GrantSource.Iap`), confirmed to the store, and tracked.
  A transaction id granted in this process is never granted again — a redelivery (the store's own
  callback *and* the purchase fetch's `PendingOrders` pass, or a confirmation that failed) is only
  re-confirmed. A grant that throws leaves the order **unconfirmed**, so the store delivers it again
  rather than the player paying for nothing.
- **`RestoreAsync`.** On iOS it first runs Apple's restore-transactions; on every platform it then
  fetches the player's purchases and re-applies the **entitlement legs** of every store-owned
  `NonConsumable` catalog product — `FakePurchaseService`'s semantics. `Success` when one or more were
  re-applied, `Nothing` when none, `Failed` before init, while another restore runs, on a store error or
  after 30 s without an answer. Always tracks `PurchaseRestored(count)`. Like the fake, an explicit restore
  replays a time-limited entitlement too (it accumulates).
- **Reinstall.** The purchase fetch after init re-applies *permanent* entitlements the store says the
  player owns but this install does not hold (e.g. remove-ads after a reinstall on Android) — silently,
  no analytics, never a time-limited one (it would extend on every launch).
- **Results, never throws.** Only `OperationCanceledException` is rethrown, as the port requires.

### Known gaps

- **Cross-session double grant.** The grant and the store confirmation run in one synchronous step,
  so an order stays unconfirmed across a restart only when the confirmation itself fails (store
  disconnected) and the app is killed before it is redelivered in-session. The next session then grants
  it again. Closing it needs a durable transaction ledger saved with the economy — a persisted user
  model, which is a framework decision, not this module's.
- **No receipt validation.** Orders are trusted as Unity IAP delivers them; no server-side or local
  receipt validation (`CrossPlatformValidator`) is done.
- **No subscriptions.** `ProductKind` has no subscription value; no framework port models one.
- **Single-item orders only.** The port buys one product at a time; an order's product is its cart's
  first item.

## Build preflight

`UnityIapPreflight` (Editor role) feeds `UnityIapPreflightRules` (Core); the framework's one
`ModulePreflightBuildHook` fails the build listing every problem, each prefixed `[com.classicspins.pf-iap-unity]`.
Silent when the module is not in play.

| Code | Targets | Fails when |
|---|---|---|
| `define-without-package` | all | `PF_UNITY_IAP` armed, `com.unity.purchasing` unresolved |
| `package-without-define` | all | the package resolved but the define is not armed (below 5.0.0) |
| `installer-without-define` | all | `UnityIapInstaller` slotted in a build scene, define not armed |
| `catalog-missing` | Android, iOS | no `Resources/Config/products.json` |

Advisory only (logged): the project is not linked to a Unity Cloud project.

## Manual steps (human zone — `pf-module.json` `manualSteps`)

1. **Link the project to a Unity Cloud project** (Edit ▸ Project Settings ▸ Services) — the module
   initializes Unity Gaming Services before it connects the store. An unlinked project still connects,
   with a `Unity Gaming Services did not initialize` warning at boot.
2. **Type every product** in `Resources/Config/products.json` (`"type": "Consumable" | "NonConsumable"`).
3. **App Store Connect → In-App Purchases:** one product per catalog id (Product ID = the id, verbatim),
   matching type (Consumable / Non-Consumable), price tier, localization; Paid Applications agreement
   signed; test with a Sandbox account.
4. **Google Play Console → Monetize ▸ In-app products:** one product per catalog id (Product ID = the
   id, verbatim), activated; upload a build to a testing track (Unity IAP adds the `BILLING`
   permission) and add license testers.
5. **Slot the installer:** `Framework/Modules/Create Installer Assets` (or Assets ▸ Create ▸
   PrototypeFramework ▸ IAP ▸ Unity IAP Installer) at `Assets/<Sku>/Installers/UnityIapInstaller.asset`,
   then drag it into the game-installer slot on `RootLifetimeScope` in the Master Scene.
6. **iOS: a Restore Purchases button** calling `IPurchaseService.RestoreAsync` — App Review requires one
   for non-consumables. Android restores on its own after init (the *reinstall* path above).

## Gates

```sh
sh Tools/CI/module-tests.sh            # this module's Tests~/IapUnityHeadlessTests — CI step 5c
Tools/Build/pf-build.sh modules --check # pf-module.json vs the module's code — CI step 3
```

`IapUnityHeadlessTests` compiles `Runtime/Core/**` + `NullUnityIapBridge.cs` against the framework's
engine-free tiers; `Runtime/Sdk/**` is never compiled there (no SDK on a CI runner). The real store —
UGS init, connect, a sandbox purchase, a restore — is provable only on a device in a consumer project.

Distribution: `…?path=/Packages/com.classicspins.pf-iap-unity#iap-unity/v<version>`.
