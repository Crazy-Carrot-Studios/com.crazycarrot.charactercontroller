# CCS Base Locomotion — Action Plan (Locked for Phase 1)

**Status:** **Locked decisions** — implementation follows this charter; avoid re-opening scope until the **fresh-project gate** (§8) passes.  
**Updated:** 2026-04-15 (tightened from planning-only draft)  
**Goal:** **CCS Base Locomotion only** — reliable in a **brand-new Unity project**, Invector-**discipline** without Invector **code** or Invector **avatar-transfer** risk.

**Related:** `CCS_Invector_Planning_Analysis.md` (historical comparison; Invector tree may not be present on disk).

---

## 0. Scope lock (non-negotiable)

**In scope for Phase 1**

- Walk, run / sprint, rotate-to-move, grounded gravity  
- Third-person camera follow / orbit  
- Create from **template + visual**, one-click path  
- **Fresh-project safe** (import → Create → Play → no avatar breakage)

**Explicitly out of scope until the gate passes**

- Survival, ladder, weapon/action layers, crouch complexity, extra controller variants  
- Old camera profiles, optional systems, feature expansion  
- Using **`AC_CCS_Locomotion_Base`** as the **shipped default** runtime graph for Phase 1 testing  

**Package assets already aligned with the smaller target**

Shell template prefab, starter visual prefab, Create window, creator, animator/input helpers, camera rig, and (legacy) large locomotion controller assets — **Phase 1 ships a new minimal controller**; large assets remain **reference only** unless proven otherwise.

---

## 1. Invector lesson = discipline, not code

Copy these **five** disciplines, not Invector’s internals:

| # | Discipline |
|---|------------|
| 1 | **One strict template contract** |
| 2 | **One strict humanoid validation path** |
| 3 | **One reliable create flow** |
| 4 | **One animator ownership model** |
| 5 | **One known-good camera + input path** |

---

## 2. Locked decisions (do not re-litigate for Phase 1)

### 2.1 Animator ownership — **LOCKED**

| Rule | Detail |
|------|--------|
| **Primary locomotion Animator** | Lives on the **visual prefab** (Humanoid rig under `VisualRoot`). |
| **Player shell root** | **No** locomotion `Animator` on the shell for Phase 1. |
| **Avatar** | Stays on the **imported visual**; **do not** use Invector’s **avatar-transfer-to-root** pattern for Phase 1. |
| **Do not** destroy the visual’s Animator | Single authority on the rig that owns the Humanoid Avatar. |

**Rationale:** Stated priority is **no avatar breakage in a new project**. Leaving Humanoid Avatar on the visual’s Animator and driving **that** Animator only is the safest default for imported characters. Root = movement shell; visual = **Humanoid Animator + Avatar + Phase-1 runtime controller**.

### 2.2 Animator controller strategy — **LOCKED: C1**

| Rule | Detail |
|------|--------|
| **Shipped default for Phase 1** | New **minimal** `AnimatorController`, e.g. **`AC_CCS_BasicLocomotion_Minimal.controller`**, **CCS-owned** parameters and states only. |
| **`AC_CCS_Locomotion_Base`** | **Not** the shipped default for fresh-project testing until every blocking parameter path is proven. Use it only as **reference** for clips / state ideas. |
| **Graph content (minimal)** | Idle, walk, run, sprint **if needed**; grounded handling **only if** the basic loop truly requires it. **No** survival / action / weapon / ladder / `ActionState`-scale complexity in the Phase 1 default graph. |
| **Parameter set (illustrative)** | Small CCS-owned set, e.g. `InputMagnitude`, `IsGrounded`, `IsSprinting`, and `MoveX` / `MoveY` **only if** the minimal blend tree requires them — final names documented when the asset exists. |

#### 2.2.1 `AC_CCS_BasicLocomotion_Minimal` — implementation size cap (do not overbuild)

Phase 1 controller must stay **brutally small**. Implementers must **not** grow the graph “while we’re here.”

| Include | Exclude (Phase 1) |
|---------|-------------------|
| **Idle** | Jump — **unless** proven unavoidable for the current movement / grounded test |
| **Walk** | Action states, triggers, or Invector-style `ActionState` machinery |
| **Run** | Survival / ladder / weapon / full-body extras |
| **Sprint** — **only** if wiring is already trivial | Extra Animator layers beyond what a **single** Base Layer strictly needs |

**Rules:** No survival, ladder, or weapon logic. No layers beyond the minimum needed for idle → move → run/sprint. If a state does not serve the §8 gate checklist, it does not ship in v1 of this controller.

### 2.3 Template contents — **LOCKED**

**`PF_CCS_BasicController_Template`** must contain **only**:

- `CCSPlayer` root  
- `CharacterController`  
- `CCS_CharacterController`  
- `VisualRoot`  
- `CameraTargets/CameraFollowTarget`  
- `CameraTargets/CameraLookTarget`  

**No** root locomotion `Animator` on the shell for Phase 1. **One** `VisualRoot`; **one** follow target and **one** look target. **Template authoring** and **create flow** must emit this contract **every** time.

### 2.4 Camera baseline — **LOCKED**

| Rule | Detail |
|------|--------|
| **Use** | `CCS_CameraRig` + **Cinemachine** (existing package direction). |
| **Scene behavior** | **Reuse** an existing scene `CCS_CameraRig` if found; **create** one **only** if missing. |
| **Separation** | Camera stays **out of** the player prefab — same as current CCS notes and Invector analysis recommendation for CCS. |

### 2.5 Starter content — **LOCKED**

| Rule | Detail |
|------|--------|
| **Default visual** | Keep **`PF_CCS_StarterCharacter_Visual`** for Phase 1 so new-project testing is **fast and complete** (matches package inventory). |

### 2.6 Active script set — **LOCKED (Phase 1)**

These remain the **only** scripts in **active** use for Base Locomotion; other code paths are **removed, hidden, or disconnected** until the gate passes:

- `CCS_CharacterController`  
- `CCS_CameraRig`  
- `CCS_CreateBasicControllerWindow`  
- `CCS_BasicControllerCreator`  
- `CCS_BasicControllerTemplateAuthoring`  
- `CCS_AnimatorSetupUtility`  
- `CCS_InputAssetUtility`  

**Decommission / disconnect for Phase 1:** anything tied to old camera profiles, root-Animator shell assumptions, alternate creation paths not needed for Base Locomotion, or extra optional toggles.

---

## 3. Objectives (unchanged intent, aligned to locks)

| # | Objective | Measurable outcome |
|---|-----------|-------------------|
| O1 | **Drop-in new project** | Package → Create → Play → move, sprint, camera, **visual animates** with **minimal** controller. |
| O2 | **Animator contract** | **One** primary Humanoid Animator on **visual**; Avatar from import; **minimal** CCS controller assigned. |
| O3 | **Minimal shell** | Template = §2.3 list only; no optional systems on prefab. |
| O4 | **Validation** | Fail-fast before Create (template, visual, Animator, Humanoid, Avatar, input asset, Cinemachine deps). |
| O5 | **No avatar authority fights** | No duplicate locomotion Animators; `applyRootMotion = false` on the visual locomotion Animator. |

---

## 4. How Invector differs (context only)

Invector’s template flow can **transfer Avatar** to a **root** Animator and tear down the model Animator. **CCS Phase 1 explicitly does not do this** (§2.1).

CCS still mirrors Invector’s **validation spirit** (`CanCreate()`-style checks) without copying their implementation.

---

## 5. Risks (unchanged; mitigated by locks)

| Risk | Mitigation (locked) |
|------|----------------------|
| Large `AC_CCS_Locomotion_Base` | **Not** Phase 1 default; **C1** minimal controller. |
| Root vs visual Animator | **Visual-only** locomotion Animator. |
| Drift between authoring and Create | Same template contract from **both** paths. |

---

## 6. Execution phases (implementation order)

### Recommended first batch (bounded work — stop and report)

Execute **Phase 1 → Phase 2 → Phase 3** first, then **pause** for review (diff summary, prefab YAML check, controller screenshot or state list). **Do not** start Phase 4–6 or run the full **§8** fresh-project gate until that checkpoint is accepted. This bounds work to: **dead-path cleanup**, **locked template shell**, **new minimal locomotion controller** — then Phase 4–5 layer **visual Animator wiring** and **Create validation** on top.

### Phase 1 — Freeze and simplify architecture

- Audit every runtime/editor path; **remove or deactivate** anything not required for Base Locomotion only (profiles, alternate flows, root-Animator assumptions, optional toggles).  
- **Exit:** only the script set in §2.6 is required for the user-facing create + play path.

### Phase 2 — Lock the prefab contract

- **`PF_CCS_BasicController_Template`** = §2.3 only; authoring + creator always match.  
- **Exit:** prefab diff / checklist passes; no root locomotion Animator.

### Phase 3 — Build `AC_CCS_BasicLocomotion_Minimal.controller`

- Obey **§2.2.1** (idle, walk, run; sprint only if trivial; no jump unless unavoidable; no action states; minimal layers; no survival/ladder/weapon).  
- Document **exact** parameter list consumed by `CCS_CharacterController` (trim driver code to match).  
- **Exit:** Animator window shows correct transitions in isolation test scene.

### Phase 4 — Visual Animator is authoritative

In **`CCS_BasicControllerCreator`** and **`CCS_AnimatorSetupUtility`** (and related wiring):

- Resolve **primary** Animator on the **selected visual prefab**.  
- **Validate before Create:** Animator exists; **Humanoid**; **Avatar** valid.  
- Assign **`AC_CCS_BasicLocomotion_Minimal`** to **that** Animator; `applyRootMotion = false`.  
- **`CCS_CharacterController`** must reference **that exact** primary visual Animator for locomotion parameter drive.  
- **Never** modify secondary Animators.  
- **Exit:** create flow assigns controller once; no T-pose / duplicate authority in test harness.

### Phase 5 — Tighten Create window

- **Fields only:** Template, Visual Model Template, preview, **Create**.  
- **Validation** (fail with clear messages): missing template/visual; no Animator on visual; not Humanoid; invalid Avatar; missing input asset; Cinemachine dependencies unavailable.  
- **Exit:** `CanCreate`-equivalent behavior without extra Phase-1 options.

### Phase 6 — Camera handling (confirm locked behavior)

- Reuse scene `CCS_CameraRig` if present; else create; wire follow/look from created player.  
- **Exit:** camera never embedded in player template.

---

## 7. Task checklist (maps to phases)

| ID | Task |
|----|------|
| **A** | Audit and remove/deactivate dead paths (profiles, root-Animator assumptions, alternate creation flows, optional toggles). |
| **B** | Lock template to §2.3; align `CCS_BasicControllerTemplateAuthoring` + `CCS_BasicControllerCreator`. |
| **C** | Create **`AC_CCS_BasicLocomotion_Minimal.controller`**; keep **`AC_CCS_Locomotion_Base`** off the default assign path for Phase 1. |
| **D** | Visual Animator authoritative: resolve, validate, assign minimal controller, wire `CCS_CharacterController` reference, respect secondary Animators. |
| **E** | Minimal Create window + fail-fast validation list (§2.1–2.4, Cinemachine). |
| **F** | Camera: reuse rig or create; wire targets; stay out of player prefab. |
| **G** | Fresh-project validation (§8). |

---

## 8. Fresh-project test gate (must pass before expansion)

Do **not** expand features until **all** pass in a **clean** project:

1. Import package  
2. Open **Create Basic Controller**  
3. Defaults auto-filled where applicable  
4. **Create**  
5. Enter **Play**  
6. Move and sprint work  
7. Camera works  
8. Visual animates  
9. No avatar / Animator authority breakage  
10. No compile errors  
11. No missing references after **project reopen**  

*(Optional: repeat on a second Unity LTS / render pipeline row in your support matrix.)*

---

## 9. Deliverables (this pass)

1. Cleaned runtime/editor surface for **Base Locomotion only**  
2. Locked **minimal** template prefab contract (§2.3)  
3. **`AC_CCS_BasicLocomotion_Minimal.controller`** (+ documented parameter contract)  
4. Updated creator flow: **visual Animator** = single locomotion authority + `CCS_CharacterController` reference  
5. **Verified** checklist §8  
6. Updated **`CCS_CharacterController_Package_Inventory.md`** (and optional **`CCS_BaseLocomotion_Install.md`**) after cleanup  

---

## 10. Remaining logistics (not blocking animator locks)

These can be documented in parallel with implementation:

| Topic | Action |
|-------|--------|
| UPM vs embedded | Document **one** primary install story for “new project”; note the other if still supported. |
| Cinemachine / Unity LTS | Publish **minimum** package versions and a small **test matrix**. |

---

## 11. Execution charter (paste for implementer)

We are locking **CCS Base Locomotion only** for fresh-project reliability. This is **cleanup + hardening**, not feature expansion.

**Primary goal:** Package works in a **new** project with Invector-**style** workflow and a **safer** CCS animator contract: strict shell, strict humanoid validation, one-click create, **no** avatar breakage, **no** optional systems yet.

**Hard rules**

1. Primary locomotion **Animator** on the **visual prefab**, not the player root.  
2. **Do not** use Invector **avatar-transfer-to-root** for Phase 1.  
3. **Do not** destroy the visual Animator.  
4. Build and ship **`AC_CCS_BasicLocomotion_Minimal`** (or agreed name) as Phase 1 default — **not** full `AC_CCS_Locomotion_Base`.  
5. Shell prefab **must not** have a root locomotion Animator.  
6. **`CCS_CameraRig`**: scene-based; reuse if found, create if missing; camera logic **not** on player template.

**Active scripts:** `CCS_CharacterController`, `CCS_CameraRig`, `CCS_CreateBasicControllerWindow`, `CCS_BasicControllerCreator`, `CCS_BasicControllerTemplateAuthoring`, `CCS_AnimatorSetupUtility`, `CCS_InputAssetUtility`.

**Do not expand** until §8 passes.

---

## 12. Summary

Decisions that were “open” in the first draft are **closed**: **visual Animator only**, **C1 minimal controller**, **minimal template**, **scene camera rig**, **starter visual** for Phase 1. Next work is **execution** against §6–§9 with §8 as the **merge gate** for any expansion.
