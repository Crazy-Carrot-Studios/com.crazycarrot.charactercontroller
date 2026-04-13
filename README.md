# CCS Character Controller

**Package:** `com.crazycarrot.charactercontroller`  
**Version:** `0.2.0` (`package.json`)  
**Baseline:** Third-person **Unity `CharacterController`** + **`CCS_CharacterController`** (camera-relative move, visual-root yaw). **Cinemachine 3** orbit camera via **`CCS_CameraRig`** with **Inspector tuning only** (no `CCS_CameraProfile` ScriptableObjects). Optional imported model under **`ModelOffsetRoot`**; the wizard **disables all `Animator` components** under that subtree so the mesh stays **static** while the capsule moves. Not feature-complete; not combat-ready.

## Hub integration (CCS Hub)

If you use **`com.crazycarrot.hub`**, pin this package to a tag that matches your manifest (for example **`v0.2.0`** after release). Hub typically bootstraps into **`Assets/CCS/CharacterController`** and removes the UPM entry; ensure your Hub copy of this package matches the version you expect.

## Install (Unity Package Manager — Git URL)

`Packages/manifest.json` → `dependencies`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Optional pin:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.2.0"
```

## Dependencies

- **com.crazycarrot.branding** (Git, see above)
- **Input System** & **Cinemachine 3** — versions in `package.json`

## Wizard

**CCS → Character Controller → Create Character** — builds **CCSPlayer** (capsule motor + targets) and optional **CCSCameraRig**, resolves **Gameplay/Move** and **Gameplay/Look**, and applies default Cinemachine wiring. Tune the camera on **`CCS_CameraRig`** in the Inspector.

## Test / scenes

There is **no** packaged sample scene in this baseline. Open an empty scene (or your project test scene), add a ground collider, then run **Create Character** and press Play.

## Repository

https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller

---

**Hub / other templates:** Errors for `Assets/CCS/Materials/TestLocomotion*.meta` come from **materials shipped with the Hub template**, not from this package. Fix those `.meta` files in the template (each `guid:` must be **32 hex characters**).
