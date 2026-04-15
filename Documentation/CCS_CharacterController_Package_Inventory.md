# CCS CharacterController — Package Inventory

This document summarizes **non-meta** assets under `Assets/CCS/CharacterController` and lists key files. Full clip names follow the pattern `ANIM_*.anim` under `Animations/Clips/<Category>/`.

**Locked execution charter (Base Locomotion Phase 1):** see [`CCS_BaseLocomotion_InvectorParity_ActionPlan.md`](./CCS_BaseLocomotion_InvectorParity_ActionPlan.md).

**Generated:** 2026-04-15  
**Source:** Live disk scan (PowerShell `Get-ChildItem -Recurse`) of `Assets/CCS/CharacterController`

## Summary

| Metric | Count |
|--------|------:|
| Total files on disk (including `.meta`) | 458 |
| Asset files (excluding `.meta`) | 207 |
| `.meta` sidecar files | 251 |

## Non-meta files by extension

| Extension | Count | Notes |
|-----------|------:|-------|
| `.anim` | 153 | Locomotion, survival, ladder, crouch, jump, turn-on-spot, etc. |
| `.tif` | 11 | Mostly starter-character textures |
| `.png` | 1 | `TEX_CCS_StarterCharacter_Body_AlbedoTransparency.png` |
| `.mat` | 9 | Starter character + Art reference/grid/imported |
| `.cs` | 7 | Runtime + Editor scripts |
| `.controller` | 6 | Locomotion + survival animsets + idle/base variants |
| `.fbx` | 6 | Animation rigs + `MODEL_CCS_StarterCharacter.fbx` |
| `.mask` | 5 | Layer masks for animator |
| `.prefab` | 2 | Basic controller template + starter visual |
| `.asmdef` | 2 | Runtime + Editor assemblies |
| `.md` | 3 | Inventory, Invector planning analysis, Base Locomotion execution charter |
| `.inputactions` | 1 | Package input asset |
| `.unity` | 1 | Demo scene |

## Top-level layout

| Path | Role |
|------|------|
| `Animations/` | Clips, animator controllers, avatar masks, source animation FBX |
| `Art/` | Reference / scene / imported materials |
| `Characters/CCS_StarterCharacter/` | Humanoid model, materials, textures, visual prefab |
| `Prefabs/` | `PF_CCS_BasicController_Template` (controller shell) |
| `Scenes/` | `SCN_CCS_Controller_Demo` |
| `Scripts/` | `Runtime/` + `Editor/` C# and asmdefs |
| `Settings/` | `Input/CCS_CharacterController_InputActions.inputactions` |
| `CCS_CharacterController_Package_Inventory.md` | This file (package root) |
| `CCS_Invector_Planning_Analysis.md` | Planning notes (package root) |

## Scripts (`Scripts/`)

**Runtime**

- `Scripts/Runtime/CCS.CharacterController.Runtime.asmdef`
- `Scripts/Runtime/CCS_CharacterController.cs` — CharacterController locomotion, optional **AC_CCS_Locomotion_Base** animator parameter drive (`InputMagnitude`, `IsGrounded`, `IsSprinting`, …).
- `Scripts/Runtime/CCS_CameraRig.cs` — Third-person camera rig wiring for Cinemachine.

**Editor**

- `Scripts/Editor/CCS.CharacterController.Editor.asmdef`
- `Scripts/Editor/CCS_CreateBasicControllerWindow.cs` — **CCS → Character Controller → Basic Locomotion → Create Basic Controller** window.
- `Scripts/Editor/CCS_BasicControllerCreator.cs` — Instantiate template, visual swap, camera rig, input wiring, optional **AC_CCS_Locomotion_Base** on primary visual Animator.
- `Scripts/Editor/CCS_BasicControllerTemplateAuthoring.cs` — **CCS → Character Controller → Authoring → Build PF_CCS_BasicController_Template**.
- `Scripts/Editor/CCS_AnimatorSetupUtility.cs` — Primary Animator selection; locomotion / idle controller helpers.
- `Scripts/Editor/CCS_InputAssetUtility.cs` — Resolves embedded vs UPM paths; input asset + **CCS_Idle_Controller** + **AC_CCS_Locomotion_Base** loaders.

## Prefabs (`Prefabs/`)

- `Prefabs/PF_CCS_BasicController_Template.prefab` — Shell: `UnityEngine.CharacterController`, `CCS_CharacterController`, `VisualRoot`, `CameraTargets`, nested `PF_CCS_StarterCharacter_Visual` (no root Animator; locomotion Animator + controller on the visual).

## Characters (`Characters/CCS_StarterCharacter/`)

- `Models/MODEL_CCS_StarterCharacter.fbx`
- `Prefabs/PF_CCS_StarterCharacter_Visual.prefab`
- `Materials/MAT_CCS_StarterCharacter_Arms.mat`, `…_Body.mat`, `…_Legs.mat`
- `Textures/` — arms/legs `.tif` sets; body albedo **`TEX_CCS_StarterCharacter_Body_AlbedoTransparency.png`** (+ body `.tif` maps: MetallicSmoothness, Normal, RGB)

## Art (`Art/`)

- `Art/Materials/Imported/V-bot-texture.mat`
- `Art/Materials/Reference/` — `MAT_Ref_Blue`, `Magenta`, `Red`, `Yellow`
- `Art/Materials/Scene/MAT_Grid_Ground_LightGreen.mat`

## Scenes & settings

- `Scenes/SCN_CCS_Controller_Demo.unity`
- `Settings/Input/CCS_CharacterController_InputActions.inputactions`

## Animations (`Animations/`)

### Clips (`Animations/Clips/`)

| Subfolder | `.anim` count |
|-----------|--------------:|
| Crouch | 8 |
| JumpFallLand | 9 |
| Ladder | 22 |
| Locomotion | 20 |
| SurvivalBonfire | 29 |
| SurvivalCrafting | 9 |
| SurvivalFarming | 4 |
| SurvivalFishing | 4 |
| SurvivalInteractions | 11 |
| SurvivalKnife | 4 |
| SurvivalMining | 6 |
| SurvivalPoses | 10 |
| SurvivalRepair | 5 |
| SurvivalWoodcut | 6 |
| TurnOnSpot | 6 |
| **Total** | **153** |

Naming: `ANIM_CCS_Basic_*`, `ANIM_CCS_Survival_*`, `ANIM_CCS_Ladder_*`, `ANIM_CCS_vTurnOnSpotAnimations_*`, etc., under the folders above.

### Controllers (`Animations/Controllers/`)

- `AC_CCS_Locomotion_Base.controller` — Large Invector-style graph; **reference / legacy** — Phase 1 shipped default is **`AC_CCS_BasicLocomotion_Minimal`** (see charter; asset added when Phase 3 lands).
- `AC_CCS_SurvivalAnimset_ChopTree.controller`
- `AC_CCS_SurvivalAnimset_CookingFire.controller`
- `AC_CCS_SurvivalAnimset_Farming.controller`
- `CCS_Base_locomotion_controller.controller`
- `CCS_Idle_Controller.controller`

### Masks (`Animations/Masks/`)

- `MASK_CCS_FullBody.mask`, `MASK_CCS_LeftArm.mask`, `MASK_CCS_RightArm.mask`, `MASK_CCS_UnderBody.mask`, `MASK_CCS_UpperBody.mask`

### Models (`Animations/Models/`)

- `MODEL_CCS_Basic_Actions.fbx`
- `MODEL_CCS_Basic_FreeMovement.fbx`
- `MODEL_CCS_Basic_StrafeMoveset.fbx`
- `MODEL_CCS_vBot@lowpoly.fbx`
- `MODEL_CCS_vTurnOnSpotAnimations.fbx`

## Package root documentation

- `CCS_CharacterController_Package_Inventory.md` — This inventory.
- `CCS_BaseLocomotion_InvectorParity_ActionPlan.md` — **Locked** Phase 1 execution charter (template, animator ownership, minimal controller path, fresh-project gate).
- `CCS_Invector_Planning_Analysis.md` — Historical / planning reference for Invector parity work.

## Notes

- **Regenerate counts:** from repo root, run a recursive file count on `Assets/CCS/CharacterController` if this file drifts after large imports.
- **Animator + movement (Phase 1 per charter):** primary locomotion **`Animator`** on the **visual**; shipped default graph targets **`AC_CCS_BasicLocomotion_Minimal`** (see charter). **`AC_CCS_Locomotion_Base`** remains a large **reference** asset in `Animations/Controllers/`, not the Phase 1 default.
- **Template rebuild:** `CCS → Character Controller → Authoring → Build PF_CCS_BasicController_Template` refreshes the shell prefab after hierarchy or input changes.
