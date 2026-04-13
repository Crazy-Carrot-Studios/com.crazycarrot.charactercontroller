# CCS Character Controller

**Package:** `com.crazycarrot.charactercontroller`  
**Version:** `0.2.1` (`package.json`)  
**Baseline:** **Unity `CharacterController`** + **`CCS_CharacterController`** — camera-relative **walk** and **sprint** (hold Sprint while moving), gravity, smooth yaw on a visual root. **Cinemachine 3** orbit via **`CCS_CameraRig`** (serialized fields only; no camera profiles). Optional model under **`ModelOffsetRoot`**; wizard **disables `Animator`s** there so the mesh stays **static**. Not feature-complete; not combat-ready.

## Hub integration (CCS Hub)

If you use **`com.crazycarrot.hub`**, pin this package to a tag that matches your manifest (for example **`v0.2.1`** after release). Hub typically bootstraps into **`Assets/CCS/CharacterController`** and removes the UPM entry; ensure your Hub copy of this package matches the version you expect.

## Install (Unity Package Manager — Git URL)

`Packages/manifest.json` → `dependencies`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Optional pin:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.2.1"
```

## Dependencies

- **com.crazycarrot.branding** (Git, see above)
- **Input System** & **Cinemachine 3** — versions in `package.json`

## Wizard

**CCS → Character Controller → Create Character** — builds **CCSPlayer** and optional **CCSCameraRig**, wires **Gameplay/Move**, **Look**, and **Sprint**, and applies default Cinemachine wiring. Tune speeds and camera on the components in the Inspector.

## Test / scenes

There is **no** packaged sample scene in this baseline. Open an empty scene (or your project test scene), add a ground collider, then run **Create Character** and press Play.

## Repository

https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller

---

**Hub / other templates:** Errors for `Assets/CCS/Materials/TestLocomotion*.meta` come from **materials shipped with the Hub template**, not from this package. Fix those `.meta` files in the template (each `guid:` must be **32 hex characters**).
