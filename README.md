# com.crazycarrot.charactercontroller

**Version 0.1.0 — Base Controller / BaseLocomotion baseline only**

Unity package: third-person **CharacterController** locomotion, **Cinemachine 3** orbit camera rig, **Character Setup Wizard**, default **CCS_Default_TP_Follow_CameraProfile**, default **CCS_Base_locomotion_controller** with **CCS_AnimatorDriver** (root motion off).

This is **not** a full controller product, **not** combat-ready, and **not** feature-complete. It is the clean **baseline milestone** for basic locomotion and camera setup.

## Scope (this release)

- Base controller + camera-relative move (Input System)
- Basic third-person Cinemachine rig + default camera profile support
- Setup wizard: player + rig + animator + animator driver wiring
- BaseLocomotion animator parameters driven from gameplay state

## Not included / not promised

- Combat, advanced animation layers, networking, full input feature set beyond baseline maps

## Install

Add to `Packages/manifest.json`:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git",
"com.crazycarrot.branding": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.branding.git"
```

Or pin this baseline tag:

```json
"com.crazycarrot.charactercontroller": "https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller.git#v0.1.0-base-locomotion"
```

## Layout

- **UPM path:** `Packages/com.crazycarrot.charactercontroller/`
- **Embedded dev path:** `Assets/CCS/CharacterController/` is also detected for `AssetDatabase` resolution.

## Wizard

Menu: **CCS → Character Controller → Create Character**

## Requirements

- Unity 6 (`6000.0+` recommended)
- Input System, Cinemachine 3 (see `package.json` dependencies)
- `com.crazycarrot.branding` (wizard UI base)
