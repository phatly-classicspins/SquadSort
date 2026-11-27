# Shared Home / Shop footer

Removed the HomeFooterUI fields and branch that replaced the background per selected tab. Both existing prefab footer images use the same footer-home sprite. The normal mounted Shop continues to use Home's existing footer through UseHostFooter; its fallback footer is hidden. The fitting helper also assigns this same static sprite so a later art fitting run cannot restore the purple variant.

Observed Home → Shop → Home at 1080×1920. Each state reported exactly one active HomeFooterUI, with the same instance ID (-12052) and sprite (footer-home). Selected tab styling changes while the shared background remains pink. Final unobstructed captures: Home-Final.png, Shop-Final.png, footer-comparison.jpg. Initial Home.png/Shop.png were obstructed by an automatic reward popup and are not tab-transition evidence. Final Console error query: zero; Editor idle and compiled.

Engine-free gate: 481 passed, zero failed (Logs/gate/ui-shared-footer-final.log).

Skipped local EditMode suite: no persistent Editor-side code change.

Skipped automated PlayMode suite: prohibited locally; CI runtime gate unverified.

Skipped keyed Slow leg: no KEYED_TEST declared by the project override.

