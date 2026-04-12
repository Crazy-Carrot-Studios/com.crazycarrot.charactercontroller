# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
