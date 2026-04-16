# CCS Character Controller

**Package:** `com.crazycarrot.charactercontroller`  
**Version:** `0.3.0` (`package.json`)  
**Phase 1 baseline:** **Unity `CharacterController`** + **`CCS_CharacterController`** — camera-relative **walk** and **sprint** (hold Sprint while moving), gravity, smooth yaw on **`VisualRoot`**. **Cinemachine 3** orbit via **`CCS_CameraRig`** (serialized fields only; no camera profiles). Locomotion Mecanim uses **`AC_CCS_BasicLocomotion_Minimal`** (1D blend on **InputMagnitude**) on the **visual** Animator; **`driveMinimalLocomotionParameterSet`** drives **InputMagnitude**, **IsGrounded**, **IsSprinting**. Starter content: **`PF_CCS_BasicController_Template`**, **`PF_CCS_StarterCharacter_Visual`**, demo scene **`Scenes/SCN_CCS_Controller_Demo.unity`**. The legacy **Create Character** wizard remains; **Create Basic Controller** is the Invector-style shell flow. Not feature-complete; not combat-ready.

## Hub integration (CCS Hub)

If you use **`com.crazycarrot.hub`**, pin this package to a tag that matches your manifest (for example **`v0.3.0`** after you tag the release). Hub typically bootstraps into **`Assets/CCS/CharacterController`** and removes the UPM entry; ensure your Hub copy of this package matches the version you expect.

## Install (Unity Package Manager — Git URL)

`Packages/manifest.json` → `dependencies`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Optional pin (use a [release tag](https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/tags) when you publish one, e.g. **`v0.3.0`**):

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.3.0"
```

You can also pin to **`#0.3.0`** if your Git tag matches the `package.json` version string exactly.

## Dependencies

- **com.crazycarrot.branding** (Git, see above)
- **Input System** & **Cinemachine 3** — versions in `package.json`

## Authoring (minimal controller)

**CCS → Character Controller → Authoring → Build AC_CCS_BasicLocomotion_Minimal** — creates or rebuilds **`Animations/Controllers/AC_CCS_BasicLocomotion_Minimal.controller`** (required clips under **`Animations/Clips/Locomotion/`**).

**CCS → Character Controller → Authoring → Build PF_CCS_BasicController_Template** — writes **`Prefabs/PF_CCS_BasicController_Template.prefab`** (shell + wired **`CCS_CharacterController`** + starter visual + minimal animator).

## Create flows

- **CCS → Character Controller → Basic Locomotion → Create Basic Controller** — utility window: pick controller template + humanoid visual, validation, then instantiate into the open scene (minimal locomotion + optional **`CCS_CameraRig`**).
- **CCS → Character Controller → Create Character** — legacy wizard: **CCSPlayer**, optional rig, **`CCS_Idle_Controller`** on **`ModelOffsetRoot`** Animators when that path is used.

Tune speeds and camera in the Inspector.

## Test / scenes

Open **`Scenes/SCN_CCS_Controller_Demo.unity`**, or an empty scene with a ground collider, then **Create Basic Controller** (or the wizard) and press Play.

## Repository

https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller

---

**Hub / other templates:** Errors for `Assets/CCS/Materials/TestLocomotion*.meta` come from **materials shipped with the Hub template**, not from this package. Fix those `.meta` files in the template (each `guid:` must be **32 hex characters**).
