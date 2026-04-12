# Invector Basic Locomotion vs CCS Character Controller — Third-Person Portability

**Author:** James Schilz  
**Date:** 2026-04-10  
**Scope:** Technical comparison of how **Invector** (`Assets/Invector-3rdPersonController`) uses the Unity Editor to make a **humanoid model from any project** work with its template, versus how **CCS Character Controller** (`Assets/CCS/CharacterController`) does the same job. Includes failure modes (e.g. “curl up”, bad poses), **Avatar / Animator** behavior, and a **roadmap** so CCS characters and **camera profiles** behave reliably when moved between projects.

---

## 1. Why this document exists

Third-person stacks break across projects for predictable reasons:

- **Humanoid Avatar** must match the **skeleton** driving the mesh; assigning an **Animator Controller** alone does not fix a wrong or missing Avatar.
- **Multiple `Animator` components** on one visual hierarchy can fight (double evaluation, wrong layer, T-pose, or compressed “curl” poses).
- **Root motion** vs **CharacterController**-driven motion must be owned by one system.
- **ScriptableObject profiles** (camera tuning) are portable as *assets*, but **scene references** and **package paths** differ per install (embedded `Assets/` vs UPM `Packages/`).

Invector solves portability primarily through a **template prefab + editor-time Avatar handoff**. CCS solves it through a **wizard-built hierarchy + Avatar handoff to `ModelOffsetRoot` + isolation + `CCS_AnimatorDriver`**. This document breaks both down and records remaining roadmap items.

---

## 2. Source locations (this repo)

| Area | Path |
|------|------|
| Invector character creator (Editor) | `Invector-3rdPersonController/Basic Locomotion/Scripts/CharacterCreator/Script/Editor/vCreateBasicCharacterEditor.cs` |
| Invector third-person core (Runtime) | `.../CharacterController/vThirdPersonController.cs`, `vThirdPersonAnimator.cs`, `vThirdPersonMotor.cs`, `vCharacter.cs` |
| Invector controller Inspector | `.../CharacterController/Editor/vThirdPersonControllerEditor.cs` (debug / scene GUI; not the main “any model” flow) |
| CCS setup wizard | `CCS/CharacterController/Scripts/Editor/CCS_CharacterSetupWizard.cs` |
| CCS motor + driver | `CCS_CharacterController.cs`, `CCS_AnimatorDriver.cs` |
| CCS camera profile + paths | `CCS_CameraProfile.cs`, `CCS_CharacterControllerPackagePaths.cs` |

---

## 3. How Invector makes “any” humanoid work

### 3.1 Entry point: **Create Basic Controller** (Editor window)

- **Menu:** `Invector/Basic Locomotion/Create Basic Controller`
- **Implementation:** `vCreateBasicCharacterEditor` (`EditorWindow`).

### 3.2 Pre-flight validation (editor UX)

Before **Create** is offered, Invector checks:

1. A **target GameObject** (`charObj`) is assigned (typically your FBX / prefab instance in the scene or project).
2. It has an **`Animator`**.
3. `animator.isHuman` is true.
4. `animator.avatar != null` and `animator.avatar.isValid`.

If any check fails, the window shows **HelpBox** messages (Info / Error) and **does not** enable the main **Create** path. This is a major reason Invector *feels* reliable: bad rigs are caught **before** spawning gameplay objects.

### 3.3 The template prefab (fixed gameplay stack)

The user assigns a **`template`** `GameObject` (Invector’s `vBasicController`-style prefab). That template already contains:

- Locomotion / camera / input stack expected by Invector.
- A known child slot for visuals: **`3D Model`** (created if missing).

### 3.4 The critical step: **Avatar transfer + single Animator on the controller**

On **Create**, Invector:

1. **Instantiates** the user’s model (`newCharacter` from `charObj`).
2. **Instantiates** the `template` at the same position/rotation.
3. **Parents** the user model under `template/3D Model`, zeroing local position/rotation.
4. **Copies the Avatar** from the model’s Animator onto the **template’s** Animator:
   - `animatorTemplate.avatar = animatorController.avatar`
   - `animatorTemplate.Rebind()`
5. **`DestroyImmediate(animatorController)`** on the **model** — the mesh hierarchy **no longer has its own Animator**.

**Effect:**

- There is **exactly one** driving **`Animator`** for gameplay, on the **same GameObject** as `vThirdPersonController` / motor / input (depending on prefab layout).
- That Animator uses **Invector’s** `RuntimeAnimatorController` from the template **plus** the **user’s Humanoid Avatar** (their bone mapping).
- Visual meshes are passive children; no second Animator competes with the controller.

This pattern is the industry-standard answer to “use my Mixamo / CC / custom humanoid with your controller”: **controller owns Animator; Avatar comes from the mesh import.**

### 3.5 Camera and scene wiring (same window)

Still inside `Create()`, Invector:

- Sets **Player** tag / layer on the model subtree.
- Finds **`vThirdPersonCamera`** on the template and **parents `Camera.main`** under it (or spawns a main camera).
- Optionally spawns **`vGameController`** if missing.

So **one Editor command** produces a **playable** hierarchy with camera parenting — similar in *intent* to CCS’s “Create Character + optional rig”.

### 3.6 Runtime coupling: motor ↔ Animator

- `vCharacter` exposes `animator` as the single authority.
- `vThirdPersonAnimator` / `vThirdPersonMotor` read/write parameters on **`GetComponent<Animator>()`** (same object as controller stack).
- **Root motion** is an explicit, feature-rich path (`OnAnimatorMove`, `ControlAnimatorRootMotion`, etc.) — different from CCS Phase 1 (motor moves body; animator is cosmetic).

---

## 4. How CCS makes a model work today

### 4.1 Entry point: **Create Character** (wizard)

- **Menu:** `CCS/Character Controller/Create Character`
- **Implementation:** `CCS_CharacterSetupWizard`.

### 4.2 Hierarchy (conceptual)

The wizard builds a **CCSPlayer**-style root with roughly:

- **CharacterController** + **`CCS_CharacterController`**
- **CharacterVisuals** → **`ModelOffsetRoot`** → instantiated **model**

Camera rig (`CCSCameraRig`) is optional but normally created, with Cinemachine 3 wiring and a **`CCS_CameraProfile`**.

### 4.3 Animator strategy: **pre-validate Humanoid, Avatar handoff to `ModelOffsetRoot`, assign controller, isolate**

Aligned with Invector’s “one driving Animator + user Avatar”, adapted to CCS hierarchy:

1. **Before** removing prior CCS objects: **`TryValidateSourceModelHumanoidForPhase1`** ensures the assigned prefab/scene model has at least one **`Animator`** with a **valid Humanoid Avatar** (uses `LoadPrefabContents` / temporary instance as needed).
2. **`TryFindBestHumanoidAvatarSourceAnimator`** picks the best **Humanoid** source under the **instantiated** model (tier 3 only; enabled preference; hierarchy order).
3. **`SetupHumanoidLocomotionWithAvatarHandoff`**: add or reuse **`Animator` on `ModelOffsetRoot`**, set **`avatar`** from the source, assign **`CCS_Base_locomotion_controller`** (or wizard override), **`applyRootMotion = false`**, **`Rebind()`**.
4. **`IsolateLocomotionAnimatorOnModelStack`** disables **all other** `Animator`s under `ModelOffsetRoot` and clears their controllers (source rig Animators are disabled, not destroyed — safer for prefab overrides).

**Hierarchy-only** (no model): no locomotion `Animator` is created (no empty Avatar path).

### 4.4 Runtime coupling: **driver pattern**

- **`CCS_CharacterController`** (order ~100): movement, grounding, camera-relative input, exposes locomotion snapshots.
- **`CCS_AnimatorDriver`** (order ~200): in **`LateUpdate`**, pushes floats/bools/triggers into the **assigned** locomotion Animator (`InputMagnitude`, `IsGrounded`, etc.).
- **`EnsureRootMotionOff`**: explicitly keeps **`applyRootMotion = false`** on the locomotion Animator — consistent with **CharacterController**-driven motion.

This is **clean separation** (motor vs presentation) but **depends** on one correct Animator + valid Humanoid Avatar on that Animator.

### 4.5 Default asset resolution (cross-project)

`CCS_CharacterControllerPackagePaths` resolves:

- `Packages/com.crazycarrot.charactercontroller/...` **or**
- `Assets/CCS/CharacterController/...`

So **embedded vs UPM** is handled for **default** camera profile, **input actions**, and **locomotion controller** — important when copying the package between projects.

---

## 5. Side-by-side comparison

| Topic | Invector (`vCreateBasicCharacterEditor`) | CCS (`CCS_CharacterSetupWizard`) |
|--------|----------------------------------------|----------------------------------|
| **Primary goal** | Drop humanoid into **template** that already matches Invector’s systems | Build **CCS** stack + drop model under **`ModelOffsetRoot`** |
| **Avatar ownership** | **Template Animator** gets **`avatar` from user model**; model Animator **removed** | **`ModelOffsetRoot` Animator** gets **`avatar` from best Humanoid source** on model; sources **disabled** |
| **Animator count on visual** | **One** (on controller / template root) | **One active** on **`ModelOffsetRoot`** after handoff + isolation |
| **Controller assignment** | Template keeps **Invector** controller | Assign **`CCS_Base_locomotion_controller`** to **`ModelOffsetRoot`** Animator |
| **Validation UX** | **Hard gates**: not human / invalid avatar → cannot create | **Hard gates** before teardown: dialog + abort if assigned model fails Humanoid checks |
| **Root motion** | First-class (optional) | **Off** by design (Phase 1); motor owns translation |
| **Camera setup** | Parents **MainCamera** under **vThirdPersonCamera** | **CCSCameraRig** + Cinemachine 3 + **`CCS_CameraProfile`** |
| **Portability lever** | **Prefab template** shipped with asset | **Wizard** + **package path resolution** + **SO defaults** |

---

## 6. Failure modes: “curl up”, T-pose, twisted mesh, no locomotion

### 6.1 Invalid or non-Humanoid rig

- **Symptom:** T-pose, stretched limbs, or idle compression (“curl”).
- **Cause:** `RuntimeAnimatorController` is **Humanoid**-oriented but **`Animator.avatar`** is missing, **Generic**, or **invalid**.
- **Invector:** Blocked before Create.
- **CCS:** **Blocked before Create** when a model is assigned; fix **FBX Rig → Humanoid → Apply** (or assign a valid Humanoid Avatar), then retry.

### 6.2 Multiple Animators (before isolation or on nested prefabs)

- **Symptom:** Odd blending, wrong pose, or one body part frozen.
- **Cause:** More than one enabled Animator affecting overlapping hierarchies, or **facial / props** Animators still fighting.
- **CCS mitigation:** `IsolateLocomotionAnimatorOnModelStack` — **verify** no required Animators were disabled (e.g. face rigs).

### 6.3 **New Animator on `ModelOffsetRoot` with no Avatar** (addressed in wizard)

- **Symptom:** Severe deformation or no animation.
- **Historical cause:** Earlier wizard logic could add an Animator on `ModelOffsetRoot` without an Avatar when the model had no suitable `Animator`.
- **Current behavior:** Phase 1 **blocks** Create when the source model has no valid Humanoid Avatar source; the wizard **always** assigns the locomotion `Animator` on `ModelOffsetRoot` with **Avatar copied** from the best Humanoid source under the model, then **`Rebind()`** and disables competing Animators.

### 6.4 Animator Controller parameter mismatch

- **Symptom:** Stuck idle, wrong transitions.
- **Cause:** `CCS_AnimatorDriver` expects parameters present in **`CCS_Base_locomotion_controller`**. Swapping a different controller without matching parameters breaks the link.
- **Mitigation:** Document parameter contract; add editor validation (hash / name check).

### 6.5 Root motion vs CharacterController

- **Symptom:** Sliding, penetration, or double motion.
- **Cause:** Clips or Animator **root motion** enabled while motor also moves transform.
- **CCS:** Driver forces **`applyRootMotion = false`** — keep it that way for Phase 1.

---

## 7. Camera profiles and other projects

### 7.1 What travels well

- **`CCS_CameraProfile`** is a **ScriptableObject** with plain floats/Vectors — **no hard project paths** inside the asset itself.
- Copying the `.asset` into another project that has the **same CCS runtime scripts** works **if** the **type** (`CCS_CameraProfile`) is loaded from the **same assembly definition** / package id.

### 7.2 What breaks

- **Missing scripts** on the asset if the package is not installed or asmdef GUIDs differ.
- **Wizard / rig default fields** that point to **package-relative paths**: `CCS_CharacterControllerPackagePaths` fixes **defaults**, but **serialized references** on prefabs/scenes must be **reassigned** or go through **“repair”** utilities (e.g. health pass on camera profile — see `CCS_CameraProfileAssetUtility` usage in wizard).

### 7.3 Recommendations

1. **Ship a “baseline” profile** inside the package (already the pattern).
2. For game teams: **duplicate** into `Assets/_Project/CCS/Profiles/` and treat that copy as **source of truth** for game-specific tuning.
3. On package upgrade, use **`CCS_CameraProfile.CreateBaselineDefaultsInstance()`** (runtime factory) or an **Editor migration** to merge new fields without losing custom values.

---

## 8. Roadmap: CCS parity and robustness

Below is a **phased plan** to make CCS feel as **project-agnostic** as Invector for humanoids, while keeping CCS architecture (motor + driver).

### Phase A — Editor validation (quick win) — **implemented**

- [x] If user assigns a model, **Create Character** runs **`TryValidateSourceModelHumanoidForPhase1`**: requires at least one **`Animator`** with **`avatar != null`**, **`avatar.isValid`**, **`avatar.isHuman`**. On failure: **blocking dialog**, error log, **no** scene teardown / no partial player.
- [ ] Optional: **Model preview** using `Editor.CreateEditor` on the source object (same idea as `DrawHumanoidPreview`).

### Phase B — **Avatar handoff** (structural parity with Invector) — **implemented** (always on for Humanoid path)

- [x] Resolve best Humanoid **source** Animator under the instantiated model (same preference tiers as before: Humanoid valid Avatar, enabled, hierarchy order).
- [x] **Single** locomotion **`Animator`** on **`ModelOffsetRoot`**: copy **`source.avatar`**, assign **`CCS_Base_locomotion_controller`** (or wizard override), **`applyRootMotion = false`**, **`Rebind()`**.
- [x] **Disable** competing Animators under `ModelOffsetRoot` (clear their controllers; same as prior isolation policy).
- [x] **`CCS_AnimatorDriver`** references this **one** locomotion Animator.

**Note:** Phase B is **not** a user toggle; it is the default Phase 1 policy whenever a Humanoid model is used. Optional toggle could be added later for edge workflows.

### Phase C — Prefab template (optional product direction)

- [ ] Ship **`CCS_Player_Template.prefab`** (player + visuals empty slot + rig references wired). Wizard **instantiates template** instead of only code-built hierarchy — closer to Invector’s mental model for users.

### Phase D — Diagnostics

- [ ] Editor window: “**CCS Character Health**” — lists Animator count, Avatar validity, controller name, parameter sanity vs `CCS_AnimatorDriver`, duplicate Animators.

### Phase E — Documentation / onboarding

- [ ] Short **user-facing** checklist: Humanoid rig, **one** locomotion Animator policy, where to put custom face Animators, how to copy **camera profile** assets between projects.

---

## 9. Summary

- **Invector’s** portability trick is **editor-time**: validate Humanoid, **instantiate template**, **parent model**, **copy Avatar to template Animator**, **delete model’s Animator**, **wire camera** — **one driving Animator** with **known** controller + **user** skeleton.
- **CCS** uses a **wizard**, **package path resolution**, **pre-create Humanoid validation**, **Avatar handoff to `ModelOffsetRoot`**, **isolation**, and a **driver** with **root motion off** — one locomotion Animator owns **both** the Humanoid Avatar and **`CCS_Base_locomotion_controller`**.
- **Phase A + B** (validation + handoff) close the main behavioral gap with Invector while keeping **CharacterController**-driven motion and **`CCS_AnimatorDriver`**.

---

## 10. References (Unity concepts)

- **Humanoid retargeting:** [Unity Manual — Humanoid Avatars](https://docs.unity3d.com/Manual/class-Avatar.html)
- **Animator component:** single writer per rig for gameplay layers unless you intentionally layer (base body vs face).
- **ScriptableObject portability:** types must exist in target project; assets store YAML + script GUID.

---

*This document is **reference material** for architecture and onboarding. Phase A and Phase B of §8 are implemented in `CCS_CharacterSetupWizard` (see `TryValidateSourceModelHumanoidForPhase1`, `SetupHumanoidLocomotionWithAvatarHandoff`).*
