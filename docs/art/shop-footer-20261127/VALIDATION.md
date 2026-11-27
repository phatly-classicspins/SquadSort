# Shop footer and Restore fixes

Settings / seamless awning follow-up: live inspection confirmed both Home and mounted Shop Settings buttons were active. HomeUI now hides its Settings button alongside its resource group while a mounted page is active, restoring it on return. Observed exactly one active button in Home, one (Shop-owned) in Shop, and one (Home-owned) after return. Shop Settings open/close succeeded.

Generated one opaque 720×1280 texture `shop-background-pink-awning-720x1280.png`, compositing the original awning at its native 720×91 size and original y106 onto #F4C1C3. Assigned it to the full-screen Shop background and disabled separate awning/garden Images; original source artwork retained. Screenshot Shop-CombinedAwning.png. Gate log Logs/gate/shop-settings-awning-final.log; local suite skips below still apply. The AppLovin modal was dismissed by the user before mutations resumed.

Full-screen pink correction: reverted AwningPinkBackground. Sampled a lit pink pixel from the marked awning edge (source pixel 80,88 = #F4C1C3). Created an opaque 720×1280 PNG at Assets/SqadSort/Content/UI/SquadTheme/Aligned/Native/shop-background-pink-720x1280.png. ShopPinkBackground stretches behind the whole Shop; the old garden Image is disabled while its resource children stay active. The shared footer ground uses this same pink texture for continuity. Awning artwork unchanged. Sprite import explicitly Single / PPU 1. Final observed screenshot: Shop-FullPink-Final.png; Shop-FullPink.png was an intermediate failed import and is not final evidence. Engine-free gate passed (Logs/gate/shop-fullpink-final.log); automated PlayMode and local EditMode/keyed skips below still apply.

Awning follow-up: added AwningPinkBackground, an opaque #F7B2CE non-raycasting Image directly behind the existing awning, with matching RectTransform bounds. Original awning texture unchanged. Observed Shop in Play mode (Shop-AwningPink.png / AwningPink-detail.png); Console errors zero. Engine-free gate passed 481 tests (Logs/gate/awning-pink-final.log). The same local EditMode / automated PlayMode / keyed-leg skips below apply.

Follow-up: deselected Home now matches deselected Shop's icon box (110×100, aspect preserved), caption size, and vertical positions. The selected tab alone remains raised. Both captions remain localized. Observed Home → Shop → Home, zero Console errors; screenshots Home-InactiveSize.png / Shop-InactiveSize.png and inactive-size-comparison.jpg. Engine-free gate passed 481 tests (Logs/gate/footer-inactive-size-final.log).

Authored both the shared Home footer and Shop's standalone fallback in Unity. The deselected Home tab now has an icon and localized caption matching its selected appearance; HomeFooterUI's existing unselectedLabel binding localizes both. Original inherited bare icon disabled to avoid duplicates.

Added an opaque bottom courtyard ground behind the footer frame and tab controls, using a cropped UV region of the existing garden texture. Frame is a child drawn after the ground, so transparency around the rounded footer no longer reveals scrolling offer cards. Normal Home-mounted Shop still uses one active shared footer. Shop's scroll viewport ends above this ground (69 reference pixels more bottom inset) and is clipped; the Restore button remains reachable at the end of the list.

Restore label stretches within symmetric inset margins and uses Midline centering. No purchase or restore action was invoked in validation. Existing font and copy preserved.

Observed Home, Shop top and Shop scrolled to Restore at 1080×1920, then Home navigation. Captures Home.png, Shop.png, Restore.png. Final Console error query: zero. Engine-free gate: 481 passed, zero failed (Logs/gate/shop-footer-restore-final.log).

Implementation helper: Tools/UiMockup/FixShopFooter.cs; the existing art fitting helper retains the added Home label and avoids reenabling the old icon.

Skipped local EditMode suite: no persistent Editor-side code change.

Skipped automated PlayMode suite: prohibited locally; CI runtime gate unverified.

Skipped keyed Slow leg: override has no KEYED_TEST.
