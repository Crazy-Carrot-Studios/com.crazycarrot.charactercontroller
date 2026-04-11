# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
