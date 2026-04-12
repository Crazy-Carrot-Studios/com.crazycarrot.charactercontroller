# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.10-preview.1] — 2026-04-12

### Fixed

- **Spawn / new-project locomotion pose:** **`UnityEngine.CharacterController`** often reports **`isGrounded == false`** until at least one **`Move`**. Gravity was still applied in the first **`LateUpdate`** while the **Animator** evaluates **before** **`LateUpdate`**, so **`CCS_Base_locomotion_controller`** could enter **Fall** (`!IsGrounded` + **`VerticalVelocity` &lt; threshold**) and the Humanoid looked **curled or sunk**. **`CCS_CharacterController.Start`** now runs a small **downward ground probe**; **`CCS_AnimatorDriver.Start`** pushes parameters once after that (execution order after the character).

## [0.1.9-preview.1] — 2026-04-12

### Fixed

- **`CCS_CameraRig`**: when no **`CCS_CameraProfile`** asset is assigned, **Awake** always creates an in-memory baseline (no more “No camera profile assigned” dead-end when **Apply Profile On Awake** was off in imported setups). **Apply Profile On Awake** still controls whether that baseline is pushed onto the vcam.
- **Character wizard (`CreatePlayerHierarchy`)**: after visual ground alignment, **fits `CharacterController` height, center, and radius** to renderer bounds under **CharacterVisuals**, and **repositions camera follow/look targets** to match capsule height—reduces sinking / clipping and bad grounding on non-default Humanoid scales.

## [0.1.8-preview.1] — 2026-04-12

### Fixed

- **`CCS_CameraProfile.CreateBaselineDefaultsInstance`**: optional **`objectName`** parameter (default `null`) so **`CCS_CameraProfileAssetUtility`** recreation matches the call site; fixes **CS1501** on Hub bootstrap / new projects.

## [0.1.7-preview.1] — 2026-04-12

### Changed

- **`com.unity.inputsystem`**: dependency **`1.18.0`** to match CCS Hub required manifest.
- **README:** Hub integration (optional pin, bootstrap, `[CCS Hub]` console logs).

## [0.1.6-preview.1] — 2026-04-12

### Changed

- **Default follow camera profile path:** `Scripts/Profiles/camera/CCS_Default_TP_Follow_CameraProfile.asset` (lives next to other `camera` profile assets). **`CCS_CharacterControllerPackagePaths`**, **`CCS_CameraProfileAssetUtility`** known-asset list, and comments updated.
- **Repository layout:** `Animations`, `Art`, `Scenes`, `Scripts`, and `Settings` mirrored from the canonical Unity dev tree **`Assets/CCS/CharacterController`** (no repo changes outside the UPM package body + existing root metadata files).

## [0.1.5-preview.1] — 2026-04-10

### Fixed

- **`Scripts/Profiles/camera.meta`**: rewritten as **UTF-8 (no BOM) with LF** line endings so Unity’s YAML parser reads **`guid:`** reliably when Hub (or any tool) copies the package into **`Assets/CCS/CharacterController`** (avoids “GUID cannot be extracted” / ignored folder on Windows CRLF checkouts).

## [0.1.4-preview.1] — 2026-04-10

### Added

- **Phase A (wizard)**: **`TryValidateSourceModelHumanoidForPhase1`** — if a model is assigned, **Create Character** requires at least one **Animator** with **`avatar.isValid`** and **`avatar.isHuman`** before any scene teardown; otherwise a blocking dialog and abort.
- **Phase B (wizard)**: **`SetupHumanoidLocomotionWithAvatarHandoff`** — locomotion **Animator** lives on **`ModelOffsetRoot`** with **Avatar copied** from the best Humanoid source on the model, **`CCS_Base_locomotion_controller`** assigned, **`Rebind()`**, then **`IsolateLocomotionAnimatorOnModelStack`** (no Animator-without-Avatar path on **ModelOffsetRoot**).
- **`Documentation/Invector_vs_CCS_ThirdPerson_Portability.md`**: architecture reference; §4 / §5 / §8 updated for Phase A+B.
- **Phase 1 Report**: **`AvatarHandoffApplied`** / **`handoff (ModelOffsetRoot)`** in the compatibility log when locomotion uses the handoff path.

### Changed

- **Hierarchy-only** create (no model): wizard **no longer** adds an empty **Animator** on **ModelOffsetRoot**.
- Failed Humanoid locomotion setup after model instantiate **rolls back** the new **CCSPlayer** (destroy + error) when controller/Avatar cannot be applied.
- **README**: version **0.1.4-preview.1**, Git pin, short Phase 1 Humanoid / documentation pointer.

## [0.1.3-preview.1] — 2026-04-11

### Fixed

- **`Scripts/Profiles/camera.meta`**: GUID was **33** characters (invalid YAML); corrected to **32** hex so Unity imports the `camera` profile folder (fixes object picker / ignored path when using Hub copy).

### Added

- **`CCS_CharacterController.locomotionAnimator`**: serialized reference set by the wizard so **`LocomotionAnimator`** is not resolved by “first child Animator” (which could mismatch **`CCS_AnimatorDriver`**).
- **`IsolateLocomotionAnimatorOnModelStack`**: disables other **Animator** components under **ModelOffsetRoot** and clears their controllers so only the chosen Humanoid host runs (fixes curl / double-rig fights).

### Changed

- **Phase 1 Report**: clearer lines for chosen path, reuse vs create, locomotion controller target path.
- **README**: shortened Hub-style; note that **TestLocomotion** material `.meta` errors are from the **Hub template**, not this package.

[0.1.10-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.9-preview.1...v0.1.10-preview.1
[0.1.9-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.8-preview.1...v0.1.9-preview.1
[0.1.8-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.7-preview.1...v0.1.8-preview.1
[0.1.7-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.6-preview.1...v0.1.7-preview.1
[0.1.6-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.5-preview.1...v0.1.6-preview.1
[0.1.5-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.4-preview.1...v0.1.5-preview.1
[0.1.4-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.3-preview.1...v0.1.4-preview.1
[0.1.3-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.2-preview.1...v0.1.3-preview.1

## [0.1.2-preview.1] — 2026-04-11

**Preview** — Phase: **Basic Locomotion** (same baseline scope; setup hardening + docs).

### Added

- Editor **camera profile health pass** (script rebind / recreate), **`CCS_CameraRigEditor`** default-profile bind, **runtime in-memory baseline** on `CCS_CameraRig` when no profile asset is assigned and Apply On Awake is on.
- **Animator selection**: tiered pick (Humanoid valid Avatar → any valid Avatar → fallback), enabled preference, hierarchy order; new Animator on **ModelOffsetRoot** if none; **`[CCS] Phase 1 Report`** includes **Animator Path**.

### Changed

- **README** simplified for developers (version, phase, Git install URL).
- Wizard **validation** messages for Avatar (explicit `[CCS]` warnings). Visual ground align uses **ModelOffsetRoot** local Y; no Avatar auto-assignment from imports.

[0.1.2-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.1-preview.1...v0.1.2-preview.1

## [0.1.1-preview.1] — 2026-04-10

**Preview / test build** — same baseline scope as 0.1.0 (basic locomotion only). Improves Humanoid-friendly setup when importing into **fresh projects** (e.g. via Hub).

### Added

- **Phase 1 compatibility report** after **Create Character** (Console): model instance, Animator reuse vs created, Avatar validity / Humanoid, mesh bounds & visual ground alignment, locomotion controller & camera profile resolution, baseline-ready summary.
- **CCS_CameraProfileAssetUtility**: detects default camera profile assets that fail to load as `CCS_CameraProfile` (broken YAML / assembly binding); menu **CCS → Character Controller → Profiles → Recreate Default Follow Camera Profile**.
- **`CCS_CameraProfile.CreateBaselineDefaultsInstance()`** for editor recreation of default profile tuning.

### Changed

- **Character Setup Wizard**: hierarchy **CharacterVisuals / ModelOffsetRoot / model**; **reuses existing `Animator`** in the imported hierarchy when present; adds `Animator` only if none; assigns locomotion controller with **`applyRootMotion = false`**; **Humanoid-only** automatic Avatar assignment from the source import (no generic Avatar fallback).
- **Visual ground alignment**: offsets **ModelOffsetRoot** using renderer bounds vs `CharacterController` bottom (pivot-agnostic).
- **`CCS_CharacterController.LocomotionAnimator`**: resolves **`GetComponentInChildren<Animator>(true)`** under the visual root so child rigs are found.
- **Camera profile `.asset` files**: `m_EditorClassIdentifier` set to **`CCS.CharacterController.Runtime::CCS.CharacterController.CCS_CameraProfile`** so profiles pick correctly after UPM / Hub copy (fixes missing object-picker binding when scripts live in the Runtime asmdef).

### Notes

- **Phase 1 target**: standard **Humanoid** character prefabs. Non-Humanoid or invalid Avatar setups log **warnings/errors**; character may still be created for manual fix-up.
- **Not** combat-ready, **not** a full animation product — baseline locomotion + camera setup only.

[0.1.1-preview.1]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/compare/v0.1.0-base-locomotion...v0.1.1-preview.1

## [0.1.0] — 2026-04-10

### Baseline: Base Controller / BaseLocomotion

**This release is a baseline milestone only** — basic locomotion, not a final or feature-complete controller.

### Added

- Simplified **Character Setup Wizard** (single primary create flow; default assets in-window)
- **CCS_CharacterController** — camera-relative locomotion (CharacterController, Input System)
- **CCS_CameraRig** — Cinemachine 3 third-person rig integration; **CCS_CameraProfile** defaults; vertical orbit init aligned to profile center at startup
- **CCS_AnimatorDriver** — drives **CCS_Base_locomotion_controller** parameters from gameplay; root motion off
- Default **CCS_Default_TP_Follow_CameraProfile** and **CCS_Base_locomotion_controller** assignment paths (UPM + embedded layout resolution)
- Shared **CCS_CharacterController_InputActions** tooling (wizard / package path resolution)

### Notes

- Package id: `com.crazycarrot.charactercontroller`
- Display name: **CCS Character Controller (BaseLocomotion)**
- Depends on **com.crazycarrot.branding** for the wizard window base class

[0.1.0]: https://github.com/Crazy-Carrot-Studios/com.crazycarrot.charactercontroller/tree/main
