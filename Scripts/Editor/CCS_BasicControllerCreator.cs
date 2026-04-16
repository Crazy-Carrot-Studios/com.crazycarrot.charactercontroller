using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_BasicControllerCreator
// Purpose: Phase 1 — scene creation for Basic Locomotion (CharacterController + CCS_CharacterController,
//          optional model under ModelOffsetRoot, optional Cinemachine rig). No camera profiles.
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    /// <summary>
    /// Builds a playable baseline character from a template and/or a minimal hierarchy.
    /// </summary>
    internal static class CCS_BasicControllerCreator
    {
        #region Constants

        private const string SceneGroupName = "CCS_BasicControllers";
        private const string ThirdPersonCinemachineChildName = "Cinemachine Third Person Follow Cam";
        private const string RigMainCameraChildName = "Main Camera";

        private const float ThirdPersonOrbitRadius = 4f;
        private const float ThirdPersonFieldOfView = 58f;
        private const float ThirdPersonNearClip = 0.1f;
        private const float ThirdPersonFarClip = 5000f;
        private const float ThirdPersonOrbitTargetOffsetX = 0.18f;
        private const float ThirdPersonVerticalPitchCenterDegrees = 12f;
        private const float ThirdPersonVerticalPitchMinDegrees = -20f;
        private const float ThirdPersonVerticalPitchMaxDegrees = 80f;
        private const float ThirdPersonPositionDamping = 0.22f;
        private const float ThirdPersonRotationDamping = 0.28f;
        private const float ThirdPersonComposerDampingX = 0.32f;
        private const float ThirdPersonComposerDampingY = 0.28f;
        private const int ThirdPersonCinemachinePriority = 20;
        private const float ThirdPersonLookOrbitInputAccelTime = 0.38f;
        private const float ThirdPersonLookOrbitInputDecelTime = 0.38f;

        #endregion

        #region Nested Types

        internal struct CreationSettings
        {
            public GameObject TemplatePrefab;
            public GameObject ModelPrefab;
            public string CharacterName;
            public bool AddCcsControllerComponents;
            public bool PreserveAnimatorControllers;
            public bool CreateCameraRig;
            public bool ParentUnderSceneGroup;
            public bool AutoCreateInputAsset;
        }

        internal struct BasicLocomotionCreateResult
        {
            public bool Success;
            public GameObject PlayerRoot;
            public GameObject CameraRigRoot;
            public bool UsedExistingCameraRig;
            public List<string> Warnings;
        }

        internal struct CreationReport
        {
            public bool Success;
            public GameObject PlayerRoot;
            public GameObject CameraRigRoot;
            public string CharacterNameUsed;
            public string TemplatePathOrNone;
            public string ModelPathOrNone;
            public int AnimatorCount;
            public string PrimaryAnimatorPathOrNone;
            public bool PreservedPrimaryController;
            public bool AssignedFallbackOnPrimary;
            public string FallbackPathOrEmpty;
            public bool CameraRigCreated;
            public string AnimatorSelectionDetail;
            public List<string> Warnings;
        }

        #endregion

        #region Public Methods

        internal static BasicLocomotionCreateResult CreateBasicLocomotionCharacter(
            GameObject templatePrefab,
            GameObject visualPrefab,
            string characterName)
        {
            var result = new BasicLocomotionCreateResult
            {
                Warnings = new List<string>(8),
            };

            if (templatePrefab == null)
            {
                Debug.LogError("[CCS_BasicControllerCreator] CreateBasicLocomotionCharacter: template prefab is missing.");
                return result;
            }

            if (visualPrefab == null)
            {
                Debug.LogError("[CCS_BasicControllerCreator] CreateBasicLocomotionCharacter: visual prefab is missing.");
                return result;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(templatePrefab))
            {
                Debug.LogError("[CCS_BasicControllerCreator] CreateBasicLocomotionCharacter: template must be a prefab asset.");
                return result;
            }

            string trimmedName = string.IsNullOrWhiteSpace(characterName) ? "CCSPlayer" : characterName.Trim();

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("CCS Create Basic Locomotion Character");

            try
            {
                GameObject playerRoot = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
                if (playerRoot == null)
                {
                    Debug.LogError("[CCS_BasicControllerCreator] Failed to instantiate template prefab.");
                    return result;
                }

                Undo.RegisterCreatedObjectUndo(playerRoot, "CCS Basic Locomotion: instantiate template");
                playerRoot.name = GetUniqueRootName(trimmedName);

                Animator shellAnimator = playerRoot.GetComponent<Animator>();
                if (shellAnimator != null)
                {
                    Undo.DestroyObjectImmediate(shellAnimator);
                }

                CCS_BasicLocomotionMinimalControllerAuthoring.EnsureMinimalControllerExists(false, out string minimalEnsureErr);
                if (!string.IsNullOrEmpty(minimalEnsureErr))
                {
                    result.Warnings.Add("AC_CCS_BasicLocomotion_Minimal: " + minimalEnsureErr);
                }

                Transform visualRoot = playerRoot.transform.Find("VisualRoot");
                if (visualRoot == null)
                {
                    Debug.LogError("[CCS_BasicControllerCreator] Template is missing child transform 'VisualRoot'.");
                    Undo.DestroyObjectImmediate(playerRoot);
                    return result;
                }

                Transform follow = playerRoot.transform.Find("CameraTargets/CameraFollowTarget");
                Transform look = playerRoot.transform.Find("CameraTargets/CameraLookTarget");
                if (follow == null || look == null)
                {
                    Debug.LogError("[CCS_BasicControllerCreator] Template is missing CameraTargets/CameraFollowTarget or CameraLookTarget.");
                    Undo.DestroyObjectImmediate(playerRoot);
                    return result;
                }

                if (!TrySwapVisualUnderVisualRoot(visualRoot, visualPrefab, result.Warnings))
                {
                    Undo.DestroyObjectImmediate(playerRoot);
                    return result;
                }

                CCS_AnimatorSetupUtility.TryAssignPackageMinimalLocomotionOnPrimaryVisual(
                    visualRoot,
                    playerRoot.transform,
                    result.Warnings,
                    true);

                EnsurePlayableBasicLocomotionShell(playerRoot, visualRoot, follow, look, result.Warnings);
                WireCharacterLocomotionMinimalSerialized(playerRoot, visualRoot, result.Warnings);
                CCS_CharacterController character = playerRoot.GetComponent<CCS_CharacterController>();

                InputActionAsset inputAssetForMovement =
                    AssetDatabase.LoadAssetAtPath<InputActionAsset>(CCS_InputAssetUtility.ResolvedPackageInputActionsPath);
                if (character != null && inputAssetForMovement != null)
                {
                    InputActionReference moveRefForPlayer = ResolveMoveReference(inputAssetForMovement);
                    InputActionReference sprintRefForPlayer = ResolveSprintReference(inputAssetForMovement);
                    ApplyCharacterInputBindings(character, moveRefForPlayer, sprintRefForPlayer);
                    if (moveRefForPlayer == null)
                    {
                        result.Warnings.Add(
                            "Gameplay/Move could not be resolved on the package input asset; assign Move on CCS_CharacterController.");
                    }

                    if (sprintRefForPlayer == null)
                    {
                        result.Warnings.Add(
                            "Gameplay/Sprint could not be resolved on the package input asset; assign Sprint on CCS_CharacterController.");
                    }
                }
                else if (character == null)
                {
                    result.Warnings.Add(
                        "CCS_CharacterController could not be added; basic locomotion will not run on this player root.");
                }
                else
                {
                    result.Warnings.Add(
                        "Package Input Actions asset missing; Move/Sprint were not wired on CCS_CharacterController.");
                }

                EditorUtility.SetDirty(playerRoot);
                EditorSceneManager.MarkSceneDirty(playerRoot.scene);

                InputActionAsset inputAsset = inputAssetForMovement;
                InputActionReference lookRef = inputAsset != null ? ResolveLookReference(inputAsset) : null;
                if (lookRef == null)
                {
                    result.Warnings.Add("Could not resolve Gameplay/Look; camera orbit input may not wire on a new rig.");
                }

                GameObject rigRoot = null;
                CCS_CameraRig existingRig = FindFirstSceneCameraRig();
                if (existingRig != null)
                {
                    WireExistingCameraRig(existingRig, follow, look, character);
                    result.UsedExistingCameraRig = true;
                    rigRoot = existingRig.gameObject;
                }
                else if (lookRef != null)
                {
                    rigRoot = CreateCameraRigHierarchy(playerRoot, character, lookRef);
                    if (rigRoot == null)
                    {
                        result.Warnings.Add("CCSCameraRig could not be created automatically; add and wire CCS_CameraRig manually.");
                    }
                }
                else
                {
                    result.Warnings.Add("No scene CCS_CameraRig found and Look input could not be resolved; add CCSCameraRig manually.");
                }

                result.PlayerRoot = playerRoot;
                result.CameraRigRoot = rigRoot;
                result.Success = true;
                return result;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        [System.Obsolete("Legacy creation API. Prefer CreateBasicLocomotionCharacter with PF_CCS_BasicController_Template for Phase 1.")]
        internal static CreationReport Create(in CreationSettings settings)
        {
            var report = new CreationReport
            {
                Warnings = new List<string>(8),
                CharacterNameUsed = string.IsNullOrWhiteSpace(settings.CharacterName)
                    ? "CCSPlayer"
                    : settings.CharacterName.Trim(),
                ModelPathOrNone = settings.ModelPrefab != null
                    ? (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(settings.ModelPrefab))
                        ? settings.ModelPrefab.name + " (non-asset)"
                        : AssetDatabase.GetAssetPath(settings.ModelPrefab))
                    : "(none)",
            };

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("CCS Create Basic Controller");

            try
            {
                if (!CCS_InputAssetUtility.EnsurePackageInputAssetExists(
                        settings.AutoCreateInputAsset,
                        false,
                        out InputActionAsset inputAsset)
                    || inputAsset == null)
                {
                    report.TemplatePathOrNone = "(n/a)";
                    report.Warnings.Add("Input Action Asset could not be resolved; assign Gameplay/Move manually.");
                    report.Success = false;
                    return report;
                }

                InputActionReference moveRef = ResolveMoveReference(inputAsset);
                InputActionReference sprintRef = ResolveSprintReference(inputAsset);
                InputActionReference lookRef = ResolveLookReference(inputAsset);
                if (moveRef == null)
                {
                    report.TemplatePathOrNone = "(n/a)";
                    report.Warnings.Add("Gameplay/Move missing on input asset.");
                    report.Success = false;
                    return report;
                }

                if (settings.CreateCameraRig && lookRef == null)
                {
                    report.Warnings.Add("Gameplay/Look missing; camera rig orbit may not be wired.");
                }

                if (sprintRef == null)
                {
                    report.Warnings.Add("Gameplay/Sprint missing on input asset; assign Sprint on CCS_CharacterController.");
                }

                Transform parent = null;
                if (settings.ParentUnderSceneGroup)
                {
                    parent = GetOrCreateSceneGroup().transform;
                }

                GameObject playerRoot = InstantiateOrBuildPlayer(settings, report);
                if (playerRoot == null)
                {
                    report.Success = false;
                    return report;
                }

                playerRoot.name = GetUniqueRootName(report.CharacterNameUsed);
                if (parent != null)
                {
                    Undo.SetTransformParent(playerRoot.transform, parent, "CCS Basic: parent player");
                }

                Animator legacyShellAnimator = playerRoot.GetComponent<Animator>();
                if (legacyShellAnimator != null)
                {
                    Undo.DestroyObjectImmediate(legacyShellAnimator);
                }

                TryAssignPlayerTag(playerRoot, report);

                Transform modelOffset = EnsureModelOffsetRoot(playerRoot.transform);
                GameObject modelInstance = null;
                if (settings.ModelPrefab != null)
                {
                    modelInstance = InstantiateModel(settings.ModelPrefab, modelOffset);
                    if (modelInstance == null)
                    {
                        report.Warnings.Add("Model prefab instantiation failed.");
                    }
                }

                Transform follow;
                Transform look;
                EnsureCameraTargets(playerRoot.transform, out follow, out look);

                Transform locomotionVisualSearchRoot = playerRoot.transform.Find("VisualRoot");
                if (locomotionVisualSearchRoot == null)
                {
                    locomotionVisualSearchRoot = modelOffset;
                }

                CCS_BasicLocomotionMinimalControllerAuthoring.EnsureMinimalControllerExists(false, out string minimalEnsureErr);
                if (!string.IsNullOrEmpty(minimalEnsureErr))
                {
                    report.Warnings.Add("AC_CCS_BasicLocomotion_Minimal: " + minimalEnsureErr);
                }

                CCS_AnimatorSetupUtility.TryAssignPackageMinimalLocomotionOnPrimaryVisual(
                    locomotionVisualSearchRoot,
                    playerRoot.transform,
                    report.Warnings,
                    true);

                Transform visualFacingForShell = playerRoot.transform.Find("VisualRoot");
                if (visualFacingForShell == null)
                {
                    visualFacingForShell = modelOffset;
                }

                EnsurePlayableBasicLocomotionShell(playerRoot, visualFacingForShell, follow, look, report.Warnings);
                WireCharacterLocomotionMinimalSerialized(playerRoot, visualFacingForShell, report.Warnings);

                CCS_CharacterController character = playerRoot.GetComponent<CCS_CharacterController>();
                UnityEngine.CharacterController motor = playerRoot.GetComponent<UnityEngine.CharacterController>();

                if (settings.AddCcsControllerComponents)
                {
                    if (motor == null)
                    {
                        motor = Undo.AddComponent<UnityEngine.CharacterController>(playerRoot);
                        motor.height = 2f;
                        motor.radius = 0.35f;
                        motor.center = new Vector3(0f, 1f, 0f);
                        motor.skinWidth = 0.08f;
                    }

                    if (character == null)
                    {
                        character = Undo.AddComponent<CCS_CharacterController>(playerRoot);
                    }

                    ApplyCharacterBindings(character, motor, follow, look, visualFacingForShell, moveRef, sprintRef);
                }
                else if (character != null && motor != null)
                {
                    ApplyCharacterBindings(character, motor, follow, look, visualFacingForShell, moveRef, sprintRef);
                }

                CCS_AnimatorSetupUtility.PrimaryAnimatorResult animPick =
                    CCS_AnimatorSetupUtility.SelectPrimaryLocomotionAnimator(
                        locomotionVisualSearchRoot,
                        playerRoot.transform);
                report.AnimatorCount = animPick.AnimatorCount;
                report.AnimatorSelectionDetail = animPick.SelectionSummary;

                if (animPick.PrimaryAnimator != null)
                {
                    report.PrimaryAnimatorPathOrNone =
                        TransformHierarchyPathExtensions.GetHierarchyPath(animPick.PrimaryAnimator.transform);
                }

                if (animPick.AnimatorCount > 0)
                {
                    CCS_AnimatorSetupUtility.LocomotionAnimatorApplyResult animApply =
                        CCS_AnimatorSetupUtility.ApplyPrimaryLocomotionRules(
                            animPick.PrimaryAnimator,
                            settings.PreserveAnimatorControllers,
                            preferMinimalLocomotionController: true);
                    report.PreservedPrimaryController = animApply.PreservedExistingOnPrimary;
                    report.AssignedFallbackOnPrimary = animApply.AssignedFallbackController;
                    report.FallbackPathOrEmpty = animApply.FallbackControllerPathOrEmpty;
                }

                EditorUtility.SetDirty(playerRoot);
                EditorSceneManager.MarkSceneDirty(playerRoot.scene);

                GameObject rigRoot = null;
                if (settings.CreateCameraRig && lookRef != null && character != null)
                {
                    rigRoot = CreateCameraRigHierarchy(playerRoot, character, lookRef);
                    report.CameraRigCreated = rigRoot != null;
                    if (rigRoot != null && parent != null)
                    {
                        Undo.SetTransformParent(rigRoot.transform, parent, "CCS Basic: parent camera rig");
                    }
                }
                else if (settings.CreateCameraRig && lookRef == null)
                {
                    report.Warnings.Add("Camera rig skipped: Look action not available.");
                }

                report.PlayerRoot = playerRoot;
                report.CameraRigRoot = rigRoot;
                report.Success = true;
                return report;
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        #endregion

        #region Private Methods

        private static void TryAssignPlayerTag(GameObject playerRoot, CreationReport report)
        {
            if (playerRoot == null)
            {
                return;
            }

            try
            {
                playerRoot.tag = "Player";
            }
            catch
            {
                report.Warnings.Add("Could not assign Player tag (add Player in Tag Manager).");
            }
        }

        private static GameObject GetOrCreateSceneGroup()
        {
            GameObject existing = GameObject.Find(SceneGroupName);
            if (existing != null)
            {
                return existing;
            }

            GameObject created = new GameObject(SceneGroupName);
            Undo.RegisterCreatedObjectUndo(created, "CCS Basic: scene group");
            return created;
        }

        private static GameObject InstantiateOrBuildPlayer(in CreationSettings settings, CreationReport report)
        {
            if (settings.TemplatePrefab != null)
            {
                GameObject instance;
                if (PrefabUtility.IsPartOfPrefabAsset(settings.TemplatePrefab))
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(settings.TemplatePrefab);
                }
                else
                {
                    instance = Object.Instantiate(settings.TemplatePrefab);
                }

                if (instance == null)
                {
                    return null;
                }

                Undo.RegisterCreatedObjectUndo(instance, "CCS Basic: instantiate template");
                report.TemplatePathOrNone = AssetDatabase.GetAssetPath(settings.TemplatePrefab);
                if (string.IsNullOrEmpty(report.TemplatePathOrNone))
                {
                    report.TemplatePathOrNone = settings.TemplatePrefab.name + " (scene instance)";
                }

                return instance;
            }

            report.TemplatePathOrNone = "(none — built minimal hierarchy)";
            return BuildMinimalPlayerHierarchy();
        }

        private static GameObject BuildMinimalPlayerHierarchy()
        {
            GameObject root = new GameObject("CCSPlayer");
            Undo.RegisterCreatedObjectUndo(root, "CCS Basic: create player root");

            GameObject visuals = new GameObject("CharacterVisuals");
            Undo.RegisterCreatedObjectUndo(visuals, "CCS Basic: CharacterVisuals");
            visuals.transform.SetParent(root.transform, false);

            GameObject modelOffset = new GameObject("ModelOffsetRoot");
            Undo.RegisterCreatedObjectUndo(modelOffset, "CCS Basic: ModelOffsetRoot");
            modelOffset.transform.SetParent(visuals.transform, false);

            GameObject cameraTargets = new GameObject("CameraTargets");
            Undo.RegisterCreatedObjectUndo(cameraTargets, "CCS Basic: CameraTargets");
            cameraTargets.transform.SetParent(root.transform, false);

            GameObject follow = new GameObject("CameraFollowTarget");
            Undo.RegisterCreatedObjectUndo(follow, "CCS Basic: CameraFollowTarget");
            follow.transform.SetParent(cameraTargets.transform, false);
            follow.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            GameObject look = new GameObject("CameraLookTarget");
            Undo.RegisterCreatedObjectUndo(look, "CCS Basic: CameraLookTarget");
            look.transform.SetParent(cameraTargets.transform, false);
            look.transform.localPosition = new Vector3(0f, 1.6f, 0.1f);

            UnityEngine.CharacterController motor = Undo.AddComponent<UnityEngine.CharacterController>(root);
            motor.height = 2f;
            motor.radius = 0.35f;
            motor.slopeLimit = 45f;
            motor.stepOffset = 0.3f;
            motor.skinWidth = 0.08f;
            motor.minMoveDistance = 0.001f;
            motor.center = new Vector3(0f, 1f, 0f);

            Undo.AddComponent<CCS_CharacterController>(root);

            return root;
        }

        /// <summary>
        /// Assigns the primary visual <see cref="Animator"/> to <see cref="CCS_CharacterController"/> and enables minimal parameter driving.
        /// </summary>
        private static void WireCharacterLocomotionMinimalSerialized(
            GameObject playerRoot,
            Transform visualSearchRoot,
            List<string> warnings)
        {
            if (playerRoot == null || visualSearchRoot == null)
            {
                return;
            }

            CCS_CharacterController ccs = playerRoot.GetComponent<CCS_CharacterController>();
            if (ccs == null)
            {
                return;
            }

            CCS_AnimatorSetupUtility.PrimaryAnimatorResult pick =
                CCS_AnimatorSetupUtility.SelectPrimaryLocomotionAnimator(visualSearchRoot, playerRoot.transform);
            if (pick.PrimaryAnimator == null)
            {
                warnings?.Add(
                    "Could not resolve primary Animator for locomotionAnimator on CCS_CharacterController. ("
                    + pick.SelectionSummary
                    + ")");
                return;
            }

            SerializedObject so = new SerializedObject(ccs);
            so.FindProperty("locomotionAnimator").objectReferenceValue = pick.PrimaryAnimator;
            so.FindProperty("driveMinimalLocomotionParameterSet").boolValue = true;
            so.FindProperty("driveLocomotionAnimator").boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ccs);
        }

        /// <summary>
        /// Ensures the player root matches the Invector-style contract: motor + CCS_CharacterController on the root,
        /// with camera and visual references wired for basic locomotion.
        /// </summary>
        private static void EnsurePlayableBasicLocomotionShell(
            GameObject playerRoot,
            Transform visualFacingRoot,
            Transform cameraFollow,
            Transform cameraLook,
            List<string> warnings)
        {
            if (playerRoot == null)
            {
                return;
            }

            UnityEngine.CharacterController motor = playerRoot.GetComponent<UnityEngine.CharacterController>();
            if (motor == null)
            {
                motor = Undo.AddComponent<UnityEngine.CharacterController>(playerRoot);
                motor.height = 2f;
                motor.radius = 0.35f;
                motor.slopeLimit = 45f;
                motor.stepOffset = 0.3f;
                motor.skinWidth = 0.08f;
                motor.minMoveDistance = 0.001f;
                motor.center = new Vector3(0f, 1f, 0f);
                warnings.Add(
                    "Added Unity CharacterController on the player root (template had no motor). Matches PF_CCS_BasicController_Template defaults.");
            }

            CCS_CharacterController ccs = playerRoot.GetComponent<CCS_CharacterController>();
            if (ccs == null)
            {
                ccs = Undo.AddComponent<CCS_CharacterController>(playerRoot);
                warnings.Add(
                    "Added CCS_CharacterController on the player root (Invector-style: movement driver lives on the controller shell).");
            }

            Transform visualForSerialized = visualFacingRoot;
            if (visualForSerialized == null)
            {
                visualForSerialized = playerRoot.transform.Find("CharacterVisuals/ModelOffsetRoot");
            }

            SerializedObject so = new SerializedObject(ccs);
            so.FindProperty("characterMotor").objectReferenceValue = motor;
            if (cameraFollow != null)
            {
                so.FindProperty("cameraFollowTarget").objectReferenceValue = cameraFollow;
            }

            if (cameraLook != null)
            {
                so.FindProperty("cameraLookTarget").objectReferenceValue = cameraLook;
            }

            if (visualForSerialized != null)
            {
                so.FindProperty("characterVisualRoot").objectReferenceValue = visualForSerialized;
            }
            else
            {
                warnings.Add(
                    "Could not resolve a visual facing root (VisualRoot or CharacterVisuals/ModelOffsetRoot); assign Character Visual Root on CCS_CharacterController.");
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ccs);
        }

        private static Transform EnsureModelOffsetRoot(Transform playerRoot)
        {
            Transform visuals = playerRoot.Find("CharacterVisuals");
            if (visuals == null)
            {
                GameObject v = new GameObject("CharacterVisuals");
                Undo.RegisterCreatedObjectUndo(v, "CCS Basic: add CharacterVisuals");
                v.transform.SetParent(playerRoot, false);
                visuals = v.transform;
            }

            Transform modelOffset = visuals.Find("ModelOffsetRoot");
            if (modelOffset == null)
            {
                GameObject mo = new GameObject("ModelOffsetRoot");
                Undo.RegisterCreatedObjectUndo(mo, "CCS Basic: add ModelOffsetRoot");
                mo.transform.SetParent(visuals, false);
                modelOffset = mo.transform;
            }

            return modelOffset;
        }

        private static void EnsureCameraTargets(Transform playerRoot, out Transform follow, out Transform look)
        {
            Transform cameraTargets = playerRoot.Find("CameraTargets");
            if (cameraTargets == null)
            {
                GameObject ct = new GameObject("CameraTargets");
                Undo.RegisterCreatedObjectUndo(ct, "CCS Basic: CameraTargets");
                ct.transform.SetParent(playerRoot, false);
                cameraTargets = ct.transform;
            }

            follow = cameraTargets.Find("CameraFollowTarget");
            if (follow == null)
            {
                GameObject f = new GameObject("CameraFollowTarget");
                Undo.RegisterCreatedObjectUndo(f, "CCS Basic: CameraFollowTarget");
                f.transform.SetParent(cameraTargets, false);
                f.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                follow = f.transform;
            }

            look = cameraTargets.Find("CameraLookTarget");
            if (look == null)
            {
                GameObject l = new GameObject("CameraLookTarget");
                Undo.RegisterCreatedObjectUndo(l, "CCS Basic: CameraLookTarget");
                l.transform.SetParent(cameraTargets, false);
                l.transform.localPosition = new Vector3(0f, 1.6f, 0.1f);
                look = l.transform;
            }
        }

        private static GameObject InstantiateModel(GameObject prefab, Transform parent)
        {
            GameObject instance;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            }
            else
            {
                instance = Object.Instantiate(prefab, parent);
                instance.name = prefab.name;
            }

            if (instance == null)
            {
                return null;
            }

            Undo.RegisterCreatedObjectUndo(instance, "CCS Basic: instantiate model");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void ApplyCharacterBindings(
            CCS_CharacterController character,
            UnityEngine.CharacterController motor,
            Transform follow,
            Transform look,
            Transform visualRoot,
            InputActionReference moveRef,
            InputActionReference sprintRef)
        {
            SerializedObject so = new SerializedObject(character);
            so.FindProperty("characterMotor").objectReferenceValue = motor;
            so.FindProperty("cameraFollowTarget").objectReferenceValue = follow;
            so.FindProperty("cameraLookTarget").objectReferenceValue = look;
            so.FindProperty("characterVisualRoot").objectReferenceValue = visualRoot;
            so.FindProperty("moveAction").objectReferenceValue = moveRef;
            so.FindProperty("sprintAction").objectReferenceValue = sprintRef;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(character);
        }

        private static void ApplyCharacterInputBindings(
            CCS_CharacterController character,
            InputActionReference moveRef,
            InputActionReference sprintRef)
        {
            if (character == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(character);
            so.FindProperty("moveAction").objectReferenceValue = moveRef;
            so.FindProperty("sprintAction").objectReferenceValue = sprintRef;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(character);
        }

        private static InputActionReference ResolveMoveReference(InputActionAsset asset)
        {
            InputActionMap map = asset.FindActionMap("Gameplay", false);
            InputAction action = map != null ? map.FindAction("Move", false) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static InputActionReference ResolveSprintReference(InputActionAsset asset)
        {
            InputActionMap map = asset.FindActionMap("Gameplay", false);
            InputAction action = map != null ? map.FindAction("Sprint", false) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static InputActionReference ResolveLookReference(InputActionAsset asset)
        {
            InputActionMap map = asset.FindActionMap("Gameplay", false);
            InputAction action = map != null ? map.FindAction("Look", false) : null;
            return action != null ? InputActionReference.Create(action) : null;
        }

        private static GameObject CreateCameraRigHierarchy(
            GameObject playerRoot,
            CCS_CharacterController character,
            InputActionReference lookReference)
        {
            Transform follow = playerRoot.transform.Find("CameraTargets/CameraFollowTarget");
            Transform look = playerRoot.transform.Find("CameraTargets/CameraLookTarget");
            if (follow == null || look == null)
            {
                return null;
            }

            GameObject rigRoot = new GameObject(GetUniqueRootName("CCSCameraRig"));
            Undo.RegisterCreatedObjectUndo(rigRoot, "CCS Basic: CCSCameraRig");
            CCS_CameraRig cameraRig = Undo.AddComponent<CCS_CameraRig>(rigRoot);

            Camera renderCamera = ResolveOrCreateMainCameraUnderRig(rigRoot.transform);

            GameObject thirdPersonCm = new GameObject(ThirdPersonCinemachineChildName);
            Undo.RegisterCreatedObjectUndo(thirdPersonCm, "CCS Basic: Cinemachine vcam");
            thirdPersonCm.transform.SetParent(rigRoot.transform, false);

            CinemachineCamera cinemachineCamera = Undo.AddComponent<CinemachineCamera>(thirdPersonCm);
            CinemachineOrbitalFollow orbitalFollow = Undo.AddComponent<CinemachineOrbitalFollow>(thirdPersonCm);
            CinemachineRotationComposer rotationComposer = Undo.AddComponent<CinemachineRotationComposer>(thirdPersonCm);

            ConfigureThirdPersonCinemachineCamera(
                cinemachineCamera,
                orbitalFollow,
                rotationComposer,
                follow,
                look);

            ConfigureCinemachineInputAxisController(thirdPersonCm, lookReference);

            ApplyCameraRigBindings(cameraRig, follow, look, renderCamera, cinemachineCamera, character);

            Undo.RecordObject(cameraRig, "CCS Basic: ApplySerializedRigSettings");
            Undo.RecordObject(cinemachineCamera, "CCS Basic: ApplySerializedRigSettings");
            Undo.RecordObject(orbitalFollow, "CCS Basic: ApplySerializedRigSettings");
            Undo.RecordObject(rotationComposer, "CCS Basic: ApplySerializedRigSettings");
            if (renderCamera != null)
            {
                Undo.RecordObject(renderCamera, "CCS Basic: ApplySerializedRigSettings");
            }

            cameraRig.ApplySerializedRigSettings();

            EditorUtility.SetDirty(rigRoot);
            return rigRoot;
        }

        private static void ApplyCameraRigBindings(
            CCS_CameraRig cameraRig,
            Transform followTarget,
            Transform lookTarget,
            Camera mainCameraReference,
            CinemachineCamera virtualCamera,
            CCS_CharacterController playerCharacter)
        {
            SerializedObject serializedObject = new SerializedObject(cameraRig);
            serializedObject.FindProperty("cameraFollowTarget").objectReferenceValue = followTarget;
            serializedObject.FindProperty("cameraLookTarget").objectReferenceValue = lookTarget;
            serializedObject.FindProperty("mainCamera").objectReferenceValue = mainCameraReference;
            serializedObject.FindProperty("cinemachineCamera").objectReferenceValue = virtualCamera;
            serializedObject.FindProperty("playerCharacterController").objectReferenceValue = playerCharacter;
            serializedObject.ApplyModifiedProperties();
        }

        private static Camera ResolveOrCreateMainCameraUnderRig(Transform rigRoot)
        {
            Camera main = Camera.main;
            if (main != null)
            {
                EnsureCinemachineBrain(main);
                EnsureMainCameraTag(main);
                return main;
            }

            GameObject mainCameraObject = new GameObject(RigMainCameraChildName);
            Undo.RegisterCreatedObjectUndo(mainCameraObject, "CCS Basic: Main Camera");
            mainCameraObject.transform.SetParent(rigRoot, false);
            Camera camera = Undo.AddComponent<Camera>(mainCameraObject);
            Undo.AddComponent<CinemachineBrain>(mainCameraObject);
            mainCameraObject.tag = "MainCamera";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 5000f;
            return camera;
        }

        private static void EnsureCinemachineBrain(Camera camera)
        {
            if (camera != null && camera.GetComponent<CinemachineBrain>() == null)
            {
                Undo.AddComponent<CinemachineBrain>(camera.gameObject);
                EditorUtility.SetDirty(camera.gameObject);
            }
        }

        private static void EnsureMainCameraTag(Camera camera)
        {
            if (camera != null && !camera.CompareTag("MainCamera"))
            {
                try
                {
                    camera.gameObject.tag = "MainCamera";
                }
                catch
                {
                    // Tag may not exist in project.
                }
            }
        }

        private static void ConfigureThirdPersonCinemachineCamera(
            CinemachineCamera cinemachineCamera,
            CinemachineOrbitalFollow orbitalFollow,
            CinemachineRotationComposer rotationComposer,
            Transform followTarget,
            Transform lookTarget)
        {
            CameraTarget target = cinemachineCamera.Target;
            target.TrackingTarget = followTarget;
            target.LookAtTarget = lookTarget;
            target.CustomLookAtTarget = lookTarget != null;
            cinemachineCamera.Target = target;

            cinemachineCamera.Priority = ThirdPersonCinemachinePriority;

            LensSettings lens = cinemachineCamera.Lens;
            lens.FieldOfView = ThirdPersonFieldOfView;
            lens.NearClipPlane = ThirdPersonNearClip;
            lens.FarClipPlane = ThirdPersonFarClip;
            cinemachineCamera.Lens = lens;

            orbitalFollow.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbitalFollow.Radius = ThirdPersonOrbitRadius;
            orbitalFollow.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.TrackingTarget;
            orbitalFollow.TargetOffset = new Vector3(ThirdPersonOrbitTargetOffsetX, 0f, 0f);

            TrackerSettings tracker = orbitalFollow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTargetWithWorldUp;
            tracker.PositionDamping = new Vector3(
                ThirdPersonPositionDamping,
                ThirdPersonPositionDamping,
                ThirdPersonPositionDamping);
            tracker.RotationDamping = new Vector3(
                ThirdPersonRotationDamping,
                ThirdPersonRotationDamping,
                ThirdPersonRotationDamping);
            orbitalFollow.TrackerSettings = tracker;

            InputAxis vertical = orbitalFollow.VerticalAxis;
            vertical.Range = new Vector2(ThirdPersonVerticalPitchMinDegrees, ThirdPersonVerticalPitchMaxDegrees);
            vertical.Wrap = false;
            vertical.Center = Mathf.Clamp(
                ThirdPersonVerticalPitchCenterDegrees,
                vertical.Range.x + 0.01f,
                vertical.Range.y - 0.01f);
            vertical.Value = vertical.Center;
            orbitalFollow.VerticalAxis = vertical;

            InputAxis horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Wrap = true;
            orbitalFollow.HorizontalAxis = horizontal;

            rotationComposer.CenterOnActivate = true;
            rotationComposer.TargetOffset = Vector3.zero;
            rotationComposer.Damping = new Vector2(ThirdPersonComposerDampingX, ThirdPersonComposerDampingY);
        }

        private static void ConfigureCinemachineInputAxisController(
            GameObject thirdPersonVcamObject,
            InputActionReference lookActionReference)
        {
            if (lookActionReference == null || lookActionReference.action == null)
            {
                return;
            }

            CinemachineInputAxisController axisController =
                thirdPersonVcamObject.GetComponent<CinemachineInputAxisController>();
            if (axisController == null)
            {
                axisController = Undo.AddComponent<CinemachineInputAxisController>(thirdPersonVcamObject);
            }

            axisController.ScanRecursively = false;
            axisController.SuppressInputWhileBlending = true;
#if CINEMACHINE_UNITY_INPUTSYSTEM
            axisController.AutoEnableInputs = true;
#endif
            axisController.SynchronizeControllers();

            Undo.RecordObject(axisController, "CCS Basic: wire Cinemachine orbit input");
            for (int i = 0; i < axisController.Controllers.Count; i++)
            {
                InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller controller =
                    axisController.Controllers[i];
                if (controller == null || controller.Input == null)
                {
                    continue;
                }

                if (controller.Name == "Look Orbit X" || controller.Name == "Look Orbit Y")
                {
                    controller.Enabled = true;
                    controller.Input.InputAction = lookActionReference;
                    controller.Input.CancelDeltaTime = true;
                    DefaultInputAxisDriver driver = controller.Driver;
                    driver.AccelTime = ThirdPersonLookOrbitInputAccelTime;
                    driver.DecelTime = ThirdPersonLookOrbitInputDecelTime;
                    controller.Driver = driver;
                }
                else if (controller.Name == "Orbit Scale")
                {
                    controller.Enabled = false;
                }
            }

            EditorUtility.SetDirty(axisController);
        }

        private static string GetUniqueRootName(string baseName)
        {
            int suffix = 0;
            string candidate = baseName;
            while (GameObject.Find(candidate) != null)
            {
                suffix++;
                candidate = baseName + " (" + suffix + ")";
            }

            return candidate;
        }

        private static GameObject TryLoadDefaultStarterVisualPrefab()
        {
            GameObject fromPackage =
                AssetDatabase.LoadAssetAtPath<GameObject>(CCS_InputAssetUtility.ResolvedStarterVisualPrefabPath);
            if (fromPackage != null)
            {
                return fromPackage;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CCS/CharacterController/Characters/CCS_StarterCharacter/Prefabs/PF_CCS_StarterCharacter_Visual.prefab");
        }

        private static bool TrySwapVisualUnderVisualRoot(
            Transform visualRoot,
            GameObject visualPrefab,
            List<string> warnings)
        {
            GameObject defaultVisual = TryLoadDefaultStarterVisualPrefab();
            string selectedGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(visualPrefab));
            if (defaultVisual != null && !string.IsNullOrEmpty(selectedGuid))
            {
                string defaultGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(defaultVisual));
                if (!string.IsNullOrEmpty(defaultGuid) && selectedGuid == defaultGuid)
                {
                    return true;
                }
            }

            while (visualRoot.childCount > 0)
            {
                Transform child = visualRoot.GetChild(0);
                Undo.DestroyObjectImmediate(child.gameObject);
            }

            GameObject instance;
            if (PrefabUtility.IsPartOfPrefabAsset(visualPrefab))
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, visualRoot);
            }
            else
            {
                instance = Object.Instantiate(visualPrefab, visualRoot);
                instance.name = visualPrefab.name;
            }

            if (instance == null)
            {
                warnings.Add("Failed to instantiate the selected visual prefab under VisualRoot.");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "CCS Basic Locomotion: instantiate visual");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return true;
        }

        private static CCS_CameraRig FindFirstSceneCameraRig()
        {
#if UNITY_2022_1_OR_NEWER
            CCS_CameraRig[] rigs = Object.FindObjectsByType<CCS_CameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            CCS_CameraRig[] rigs = Object.FindObjectsOfType<CCS_CameraRig>(true);
#endif
            for (int i = 0; i < rigs.Length; i++)
            {
                CCS_CameraRig rig = rigs[i];
                if (rig == null)
                {
                    continue;
                }

                GameObject go = rig.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                {
                    continue;
                }

                return rig;
            }

            return null;
        }

        private static void WireExistingCameraRig(
            CCS_CameraRig rig,
            Transform followTarget,
            Transform lookTarget,
            CCS_CharacterController player)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = Object.FindFirstObjectByType<Camera>();
            }

            CinemachineCamera vcam = rig.GetComponentInChildren<CinemachineCamera>(true);

            Undo.RecordObject(rig, "CCS Basic Locomotion: wire camera rig");
            SerializedObject serializedObject = new SerializedObject(rig);
            serializedObject.FindProperty("cameraFollowTarget").objectReferenceValue = followTarget;
            serializedObject.FindProperty("cameraLookTarget").objectReferenceValue = lookTarget;
            serializedObject.FindProperty("mainCamera").objectReferenceValue = mainCam;
            serializedObject.FindProperty("cinemachineCamera").objectReferenceValue = vcam;
            serializedObject.FindProperty("playerCharacterController").objectReferenceValue = player;
            serializedObject.ApplyModifiedProperties();

            rig.ApplySerializedRigSettings();
            EditorUtility.SetDirty(rig);

            if (vcam == null)
            {
                Debug.LogWarning(
                    "[CCS_BasicControllerCreator] Scene CCS_CameraRig has no CinemachineCamera child; orbit may not work until the rig is complete.",
                    rig);
            }
        }

        #endregion
    }

    /// <summary>
    /// Small helper for hierarchy paths in reports.
    /// </summary>
    internal static class TransformHierarchyPathExtensions
    {
        internal static string GetHierarchyPath(Transform t)
        {
            if (t == null)
            {
                return string.Empty;
            }

            var stack = new Stack<string>();
            Transform walk = t;
            while (walk != null)
            {
                stack.Push(walk.name);
                walk = walk.parent;
            }

            var sb = new StringBuilder();
            while (stack.Count > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append('/');
                }

                sb.Append(stack.Pop());
            }

            return sb.ToString();
        }
    }
}
