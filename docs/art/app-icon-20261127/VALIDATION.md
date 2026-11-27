# Squad Sort application icon

Approved mockup: three glossy red, blue and green buddies in the pink sorting tray. Source created with the built-in image generator and copied unchanged from exec-257b1e75-316e-474c-aa94-d828bbeb5a08.png.

`squad-sort-icon-v1.png` is assigned through Unity PlayerSettings to the default icon, all 18 Android platform slots and all 19 iOS slots. Android adaptive icons use the opaque full artwork as the background and adaptive-transparent.png as the foreground, preserving a static illustration under the launcher mask. Both adaptive layers are assigned for all six densities.

Unity API readback confirmed the source path and every platform slot. No player was built or installed, so launcher masking/caching on a device is unverified. Existing installed apps require a rebuilt and installed application to receive the icon.

Engine-free validation log: Logs/gate/app-icon-final.log.

Skipped local EditMode suite: no persistent Editor-side code added.

Skipped Play session and automated PlayMode suite: app icon settings do not draw in Game View; local automated PlayMode is prohibited.

Skipped keyed Slow leg: no KEYED_TEST declared by project override.
