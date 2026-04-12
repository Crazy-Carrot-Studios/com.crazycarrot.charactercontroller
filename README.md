# CCS Character Controller

**Package:** `com.crazycarrot.charactercontroller`  
**Version:** `0.1.3-preview.1` (`package.json`)  
**Phase:** **Base Controller** — baseline third-person controller, Humanoid basic locomotion only. Not feature-complete. Not combat-ready.

## Install (Unity Package Manager — Git URL)

`Packages/manifest.json` → `dependencies`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Optional pin:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.1.3-preview.1"
```

## Dependencies

- **com.crazycarrot.branding** (Git, see above)
- **Input System** & **Cinemachine 3** — versions in `package.json`

## Wizard

**CCS → Character Controller → Create Character**

## Repository

https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller

---

**Hub / other templates:** Errors for `Assets/CCS/Materials/TestLocomotion*.meta` come from **materials shipped with the Hub template**, not from this package. Fix those `.meta` files in the template (each `guid:` must be **32 hex characters**). This repo’s `Scripts/Profiles/camera.meta` must also stay valid for the `camera` profile folder to import.
