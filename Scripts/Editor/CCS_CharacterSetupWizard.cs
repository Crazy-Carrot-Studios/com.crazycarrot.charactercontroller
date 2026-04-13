using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using CCS.Editor.CustomInspectors.Branding;
using CCS.CharacterController;

//==============================================================================
// CCS Script Summary
// Name: CCS_CharacterSetupWizard
// Purpose: Model-agnostic third-person setup: build CCSPlayer + optional CCSCameraRig, wire Cinemachine 3,
//          CharacterController locomotion (no Mecanim driver), camera tuning on CCS_CameraRig (serialized fields).
//          Optional visual prefab under ModelOffsetRoot; all Animators under that subtree are disabled for a static mesh.
//          Before creating, removes prior CCS players, rigs, and MainCamera-tagged cameras in loaded scenes.
// Placement: Editor / menu CCS/Character Controller/Create Character.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    public sealed class CCS_CharacterSetupWizard : CCSEditorWindowBase
    {
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

        private GameObject sourceModelPrefab;
        private InputActionAsset inputActionsAsset;
        private bool autoCreatePackageInputAsset = true;
        private bool createCameraRigIfMissing = true;
        private float defaultMoveSpeed = 4.5f;
        private float defaultRotationSmoothTime = 0.12f;
        private float defaultInputDeadZone = 0.08f;
        private bool enableWizardDebugLogs;

        private struct GroundAlignOutcome
        {
            public bool HadRenderableBounds;
            public bool OffsetApplied;
            public float LocalYOffsetApplied;
            public bool SkippedNearZero;
        }

        protected override string WindowTitle => "Character Controller";

        protected override void OnEnable()
        {
            base.OnEnable();

            if (inputActionsAsset == null)
            {
                inputActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    CCS_CharacterControllerPackagePaths.ResolvedPackageInputActionsPath);
            }
        }

        [MenuItem("CCS/Character Controller/Create Character")]
        private static void OpenWindow()
        {
            CCS_CharacterSetupWizard window = GetWindow<CCS_CharacterSetupWizard>();
            window.minSize = new Vector2(420f, 420f);
        }

        protected override void DrawBody()
        {
            CCSEditorStyles.DrawSectionLabel("Source");
            sourceModelPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Model or prefab",
                    "Optional. Instantiated under CharacterVisuals/ModelOffsetRoot. All Animator components under ModelOffsetRoot are disabled so the mesh stays static."),
                sourceModelPrefab,
                typeof(GameObject),
                false);

            CCSEditorStyles.DrawSectionLabel("Input");
            inputActionsAsset = (InputActionAsset)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Input Actions asset",
                    "CCS_CharacterController_InputActions. Leave empty to use the package path and auto-create if needed."),
                inputActionsAsset,
                typeof(InputActionAsset),
                false);
            autoCreatePackageInputAsset = EditorGUILayout.Toggle(
                new GUIContent(
                    "Auto-create package input asset",
                    "When the package asset is missing, generates CCS_CharacterController_InputActions under Settings/Input."),
                autoCreatePackageInputAsset);

            CCSEditorStyles.DrawSectionLabel("Defaults");
            defaultMoveSpeed = EditorGUILayout.FloatField(new GUIContent("Move speed"), defaultMoveSpeed);
            defaultRotationSmoothTime = EditorGUILayout.FloatField(
                new GUIContent("Rotation smooth time"),
                defaultRotationSmoothTime);
            defaultInputDeadZone = EditorGUILayout.FloatField(new GUIContent("Input dead zone"), defaultInputDeadZone);
            createCameraRigIfMissing = EditorGUILayout.Toggle(
                new GUIContent(
                    "Create camera rig",
                    "After clearing any prior CCS rig, creates a new CCSCameraRig and wires it to the new player."),
                createCameraRigIfMissing);
            enableWizardDebugLogs = EditorGUILayout.Toggle(new GUIContent("Wizard debug logs"), enableWizardDebugLogs);

            EditorGUILayout.Space(10f);

            if (GUILayout.Button("Create Character", GUILayout.Height(32)))
            {
                RunCreateCharacter();
            }
        }

        private void RunCreateCharacter()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("CCS Character Setup");

            try
            {
                InputActionAsset workingAsset = ResolveWorkingInputAsset();
                if (workingAsset == null)
                {
                    LogCharacterSetupFailed();
                    return;
                }

                InputActionReference moveReference = ResolveMoveActionReference(workingAsset);
                InputActionReference lookReference = ResolveLookActionReference(workingAsset);
                if (moveReference == null || lookReference == null)
                {
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Could not resolve Move and/or Look from the input asset. Check Gameplay map actions.",
                        this);
                    LogCharacterSetupFailed();
                    return;
                }

                RemoveExistingCcsCharacterSetupFromOpenScenes();

                GameObject playerRoot = CreatePlayerHierarchy(moveReference);
                if (playerRoot == null)
                {
                    LogCharacterSetupFailed();
                    return;
                }

                EditorSceneManager.MarkSceneDirty(playerRoot.scene);

                GameObject cameraRigRoot = null;
                if (!createCameraRigIfMissing)
                {
                    Debug.LogWarning(
                        "[CCS_CharacterSetupWizard] Camera rig creation is disabled; player hierarchy is ready without a new CCSCameraRig.",
                        this);
                }
                else
                {
                    cameraRigRoot = CreateCameraRigHierarchy(playerRoot, lookReference);
                    if (cameraRigRoot == null)
                    {
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Camera rig creation failed; player hierarchy may still be valid. See previous log entries.",
                            playerRoot);
                        LogCharacterSetupFailed();
                    }
                }

                Selection.activeGameObject = playerRoot;
                EditorGUIUtility.PingObject(playerRoot);

                if (SceneView.lastActiveSceneView != null)
                {
                    Bounds frameBounds = new Bounds(playerRoot.transform.position, Vector3.one * 4f);
                    SceneView.lastActiveSceneView.Frame(frameBounds, false);
                }

                if (!createCameraRigIfMissing || cameraRigRoot != null)
                {
                    Debug.Log(
                        "[CCS_CharacterSetupWizard] Character setup finished. CharacterController locomotion + serialized camera tuning on CCS_CameraRig (no packaged Mecanim).",
                        playerRoot);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private void LogCharacterSetupFailed()
        {
            Debug.LogError(
                "[CCS_CharacterSetupWizard] Character setup failed. Check previous errors.",
                this);
        }

        private InputActionAsset ResolveWorkingInputAsset()
        {
            if (inputActionsAsset != null)
            {
                CCS_InputAssetUtility.ValidatePackageInputAsset(inputActionsAsset, enableWizardDebugLogs);
                return inputActionsAsset;
            }

            if (!CCS_InputAssetUtility.EnsurePackageInputAssetExists(
                    autoCreatePackageInputAsset,
                    enableWizardDebugLogs,
                    out InputActionAsset ensured))
            {
                return null;
            }

            inputActionsAsset = ensured;
            return ensured;
        }

        private InputActionReference ResolveMoveActionReference(InputActionAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            InputActionMap gameplayMap = asset.FindActionMap("Gameplay", throwIfNotFound: false);
            InputAction moveAction = gameplayMap != null ? gameplayMap.FindAction("Move", throwIfNotFound: false) : null;
            if (moveAction == null)
            {
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Gameplay/Move was not found on the input asset.",
                    this);
                return null;
            }

            return InputActionReference.Create(moveAction);
        }

        private InputActionReference ResolveLookActionReference(InputActionAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            InputActionMap gameplayMap = asset.FindActionMap("Gameplay", throwIfNotFound: false);
            InputAction lookAction = gameplayMap != null ? gameplayMap.FindAction("Look", throwIfNotFound: false) : null;
            if (lookAction == null)
            {
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Gameplay/Look was not found on the input asset.",
                    this);
                return null;
            }

            return InputActionReference.Create(lookAction);
        }

        private void RemoveExistingCcsCharacterSetupFromOpenScenes()
        {
            HashSet<GameObject> roots = new HashSet<GameObject>();

            CCS_CameraRig[] rigs = UnityEngine.Object.FindObjectsByType<CCS_CameraRig>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i] != null)
                {
                    roots.Add(rigs[i].gameObject);
                }
            }

            CCS_CharacterController[] characters = UnityEngine.Object.FindObjectsByType<CCS_CharacterController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null)
                {
                    roots.Add(characters[i].gameObject);
                }
            }

            GameObject[] mainCameraTagged = GameObject.FindGameObjectsWithTag("MainCamera");
            for (int i = 0; i < mainCameraTagged.Length; i++)
            {
                GameObject go = mainCameraTagged[i];
                if (go != null && go.GetComponent<Camera>() != null)
                {
                    roots.Add(go);
                }
            }

            foreach (GameObject go in roots)
            {
                if (go == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(go);
            }

            if (enableWizardDebugLogs && roots.Count > 0)
            {
                Debug.Log(
                    $"[CCS_CharacterSetupWizard] Removed {roots.Count} prior CCS / MainCamera object(s) before creating a fresh setup.",
                    this);
            }
        }

        private GameObject CreatePlayerHierarchy(InputActionReference moveReference)
        {
            GameObject playerRoot = new GameObject(GetUniqueRootName("CCSPlayer"));
            Undo.RegisterCreatedObjectUndo(playerRoot, "Create CCSPlayer");
            EnsurePlayerTag(playerRoot);

            UnityEngine.CharacterController motor = Undo.AddComponent<UnityEngine.CharacterController>(playerRoot);
            motor.height = 2f;
            motor.radius = 0.35f;
            motor.center = new Vector3(0f, 1f, 0f);
            motor.skinWidth = 0.08f;

            CCS_CharacterController character = Undo.AddComponent<CCS_CharacterController>(playerRoot);

            GameObject visuals = new GameObject("CharacterVisuals");
            Undo.RegisterCreatedObjectUndo(visuals, "Create CharacterVisuals");
            visuals.transform.SetParent(playerRoot.transform, false);

            GameObject modelOffsetRootGo = new GameObject("ModelOffsetRoot");
            Undo.RegisterCreatedObjectUndo(modelOffsetRootGo, "Create ModelOffsetRoot");
            modelOffsetRootGo.transform.SetParent(visuals.transform, false);
            modelOffsetRootGo.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelOffsetRootGo.transform.localScale = Vector3.one;

            GameObject cameraTargets = new GameObject("CameraTargets");
            Undo.RegisterCreatedObjectUndo(cameraTargets, "Create CameraTargets");
            cameraTargets.transform.SetParent(playerRoot.transform, false);

            GameObject follow = new GameObject("CameraFollowTarget");
            Undo.RegisterCreatedObjectUndo(follow, "Create CameraFollowTarget");
            follow.transform.SetParent(cameraTargets.transform, false);
            follow.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            GameObject look = new GameObject("CameraLookTarget");
            Undo.RegisterCreatedObjectUndo(look, "Create CameraLookTarget");
            look.transform.SetParent(cameraTargets.transform, false);
            look.transform.localPosition = new Vector3(0f, 1.6f, 0.1f);

            Transform characterVisualRootForFacing = modelOffsetRootGo.transform;
            GroundAlignOutcome groundOutcome = default;
            bool modelPrefabSlotUsed = sourceModelPrefab != null;
            bool modelInstanceCreated = false;

            if (sourceModelPrefab != null)
            {
                GameObject modelInstance = InstantiateSourceModel(sourceModelPrefab, modelOffsetRootGo.transform);
                if (modelInstance == null)
                {
                    Undo.DestroyObjectImmediate(playerRoot);
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Model instantiation failed; setup rolled back.",
                        this);
                    return null;
                }

                modelInstanceCreated = true;
                groundOutcome = AlignModelOffsetForVisualGrounding(
                    modelOffsetRootGo.transform,
                    modelInstance.transform,
                    playerRoot);
                FitCharacterMotorToVisualBounds(
                    motor,
                    playerRoot.transform,
                    visuals.transform,
                    enableWizardDebugLogs,
                    playerRoot);
                AdjustCameraTargetsForMotorHeight(follow.transform, look.transform, motor);
                DisableAllAnimatorsUnderModelOffset(modelOffsetRootGo.transform, playerRoot.transform);
            }

            ApplyCharacterBindings(
                character,
                motor,
                follow.transform,
                look.transform,
                characterVisualRootForFacing,
                moveReference,
                defaultMoveSpeed,
                defaultRotationSmoothTime,
                defaultInputDeadZone,
                enableWizardDebugLogs);

            LogSimpleSetupReport(playerRoot, groundOutcome, modelPrefabSlotUsed, modelInstanceCreated);

            EditorUtility.SetDirty(playerRoot);
            return playerRoot;
        }

        private static void DisableAllAnimatorsUnderModelOffset(Transform modelOffsetRoot, Transform playerRoot)
        {
            if (modelOffsetRoot == null)
            {
                return;
            }

            Animator[] animators = modelOffsetRoot.GetComponentsInChildren<Animator>(true);
            int disabled = 0;
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                Undo.RecordObject(animator, "CCS: disable Animators under ModelOffsetRoot");
                animator.enabled = false;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
                disabled++;
            }

            if (disabled > 0)
            {
                Debug.Log(
                    "[CCS_CharacterSetupWizard] Disabled "
                    + disabled
                    + " Animator(s) under ModelOffsetRoot so the imported mesh stays static (CharacterController-only locomotion).",
                    modelOffsetRoot);
            }
        }

        private void LogSimpleSetupReport(
            GameObject playerRoot,
            GroundAlignOutcome groundOutcome,
            bool modelPrefabSlotUsed,
            bool modelInstanceCreated)
        {
            if (playerRoot == null)
            {
                return;
            }

            string groundLine = !groundOutcome.HadRenderableBounds
                ? "skipped (no renderers)"
                : groundOutcome.OffsetApplied
                    ? "applied (local Y " + groundOutcome.LocalYOffsetApplied.ToString("F3") + ")"
                    : groundOutcome.SkippedNearZero
                        ? "skipped (already aligned)"
                        : "skipped";

            StringBuilder sb = new StringBuilder(256);
            sb.AppendLine("[CCS] Setup report:");
            sb.AppendLine("* Locomotion: Unity CharacterController + CCS_CharacterController (no CCS_AnimatorDriver).");
            sb.AppendLine("* Camera: CCS_CameraRig serialized tuning (no camera profile asset).");
            sb.AppendLine("* Model slot used: " + modelPrefabSlotUsed + ", instance created: " + modelInstanceCreated);
            sb.AppendLine("* Visual ground alignment: " + groundLine);
            Debug.Log(sb.ToString(), playerRoot);

            if (modelPrefabSlotUsed && !modelInstanceCreated)
            {
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Model prefab was set but no instance exists; this should not occur after a successful create.",
                    playerRoot);
            }
        }

        private GameObject InstantiateSourceModel(GameObject source, Transform parent)
        {
            GameObject instance;
            if (PrefabUtility.IsPartOfPrefabAsset(source))
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(source, parent);
                instance.name = source.name;
            }

            if (instance == null)
            {
                Debug.LogError("[CCS_CharacterSetupWizard] Failed to instantiate the source model.", this);
                return null;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate Character Model");
            Transform transform = instance.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            return instance;
        }

        private Camera ResolveOrCreateMainCameraUnderRig(Transform rigRoot)
        {
            if (TryGetSceneMainCamera(out Camera existing))
            {
                EnsureMainCameraTag(existing);
                EnsureCinemachineBrain(existing);
                if (enableWizardDebugLogs)
                {
                    Debug.Log(
                        "[CCS_CharacterSetupWizard] Using existing scene Main Camera; Cinemachine Third Person Follow Cam drives it.",
                        existing);
                }

                return existing;
            }

            if (enableWizardDebugLogs)
            {
                Debug.Log(
                    "[CCS_CharacterSetupWizard] No MainCamera in scene; creating Main Camera under CCSCameraRig.",
                    rigRoot);
            }

            GameObject mainCameraObject = new GameObject(RigMainCameraChildName);
            Undo.RegisterCreatedObjectUndo(mainCameraObject, "Create Main Camera");
            mainCameraObject.transform.SetParent(rigRoot, false);
            Camera camera = Undo.AddComponent<Camera>(mainCameraObject);
            Undo.AddComponent<CinemachineBrain>(mainCameraObject);
            mainCameraObject.tag = "MainCamera";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 5000f;
            EnsureMainCameraTag(camera);
            return camera;
        }

        private static bool TryGetSceneMainCamera(out Camera camera)
        {
            camera = Camera.main;
            if (camera != null)
            {
                return true;
            }

            GameObject[] tagged = GameObject.FindGameObjectsWithTag("MainCamera");
            for (int i = 0; i < tagged.Length; i++)
            {
                Camera candidate = tagged[i].GetComponent<Camera>();
                if (candidate != null)
                {
                    camera = candidate;
                    return true;
                }
            }

            camera = null;
            return false;
        }

        private static void EnsureCinemachineBrain(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (camera.GetComponent<CinemachineBrain>() != null)
            {
                return;
            }

            Undo.AddComponent<CinemachineBrain>(camera.gameObject);
            EditorUtility.SetDirty(camera.gameObject);
            Debug.LogWarning(
                "[CCS_CharacterSetupWizard] CinemachineBrain was missing on the Main Camera; it was added automatically.",
                camera.gameObject);
        }

        private static void EnsureMainCameraTag(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (camera.CompareTag("MainCamera"))
            {
                return;
            }

            try
            {
                camera.gameObject.tag = "MainCamera";
                EditorUtility.SetDirty(camera.gameObject);
                Debug.LogWarning(
                    "[CCS_CharacterSetupWizard] Main Camera was not tagged MainCamera; tag was assigned for CCS gameplay camera lookup.",
                    camera.gameObject);
            }
            catch (UnityException ex)
            {
                Debug.LogWarning(
                    $"[CCS_CharacterSetupWizard] Could not assign MainCamera tag. Add it in Tag Manager. {ex.Message}",
                    camera);
            }
        }

        private void EnsurePlayerTag(GameObject playerRoot)
        {
            if (playerRoot == null)
            {
                return;
            }

            try
            {
                playerRoot.tag = "Player";
            }
            catch (UnityException ex)
            {
                Debug.LogWarning(
                    $"[CCS_CharacterSetupWizard] Could not assign Player tag. Add it in Tag Manager. {ex.Message}",
                    this);
            }
        }

        private GameObject CreateCameraRigHierarchy(GameObject playerRoot, InputActionReference lookReference)
        {
            CCS_CharacterController character = playerRoot.GetComponent<CCS_CharacterController>();
            Transform follow = playerRoot.transform.Find("CameraTargets/CameraFollowTarget");
            Transform look = playerRoot.transform.Find("CameraTargets/CameraLookTarget");

            if (follow == null || look == null)
            {
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Cannot create camera rig: player is missing CameraTargets/CameraFollowTarget or CameraLookTarget.",
                    playerRoot);
                return null;
            }

            GameObject rigRoot = new GameObject(GetUniqueRootName("CCSCameraRig"));
            Undo.RegisterCreatedObjectUndo(rigRoot, "Create CCSCameraRig");
            CCS_CameraRig cameraRig = Undo.AddComponent<CCS_CameraRig>(rigRoot);

            Camera renderCamera = ResolveOrCreateMainCameraUnderRig(rigRoot.transform);

            GameObject thirdPersonCm = new GameObject(ThirdPersonCinemachineChildName);
            Undo.RegisterCreatedObjectUndo(thirdPersonCm, "Create Cinemachine Third Person Follow Cam");
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

            ApplyCameraRigBindings(
                cameraRig,
                follow,
                look,
                renderCamera,
                cinemachineCamera,
                character);

            Undo.RecordObject(cameraRig, "Apply CCS camera serialized tuning");
            Undo.RecordObject(cinemachineCamera, "Apply CCS camera serialized tuning");
            Undo.RecordObject(orbitalFollow, "Apply CCS camera serialized tuning");
            Undo.RecordObject(rotationComposer, "Apply CCS camera serialized tuning");
            if (renderCamera != null)
            {
                Undo.RecordObject(renderCamera, "Apply CCS camera serialized tuning");
            }

            cameraRig.ApplySerializedCameraTuning();

            ValidateAndLogCcsThirdPersonRig(
                rigRoot,
                follow,
                look,
                character,
                lookReference);

            EditorUtility.SetDirty(rigRoot);
            EditorUtility.SetDirty(thirdPersonCm);
            EditorUtility.SetDirty(cinemachineCamera);
            EditorUtility.SetDirty(orbitalFollow);
            EditorUtility.SetDirty(rotationComposer);
            CinemachineInputAxisController inputAxisController = thirdPersonCm.GetComponent<CinemachineInputAxisController>();
            if (inputAxisController != null)
            {
                EditorUtility.SetDirty(inputAxisController);
            }

            if (renderCamera != null)
            {
                EditorUtility.SetDirty(renderCamera.gameObject);
            }

            return rigRoot;
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
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Cannot wire Cinemachine orbit input: Look InputActionReference is missing.",
                    thirdPersonVcamObject);
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

            Undo.RecordObject(axisController, "Wire Cinemachine orbit input");
            for (int i = 0; i < axisController.Controllers.Count; i++)
            {
                InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller controller =
                    axisController.Controllers[i];
                if (controller == null || controller.Input == null)
                {
                    continue;
                }

                if (controller.Name == "Look Orbit X")
                {
                    controller.Enabled = true;
                    controller.Input.InputAction = lookActionReference;
                    controller.Input.CancelDeltaTime = true;
                    DefaultInputAxisDriver driverX = controller.Driver;
                    driverX.AccelTime = ThirdPersonLookOrbitInputAccelTime;
                    driverX.DecelTime = ThirdPersonLookOrbitInputDecelTime;
                    controller.Driver = driverX;
                }
                else if (controller.Name == "Look Orbit Y")
                {
                    controller.Enabled = true;
                    controller.Input.InputAction = lookActionReference;
                    controller.Input.CancelDeltaTime = true;
                    DefaultInputAxisDriver driverY = controller.Driver;
                    driverY.AccelTime = ThirdPersonLookOrbitInputAccelTime;
                    driverY.DecelTime = ThirdPersonLookOrbitInputDecelTime;
                    controller.Driver = driverY;
                }
                else if (controller.Name == "Orbit Scale")
                {
                    controller.Enabled = false;
                }
            }

            EditorUtility.SetDirty(axisController);
        }

        private void ValidateAndLogCcsThirdPersonRig(
            GameObject rigRoot,
            Transform expectedFollow,
            Transform expectedLook,
            CCS_CharacterController expectedPlayer,
            InputActionReference expectedLookActionForCinemachine)
        {
            int errors = 0;
            int warnings = 0;

            if (rigRoot == null)
            {
                Debug.LogError("[CCS_CharacterSetupWizard] Rig validation failed: rig root is null.", this);
                return;
            }

            CCS_CameraRig rig = rigRoot.GetComponent<CCS_CameraRig>();
            if (rig == null)
            {
                errors++;
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Rig validation failed: CCSCameraRig root is missing CCS_CameraRig.",
                    rigRoot);
            }

            Transform cmChild = rigRoot.transform.Find(ThirdPersonCinemachineChildName);
            CinemachineCamera vcam = null;
            CinemachineOrbitalFollow orbital = null;
            CinemachineRotationComposer composer = null;
            CinemachineInputAxisController inputAxisController = null;

            if (cmChild == null)
            {
                errors++;
                Debug.LogError(
                    $"[CCS_CharacterSetupWizard] Rig validation failed: missing child '{ThirdPersonCinemachineChildName}'.",
                    rigRoot);
            }
            else
            {
                vcam = cmChild.GetComponent<CinemachineCamera>();
                orbital = cmChild.GetComponent<CinemachineOrbitalFollow>();
                composer = cmChild.GetComponent<CinemachineRotationComposer>();
                inputAxisController = cmChild.GetComponent<CinemachineInputAxisController>();

                if (vcam == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineCamera missing on third-person child.",
                        cmChild);
                }

                if (orbital == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineOrbitalFollow missing on third-person child.",
                        cmChild);
                }

                if (composer == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineRotationComposer missing on third-person child.",
                        cmChild);
                }

                if (inputAxisController == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineInputAxisController missing on third-person child (required to drive orbit from Look).",
                        cmChild);
                }
                else
                {
                    inputAxisController.SynchronizeControllers();
                    InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller orbitX =
                        inputAxisController.GetController("Look Orbit X");
                    InputAxisControllerBase<CinemachineInputAxisController.Reader>.Controller orbitY =
                        inputAxisController.GetController("Look Orbit Y");
                    if (orbitX == null || orbitY == null)
                    {
                        errors++;
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineInputAxisController is missing Look Orbit X/Y controllers.",
                            inputAxisController);
                    }
                    else if (!orbitX.Enabled || !orbitY.Enabled)
                    {
                        errors++;
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Rig validation failed: Look Orbit X/Y input controllers must be enabled.",
                            inputAxisController);
                    }
                    else if (orbitX.Input == null || orbitY.Input == null
                             || orbitX.Input.InputAction == null || orbitY.Input.InputAction == null)
                    {
                        errors++;
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Rig validation failed: Look Orbit X/Y must reference Gameplay/Look on the CCS input asset.",
                            inputAxisController);
                    }
                    else if (expectedLookActionForCinemachine != null
                             && (orbitX.Input.InputAction != expectedLookActionForCinemachine
                                 || orbitY.Input.InputAction != expectedLookActionForCinemachine))
                    {
                        errors++;
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Rig validation failed: Cinemachine Look input does not match expected InputActionReference.",
                            inputAxisController);
                    }
                }
            }

            Transform mainChild = rigRoot.transform.Find(RigMainCameraChildName);
            Camera mainCam = null;
            if (mainChild != null)
            {
                mainCam = mainChild.GetComponent<Camera>();
            }

            if (mainCam == null)
            {
                if (TryGetSceneMainCamera(out Camera sceneMain))
                {
                    warnings++;
                    Debug.LogWarning(
                        "[CCS_CharacterSetupWizard] Main Camera is not a child of CCSCameraRig; using a scene MainCamera. For the standard hierarchy, place Main Camera under the rig.",
                        rigRoot);
                    mainCam = sceneMain;
                }
            }

            if (mainCam == null)
            {
                errors++;
                Debug.LogError(
                    "[CCS_CharacterSetupWizard] Rig validation failed: no Main Camera (child or scene) with a Camera component.",
                    rigRoot);
            }
            else
            {
                if (mainCam.GetComponent<CinemachineBrain>() == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: Main Camera has no CinemachineBrain.",
                        mainCam.gameObject);
                }

                if (!mainCam.CompareTag("MainCamera"))
                {
                    warnings++;
                    Debug.LogWarning(
                        "[CCS_CharacterSetupWizard] Main Camera should be tagged MainCamera for CCS_CharacterController camera-relative movement.",
                        mainCam.gameObject);
                }
            }

            if (rig != null)
            {
                SerializedObject so = new SerializedObject(rig);
                SerializedProperty followProp = so.FindProperty("cameraFollowTarget");
                SerializedProperty lookProp = so.FindProperty("cameraLookTarget");
                SerializedProperty mainProp = so.FindProperty("mainCamera");
                SerializedProperty vcamProp = so.FindProperty("cinemachineCamera");
                SerializedProperty playerProp = so.FindProperty("playerCharacterController");

                if (followProp.objectReferenceValue == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig camera follow target is not assigned.",
                        rig);
                }

                if (lookProp.objectReferenceValue == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig camera look target is not assigned.",
                        rig);
                }

                if (mainProp.objectReferenceValue == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig main camera is not assigned.",
                        rig);
                }

                if (vcamProp.objectReferenceValue == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig cinemachine camera is not assigned.",
                        rig);
                }

                if (playerProp.objectReferenceValue == null)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig player character controller is not assigned.",
                        rig);
                }

                if (expectedFollow != null && followProp.objectReferenceValue != expectedFollow)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig follow target does not match expected CameraFollowTarget.",
                        rig);
                }

                if (expectedLook != null && lookProp.objectReferenceValue != expectedLook)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig look target does not match expected CameraLookTarget.",
                        rig);
                }

                if (expectedPlayer != null && playerProp.objectReferenceValue != expectedPlayer)
                {
                    errors++;
                    Debug.LogError(
                        "[CCS_CharacterSetupWizard] Rig validation failed: CCS_CameraRig player reference does not match expected CCS_CharacterController.",
                        rig);
                }

                if (vcam != null)
                {
                    CameraTarget ct = vcam.Target;
                    Transform assignedFollow = followProp.objectReferenceValue as Transform;
                    Transform assignedLook = lookProp.objectReferenceValue as Transform;
                    if (assignedFollow != null && ct.TrackingTarget != assignedFollow)
                    {
                        errors++;
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineCamera Target.TrackingTarget does not match CCS_CameraRig follow target.",
                            vcam);
                    }

                    if (assignedLook != null && (ct.LookAtTarget != assignedLook || !ct.CustomLookAtTarget))
                    {
                        errors++;
                        Debug.LogError(
                            "[CCS_CharacterSetupWizard] Rig validation failed: CinemachineCamera Target look settings do not match CCS_CameraRig look target.",
                            vcam);
                    }
                }
            }

            if (errors == 0)
            {
                Debug.Log(
                    $"[CCS_CharacterSetupWizard] CCS third-person rig validation passed for '{rigRoot.name}'. " +
                    "Main Camera, third-person vcam with Cinemachine input-driven orbit, body/aim, and CCS_CameraRig references are wired.",
                    rigRoot);
            }
            else
            {
                Debug.LogError(
                    $"[CCS_CharacterSetupWizard] Rig validation finished with {errors} error(s) and {warnings} warning(s). Fix the issues above or run Create Character again.",
                    rigRoot);
            }
        }

        private static void ApplyCharacterBindings(
            CCS_CharacterController character,
            UnityEngine.CharacterController motor,
            Transform followTarget,
            Transform lookTarget,
            Transform visualRoot,
            InputActionReference moveReference,
            float moveSpeed,
            float rotationSmoothTime,
            float inputDeadZone,
            bool debugLogs)
        {
            SerializedObject serializedObject = new SerializedObject(character);
            serializedObject.FindProperty("characterMotor").objectReferenceValue = motor;
            serializedObject.FindProperty("cameraFollowTarget").objectReferenceValue = followTarget;
            serializedObject.FindProperty("cameraLookTarget").objectReferenceValue = lookTarget;
            serializedObject.FindProperty("characterVisualRoot").objectReferenceValue = visualRoot;
            serializedObject.FindProperty("moveAction").objectReferenceValue = moveReference;
            serializedObject.FindProperty("moveSpeed").floatValue = moveSpeed;
            serializedObject.FindProperty("rotationSmoothTime").floatValue = rotationSmoothTime;
            serializedObject.FindProperty("inputDeadZone").floatValue = inputDeadZone;
            serializedObject.FindProperty("enableDebugLogs").boolValue = debugLogs;
            serializedObject.ApplyModifiedProperties();
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

        private static string GetUniqueRootName(string baseName)
        {
            int suffix = 0;
            string candidate = baseName;
            while (GameObject.Find(candidate) != null)
            {
                suffix++;
                candidate = $"{baseName} ({suffix})";
            }

            return candidate;
        }

        private static GroundAlignOutcome AlignModelOffsetForVisualGrounding(
            Transform modelOffsetRoot,
            Transform meshSearchRoot,
            GameObject playerRoot)
        {
            GroundAlignOutcome outcome = default;
            if (modelOffsetRoot == null || meshSearchRoot == null)
            {
                return outcome;
            }

            if (!TryGetLowestRendererPointLocalY(modelOffsetRoot, meshSearchRoot, out float lowestLocalY))
            {
                Debug.LogWarning(
                    "[CCS_CharacterSetupWizard] No Renderer under imported model; skipped visual ground alignment.",
                    meshSearchRoot);
                return outcome;
            }

            outcome.HadRenderableBounds = true;

            if (Mathf.Abs(lowestLocalY) < 0.0005f)
            {
                outcome.SkippedNearZero = true;
                Debug.Log(
                    "[CCS_CharacterSetupWizard] Visual ground alignment: mesh bottom already at ModelOffsetRoot origin (local).",
                    playerRoot);
                return outcome;
            }

            Undo.RecordObject(modelOffsetRoot, "Align character visual to ground (ModelOffsetRoot local Y)");
            Vector3 local = modelOffsetRoot.localPosition;
            float newY = local.y - lowestLocalY;
            modelOffsetRoot.localPosition = new Vector3(local.x, newY, local.z);
            outcome.OffsetApplied = true;
            outcome.LocalYOffsetApplied = newY;
            Debug.Log(
                "[CCS_CharacterSetupWizard] Visual ground alignment: ModelOffsetRoot.localPosition.y set to "
                + newY.ToString("F3")
                + " (mesh lowest local Y was "
                + lowestLocalY.ToString("F3")
                + ").",
                modelOffsetRoot);
            return outcome;
        }

        private static bool TryGetLowestRendererPointLocalY(
            Transform modelOffsetRoot,
            Transform meshSearchRoot,
            out float lowestLocalY)
        {
            lowestLocalY = 0f;
            Renderer[] renderers = meshSearchRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return false;
            }

            Matrix4x4 worldToLocal = modelOffsetRoot.worldToLocalMatrix;
            float minY = float.MaxValue;
            Vector3[] corners = new Vector3[8];

            for (int r = 0; r < renderers.Length; r++)
            {
                Bounds bounds = renderers[r].bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                corners[0] = new Vector3(min.x, min.y, min.z);
                corners[1] = new Vector3(min.x, min.y, max.z);
                corners[2] = new Vector3(min.x, max.y, min.z);
                corners[3] = new Vector3(min.x, max.y, max.z);
                corners[4] = new Vector3(max.x, min.y, min.z);
                corners[5] = new Vector3(max.x, min.y, max.z);
                corners[6] = new Vector3(max.x, max.y, min.z);
                corners[7] = new Vector3(max.x, max.y, max.z);

                for (int c = 0; c < 8; c++)
                {
                    float y = worldToLocal.MultiplyPoint3x4(corners[c]).y;
                    if (y < minY)
                    {
                        minY = y;
                    }
                }
            }

            lowestLocalY = minY;
            return true;
        }

        private static void FitCharacterMotorToVisualBounds(
            UnityEngine.CharacterController motor,
            Transform playerRoot,
            Transform characterVisualsRoot,
            bool logDebug,
            Object context)
        {
            if (motor == null || playerRoot == null || characterVisualsRoot == null)
            {
                return;
            }

            Renderer[] renderers = characterVisualsRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Matrix4x4 worldToPlayer = playerRoot.worldToLocalMatrix;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            Vector3[] corners = new Vector3[8];

            for (int r = 0; r < renderers.Length; r++)
            {
                Bounds bounds = renderers[r].bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                corners[0] = new Vector3(min.x, min.y, min.z);
                corners[1] = new Vector3(min.x, min.y, max.z);
                corners[2] = new Vector3(min.x, max.y, min.z);
                corners[3] = new Vector3(min.x, max.y, max.z);
                corners[4] = new Vector3(max.x, min.y, min.z);
                corners[5] = new Vector3(max.x, min.y, max.z);
                corners[6] = new Vector3(max.x, max.y, min.z);
                corners[7] = new Vector3(max.x, max.y, max.z);

                for (int c = 0; c < 8; c++)
                {
                    Vector3 lp = worldToPlayer.MultiplyPoint3x4(corners[c]);
                    if (lp.y < minY)
                    {
                        minY = lp.y;
                    }

                    if (lp.y > maxY)
                    {
                        maxY = lp.y;
                    }

                    if (lp.x < minX)
                    {
                        minX = lp.x;
                    }

                    if (lp.x > maxX)
                    {
                        maxX = lp.x;
                    }

                    if (lp.z < minZ)
                    {
                        minZ = lp.z;
                    }

                    if (lp.z > maxZ)
                    {
                        maxZ = lp.z;
                    }
                }
            }

            float spanY = maxY - minY;
            if (spanY < 0.35f)
            {
                return;
            }

            const float verticalPadding = 0.16f;
            float height = Mathf.Clamp(spanY + verticalPadding, 1.15f, 3.45f);
            float centerY = minY + height * 0.5f;

            float extentX = (maxX - minX) * 0.5f;
            float extentZ = (maxZ - minZ) * 0.5f;
            float radius = Mathf.Clamp(Mathf.Max(extentX, extentZ) * 0.48f, 0.22f, 0.55f);

            Undo.RecordObject(motor, "Fit CharacterController to character visuals");
            motor.height = height;
            motor.center = new Vector3(0f, centerY, 0f);
            motor.radius = radius;
            motor.skinWidth = Mathf.Clamp(radius * 0.12f, 0.05f, 0.1f);

            if (logDebug)
            {
                Debug.Log(
                    "[CCS_CharacterSetupWizard] CharacterController fit to render bounds: height="
                    + height.ToString("F2")
                    + ", center.y="
                    + centerY.ToString("F2")
                    + ", radius="
                    + radius.ToString("F2")
                    + ".",
                    context);
            }
        }

        private static void AdjustCameraTargetsForMotorHeight(
            Transform follow,
            Transform look,
            UnityEngine.CharacterController motor)
        {
            if (follow == null || look == null || motor == null)
            {
                return;
            }

            float bottomY = motor.center.y - motor.height * 0.5f;
            float eyeY = Mathf.Clamp(bottomY + motor.height * 0.82f, 1.05f, 2.35f);
            follow.localPosition = new Vector3(0f, eyeY, 0f);
            Vector3 lookLocal = look.localPosition;
            look.localPosition = new Vector3(lookLocal.x, eyeY, lookLocal.z);
        }
    }
}
