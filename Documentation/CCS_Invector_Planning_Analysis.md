# CCS Character Controller — Invector planning analysis (pre-removal)

**Date:** 2026-04-10  
**Scope:** Discovery and replacement planning only — no Invector copying into CCS in this phase.  
**Invector root:** `Assets/Invector-3rdPersonController/`  
**CCS baseline (post-cleanup):** `Animations/`, `Art/`, `Scenes/`, `Scripts/`, `Settings/` under `Assets/CCS/CharacterController/` — **no** `Characters/` folder (experimental promoted starter content removed).

---

## 1. Baseline cleanup completed

The following was **removed** to restore a clean CCS baseline:

- Entire folder **`Assets/CCS/CharacterController/Characters/`** (including `CCS_StarterCharacter` prefab, model, materials, textures, physics materials).
- Orphan **`Characters.meta`**.
- Tools scripts **`Tools/phase0b_promote_starter_character.py`**, **`Tools/phase0_invector_baseline_duplicate.py`**, and **`Tools/phase0b_promotion_report.json`** (if present).

**Unchanged (as requested):**

- `Assets/CCS/CharacterController/Animations/` — full CCS animation set, controllers, masks, FBX.
- `Assets/CCS/CharacterController/Art/` — reference/grid/imported materials.
- `Scripts/`, `Settings/`, `Scenes/SCN_CCS_Controller_Demo.unity`, and **`CCS_CharacterController_Package_Inventory.md`** updated to match disk.

---

## 2. Invector editor entry points

### 2.1 Top-level menu structure (Basic Locomotion–related)

| Menu path | Script | Role |
|-----------|--------|------|
| **Invector/Basic Locomotion/Create Basic Controller** | `Basic Locomotion/Scripts/CharacterCreator/Script/Editor/vCreateBasicCharacterEditor.cs` | Opens the **character creator** `EditorWindow` (`GetWindow<vCreateBasicCharacterEditor>()`). |
| **Invector/Basic Locomotion/Resources/New CameraState List Data** | `Basic Locomotion/Scripts/Camera/Editor/vThirdPersonCameraEditor.cs` | Creates `vThirdPersonCameraListData` asset. |
| **Invector/Basic Locomotion/Components/** … | `Basic Locomotion/Scripts/CharacterCreator/Script/Editor/vBasicMenuComponent.cs` | Spawns optional components: Generic Action, Generic Animation, Ladder Action, HitDamageParticle, HeadTrack, FootStep, AudioSurface, Ragdoll Generic Template, etc. |
| **Invector/Basic Locomotion/Actions/** … | Same `vBasicMenuComponent.cs` | Same pattern — menu items that instantiate helper prefabs/components. |
| **GameObject/Invector/Utils/** … | `vBasicMenuComponent.cs` | Scene GameObject utilities (e.g. SimpleTrigger). |
| **Invector/Basic Locomotion/Components/Ragdoll** | `Basic Locomotion/Scripts/Ragdoll/Editor/vRagdollBuilder.cs` | Ragdoll setup tool. |
| **Invector/Basic Locomotion/Components/Culling Fade** | `Basic Locomotion/Scripts/Camera/CullingFadeControl/Editor/vCullingFadeControlEditor.cs` | Culling fade component. |
| **Invector/Welcome Window**, **Invector/Add-Ons** | `Basic Locomotion/Scripts/CharacterController/Editor/vInvectorWelcomeWindow.cs` | Marketing / first-run / add-on packages (not the create flow). |
| **Invector/Help/** …, **Invector/Import ProjectSettings** | `Basic Locomotion/Scripts/CharacterCreator/Script/Editor/vHelperEditor.cs` | Documentation links and optional project settings import. |

Other top-level Invector menus (**Melee**, **Shooter**, **Inventory**, **FSM AI**) are **separate products** inside the same asset folder; they are **not** required to understand Basic Locomotion create flow.

### 2.2 Which script opens “Create Basic Controller”

**`Invector.vCharacterController.vCreateBasicCharacterEditor`** — `[MenuItem("Invector/Basic Locomotion/Create Basic Controller", false, 0)]` → `CreateNewCharacter()` → `GetWindow<vCreateBasicCharacterEditor>()`.

### 2.3 Helpers vs core creator

- **Core creator UI:** `vCreateBasicCharacterEditor.cs` (template + FBX + Create pipeline).
- **Per-component spawners / resource factories:** `vBasicMenuComponent.cs`, `vBasicUtilsMenu.cs`, `vThirdPersonCameraEditor` (CameraState list), `vRagdollBuilder.cs`, etc.
- **Onboarding / not creation:** `vInvectorWelcomeWindow.cs`, `vHelperEditor.cs`.

### 2.4 Lightweight vs coupled

**Worth recreating in CCS (conceptually):**

- Single menu item → small utility window.
- Object fields: **template prefab**, **humanoid FBX/prefab**, optional toggles.
- **Humanoid validation** (Animator humanoid + valid avatar) before Create.
- **Preview** via `Editor.CreateEditor` + `OnInteractivePreviewGUI` (same pattern Unity uses for Model Inspector).

**Heavily Invector-coupled (do not mirror structurally):**

- Hard dependency on **`vThirdPersonController`** on template (“already has controller” warning).
- **`vThirdPersonCamera`** parenting **`Camera.main`** under the Invector camera rig.
- Optional **`vGameController`** spawn — demo scaffolding, not locomotion core.

---

## 3. Invector create window breakdown

**Source:** `vCreateBasicCharacterEditor.cs` (full flow in `OnGUI`, `CanCreate`, `Create`, `DrawHumanoidPreview`).

### 3.1 Fields and validation

- **Template** (`GameObject`) — user assigns; expected to contain **`3D Model`** child (or one is created empty).
- **FBX Model** (`GameObject`) — typically the imported humanoid asset selection; can also be scene object.
- **Add GameController** (`bool`) — if enabled, ensures a `vGameController` exists in the scene (creates `vGameController_Example` if missing).

**Validation chain (blocks Create until fixed):**

1. Object assigned.
2. **`Animator`** present on `charObj`.
3. **`animator.isHuman == true`**.
4. **`animator.avatar.isValid == true`**.
5. **`vThirdPersonController` must NOT already exist** on `charObj` (warning + cannot create).

**Preview:** Only when `CanCreate()` is true — calls `DrawHumanoidPreview()` using cached `Editor humanoidpreview` from `Editor.CreateEditor(charObj)`.

### 3.2 Preview model

- Uses Unity’s **inspector preview** for the selected object: `humanoidpreview.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(100, 400), "window")`.
- Not a custom PreviewRenderUtility scene — it’s the **default Model Inspector–style** interactive preview.

### 3.3 Data required to create a character

Minimum **conceptual** inputs:

1. Humanoid **source** with valid **Avatar** (on an `Animator`).
2. A **template prefab** instance with:
   - Root **`Animator`** that will become the **gameplay** animator (receives transferred avatar).
   - Expected hierarchy: **`3D Model`** transform under template root (or created).
3. Optional: spawn **vGameController** for demo input/UI routing.

### 3.4 What CCS should keep

- **Validation**: humanoid + valid avatar before creation.
- **Preview**: lightweight `Editor.CreateEditor` + `OnInteractivePreviewGUI` (or equivalent).
- **Clear separation**: “visual source” vs “controller shell prefab” vs optional “demo game manager.”

### 3.5 What CCS should simplify

- **Do not** require Invector’s `vThirdPersonController` check — CCS should validate against **`CCS_CharacterController`** or a neutral “empty shell” contract.
- **Do not** auto-parent **Main Camera** to Invector’s `vThirdPersonCamera` — CCS already has **`CCS_CameraRig`** + Cinemachine path in `CCS_BasicControllerCreator`.
- **Naming**: drive from **CCS template + character name** fields, not `vBasicController_<modelName>`.

---

## 4. Invector template / prefab breakdown

### 4.1 `vBasicController.prefab` vs `vBasicController_Template.prefab`

| Asset | Approx. YAML blocks | Role |
|--------|---------------------|------|
| **`vBasicController.prefab`** | ~271 top-level objects | **Full demo character**: embedded **VBOT_** skeleton/mesh hierarchy (complete visual), plus controller/camera/UI stack — essentially “drop in scene” sample. |
| **`vBasicController_Template.prefab`** | ~105 | **Gameplay shell** used by the create window: **no** embedded VBOT mesh; expects your FBX to be parented under **`3D Model`**. Smaller, meant to be duplicated per character. |

**Create Basic Controller** instantiates **`template`** (intended: **Template** prefab) and parents the **FBX instance** under **`3D Model`**. It does **not** use the full `vBasicController` prefab as the default workflow (though a user could assign it as “template” in theory).

### 4.2 Which is the gameplay shell?

**`vBasicController_Template`** is the **intended** shell for the tool. **`vBasicController`** is a **complete sample** including visual mesh.

### 4.3 Composition of `vBasicController_Template` (root & children)

**Root `vBasicController_Template` includes (non-exhaustive, from prefab YAML):**

- **Transform**
- **Animator** — `m_Avatar` initially **none**; **controller** set to Invector locomotion controller asset; **Apply Root Motion** on.
- **`vThirdPersonController`** (`vThirdPersonController.cs` — guid `73fbf3aa05f6be24780438449f505aa3`) — **core locomotion** (extends health/stamina, capsule, ground check, etc.).
- **`vThirdPersonInput`** (`5deb9ff5611cb9d4596d397ac57ee8c7`) — **input → controller + camera** wiring.
- **Rigidbody** + **CapsuleCollider** — physical proxy.
- **`vFootStep`** (`19ce6a4d1f67f494f8e871355031f21c`) — **optional** surface/audio footsteps.
- **`vDamageReceiver` or similar** (`9d3efef3ad62cd548b0f85eb11858ed1`) — damage hooks.
- **`vHeadTrack`** (`61a5d2516d5dbbc40b1cfb5cbf17758a`) — **optional** look/IK-style tracking.
- **`vGenericAction`** (`f1660eeab87ecf543be4a3ca42e6e836`) — **optional** action triggers.
- **`vLadderAction`** (`7d2c95dd758dfa4469068df6a1432f05`) — **optional** ladder system.

**Child `Invector Components`:**

- **UI** subtree (Canvas, HUD-style content) — **demo / HUD**, not strictly locomotion.
- **Camera rig** with **`vThirdPersonCamera`** — expects **`CameraStateList`** (`vThirdPersonCameraListData`) for state machine; **core to Invector camera** but **not** the same as Cinemachine.

### 4.4 Absolutely required for *Invector* locomotion (as designed)

On the **template root**:

- **Animator** (with valid humanoid avatar after transfer + locomotion **AnimatorController**).
- **`vThirdPersonController`**
- **`vThirdPersonInput`**
- **Rigidbody** + **CapsuleCollider** (their movement pipeline)
- **`vThirdPersonCamera`** on child rig + assigned **`CameraStateList`** asset (camera uses list in code — see `vThirdPersonCamera.cs`).

### 4.5 Optional add-ons on template (CCS Phase 1 should skip)

- **FootStep**, **HeadTrack**, **Generic Action**, **Ladder**, **Hit/damage** receivers, **UI Canvas** under Invector Components, **vGameController** in scene.

---

## 5. Animator / avatar findings

### 5.1 How Invector uses the selected FBX

In **`Create()`**:

1. **`InstantiateNewCharacter(charObj)`** — if asset, `PrefabUtility.InstantiatePrefab`; if already in scene, uses scene instance.
2. Instantiates **`template`** at same position/rotation.
3. Finds **`3D Model`** under template (or creates empty child).
4. **Parents** the model instance under **`3D Model`**, zero local position/rotation.
5. **Avatar transfer:**
   - `animatorTemplate.avatar = animatorController.avatar;`
   - `animatorTemplate.Rebind();`
   - **`DestroyImmediate(animatorController)`** on the **model** — the **skinning rig’s Animator is removed**; the **template root Animator** becomes the only driver.

### 5.2 Preserve imported Animator vs replace

- **Does not preserve** the FBX’s **Animator** component — it is **destroyed**.
- **Preserves** the **Avatar** reference by copying it to the template’s **Animator**.
- **Keeps** the template’s **AnimatorController** on the root (Invector locomotion controller), not the imported clip controller.

### 5.3 Risks for imported characters

- **Destructive**: Removing the mesh Animator can break workflows that expect **two animators** (e.g. facial/secondary) unless recreated manually.
- **Single-controller assumption**: All locomotion comes from template controller + avatar mask/humanoid rig.
- **Layer/tag**: Sets root tag **Player** and layer **Player** on entire hierarchy — may conflict with project layers.

### 5.4 CCS-safe strategy (recommended)

| Topic | Invector behavior | CCS direction |
|--------|-------------------|----------------|
| **Primary locomotion animator** | Template’s controller + transferred avatar | Use **`CCS_AnimatorSetupUtility`**: pick **one primary** humanoid `Animator`, optional **package idle fallback** only if empty — aligns with your existing Phase 1 docs. |
| **Avatar** | Copy avatar to template root, delete mesh animator | CCS should **assign avatar** on the **chosen locomotion Animator** without blindly destroying all animators — **your utility already avoids nuking secondary animators**. |
| **Root motion** | Template defaults (e.g. `applyRootMotion` on Animator) | Explicit **toggle** per character template; document interaction with **`CCS_CharacterController`** vs Invector Rigidbody motor. |
| **Secondary animators** | Not handled | **Explicit policy**: only primary gets locomotion controller assignment; secondaries untouched (already in `CCS_AnimatorSetupUtility`). |

---

## 6. Camera findings

### 6.1 Core to Invector create flow

After character creation:

- Finds **`Camera.main`** and **`vThirdPersonCamera`** under template.
- If no main camera, creates **MainCamera** with **Camera** + **AudioListener**.
- **Parents** main camera under **`vThirdPersonCamera`** transform with zero local pose.

So the tool **assumes** Invector’s **vThirdPersonCamera** rig exists on the template.

### 6.2 Scripts / assets for “working” Invector camera

- **`vThirdPersonCamera`** (`Invector.vCamera`) — runtime orbit/follow logic.
- **`vThirdPersonCameraListData`** — **CameraStateList**; states named and stored in a **ScriptableObject** list (`tpCameraStates`). Used throughout `vThirdPersonCamera.cs` (e.g. `CameraStateList.tpCameraStates`, default state selection).

### 6.3 Concepts worth carrying to CCS

- **Third-person follow** with **collision / culling** awareness (Invector implements many edge cases).
- **State list** idea (default vs aim vs strafe) — **optional** for CCS Phase 1.

### 6.4 What CCS should replace

- **Do not** depend on **`vThirdPersonCamera`** + **CameraStateList** for CCS core.
- **Use** existing **`CCS_CameraRig`** + **Cinemachine** stack from `CCS_BasicControllerCreator` (already implemented in CCS package).

---

## 7. Optional feature findings (template root)

| Component | Script (guid prefix) | Phase 1 recommendation |
|-----------|----------------------|-------------------------|
| Locomotion + motor | `vThirdPersonController` | **Replace** with **`CCS_CharacterController`** + Unity **`CharacterController`** (your architecture). |
| Input routing | `vThirdPersonInput` | **Replace** with **Input System** + your wiring (already in creator window path). |
| Footsteps | `vFootStep` | **Ignore** → future **optional module** (audio/surface). |
| Head tracking | `vHeadTrack` | **Ignore** → optional module. |
| Generic / Ladder actions | `vGenericAction`, `vLadderAction` | **Ignore** for core; optional modules. |
| Damage | `vDamageReceiver` | **Ignore** unless gameplay package needs it. |
| UI under Invector Components | Canvas / HUD | **Ignore** — demo. |
| vGameController | Scene singleton | **Ignore** for CCS core. |

---

## 8. Recommended CCS architecture (target end state)

Aligned with your stated goal:

### 8.1 Editor

- **CCS → Add Character** (or keep **Create Basic Controller**): single **`EditorWindow`** branded CCS.
- Fields: **CCS controller template prefab** (empty or minimal shell), **visual model / prefab**, **character name**, toggles: **create **`CCS_CameraRig`**, **preserve animator controllers**, **Input asset**, parent under scene group.
- **Preview**: same pattern as Invector (`Editor.CreateEditor` + interactive preview) **or** optional **PreviewRenderUtility** later.

### 8.2 Content

- **`CCS_CharacterRoot` / template prefab** — **only** CCS components + `CharacterController` + `Animator` + references — **no** Invector namespaces.
- **Optional `CCS_VisualOnly_…` prefab** — mesh-only for UI preview or drop-in art tests.

### 8.3 Runtime

- **`CCS_CharacterController`** — movement authority.
- **`CCS_CameraRig`** — Cinemachine-based follow.
- **`CCS_AnimatorSetupUtility`** (editor) — primary animator selection + safe fallback.

### 8.4 First-pass prefab hierarchy (suggested)

```
CCS_PlayerRoot (CharacterController, CCS_CharacterController, Animator, …)
├── CCS_CameraTarget (follow / look targets if needed)
└── CharacterVisuals
    └── ModelOffsetRoot
        └── <Imported model subtree>
```

### 8.5 First-pass runtime components

- **`UnityEngine.CharacterController`**
- **`CCS_CharacterController`**
- **`Animator`** (primary locomotion — single source of truth for locomotion clips)
- **Optional**: `PlayerInput` or your input bridge — as you already wire in creator.

### 8.6 First-pass editor scripts

- **`CCS_CreateBasicControllerWindow`** — UI entry.
- **`CCS_BasicControllerCreator`** — scene instantiation pipeline.
- **`CCS_AnimatorSetupUtility`** — animator policy.
- **`CCS_InputAssetUtility`** — input asset ensure.

### 8.7 Explicitly out of scope for first CCS pass

- Invector **`vThirdPersonCamera`**, **CameraStateList**, **vFootStep**, **HeadTrack**, **Ladder**, **Melee/Shooter** stacks.
- Full parity with Invector **UI/HUD** samples.

---

## 9. Recommended CCS phase order

1. **Finalize CCS template prefab** (no Invector scripts) — hierarchy contract documented (`CharacterVisuals/ModelOffsetRoot`).
2. **Harden create window** — validation + preview + reports (builds on existing window).
3. **Animator + input** — single primary animator, preserve secondaries, package fallback policy.
4. **Camera** — **`CCS_CameraRig`** only; tune follow/collision in Cinemachine.
5. **Optional modules** — footsteps, head look, combat — **separate** asmdef or folders later.
6. **Remove Invector** — only after nothing in CCS menus references Invector types and no scene/prefab depends on it.

---

## 10. Safe delete checklist before removing Invector

### 10.1 CCS replacements that must exist first

- [ ] **Playable character** from **CCS menu only** (no `Invector/.../Create Basic Controller` dependency).
- [ ] **Template prefab** with **only CCS/Unity** components.
- [ ] **Camera** via **`CCS_CameraRig`** + Cinemachine, validated in **`SCN_CCS_Controller_Demo.unity`** (or equivalent).
- [ ] **Input** via package **`CCS_CharacterController_InputActions`** (or project-wide replacement documented).

### 10.2 Current CCS assets that already cover needs

- **`Animations/`** — locomotion controllers (`CCS_Base_locomotion_controller`, etc.), clips, masks, models.
- **`Scripts/Runtime/CCS_CharacterController.cs`**, **`CCS_CameraRig.cs`**
- **`Scripts/Editor/`** — creator window, **`CCS_BasicControllerCreator`**, **`CCS_AnimatorSetupUtility`**, input utility.
- **`Settings/Input/`** — `.inputactions`
- **`Scenes/SCN_CCS_Controller_Demo.unity`** — demo scene path (renamed from `*_Test`).

### 10.3 Still to build before deleting Invector (gaps)

- **Official CCS template prefab** (replacing any experiment that pointed at Invector shells).
- **End-to-end test**: create character from CCS menu → play mode → move/look → no missing scripts.
- **Project-wide grep**: zero references to `Invector` namespace in **your** code and **your** prefabs/scenes.

### 10.4 Safest implementation order

1. Lock **CCS** create pipeline to **CCS template only** (remove ability to assign Invector prefabs as recommended template in docs/UI).
2. Validate **demo scene** uses only CCS stack.
3. Archive **project-specific** dependency list (if any asset outside this package still references Invector).
4. Delete **`Assets/Invector-3rdPersonController/`** only when **compile + playmode** clean.

---

## Answers to highlighted questions

**Minimum Invector create-window UX worth recreating:** Template field + humanoid FBX field + humanoid/avatar validation + **interactive preview** + one **Create** button + optional “spawn demo game manager” **omitted** in CCS.

**Minimum runtime component set for a CCS-created character:** `CharacterController` + `CCS_CharacterController` + primary `Animator` + input; **`CCS_CameraRig`** in scene for camera.

**Core vs extras on Invector template:** **Core:** `vThirdPersonController`, `vThirdPersonInput`, physics proxy, root `Animator` pipeline, `vThirdPersonCamera` + state list. **Extras:** footsteps, head track, ladder, generic actions, HUD UI, vGameController.

**Handling model / animator / avatar / camera / root / optional:** See sections 5–7 and **§8** — CCS should **own** the hierarchy contract, **preserve secondaries**, **Cinemachine** for camera, and treat Invector **only** as a **reference implementation**, not a copy source.

**What must exist before deleting Invector:** **§10** — working CCS-only create path, CCS template prefab, no remaining references, validated demo scene.

---

*End of report.*
