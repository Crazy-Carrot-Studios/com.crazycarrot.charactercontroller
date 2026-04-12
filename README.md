# CCS Character Controller

**Package:** `com.crazycarrot.charactercontroller`  
**Version:** `0.1.7-preview.1` (`package.json`)  
**Phase:** **Base Controller** — Humanoid-only Phase 1: wizard validates Humanoid before create, single locomotion `Animator` on `ModelOffsetRoot` with Avatar handoff + `Rebind`. Basic locomotion only. Not feature-complete. Not combat-ready.

## Hub integration (CCS Hub)

Optional install in **`com.crazycarrot.hub`** is pinned to **`v0.1.7-preview.1`** (registry + `CCSDependencyManifest.json`). Hub runs **required** **Input System @1.18.0** and **Cinemachine @3.1.6** first, then adds this package, bootstraps into **`Assets/CCS/CharacterController`**, and removes the UPM entry. Console lines prefixed **`[CCS Hub]`** report install version and bootstrap steps.

## Install (Unity Package Manager — Git URL)

`Packages/manifest.json` → `dependencies`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Optional pin:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.1.7-preview.1"
```

## Dependencies

- **com.crazycarrot.branding** (Git, see above)
- **Input System** & **Cinemachine 3** — versions in `package.json`

## Wizard

**CCS → Character Controller → Create Character** — assigns a model with a valid **Humanoid** Avatar (Generic rigs are blocked). See `Documentation/Invector_vs_CCS_ThirdPerson_Portability.md` for architecture notes.

## Repository

https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller

---

**Hub / other templates:** Errors for `Assets/CCS/Materials/TestLocomotion*.meta` come from **materials shipped with the Hub template**, not from this package. Fix those `.meta` files in the template (each `guid:` must be **32 hex characters**). This repo’s `Scripts/Profiles/camera.meta` must also stay valid for the `camera` profile folder to import.
