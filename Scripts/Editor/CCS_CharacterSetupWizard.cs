using System.Collections.Generic;
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
// Purpose: Editor entry point to build CCSPlayer + CCSCameraRig, wire runtime scripts,
//          Cinemachine 3 third-person rig, default camera profile + base locomotion animator, and
//          default input, camera profile, and base locomotion animator. Before creating, removes prior CCS players, rigs, and MainCamera-tagged
//          cameras in loaded scenes so duplicates are not left behind.
// Placement: Editor / menu CCS/Character Controller/Create Character.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    public sealed class CCS_CharacterSetupWizard : CCSEditorWindowBase
    {
        // Exact hierarchy name for the third-person Cinemachine vcam child under CCSCameraRig.
        private const string ThirdPersonCinemachineChildName = "Cinemachine Third Person Follow Cam";
        // Exact hierarchy name for the Unity render camera child under CCSCameraRig.
        private const string RigMainCameraChildName = "Main Camera";

        // Default orbit radius for CinemachineOrbitalFollow (meters from follow target).
        private const float ThirdPersonOrbitRadius = 4f;
        // Default vertical field of view pushed to the vcam lens and gameplay feel.
        private const float ThirdPersonFieldOfView = 58f;
        // Near clip for vcam lens and new Main Camera when spawned by the wizard.
        private const float ThirdPersonNearClip = 0.1f;
        // Far clip for vcam lens and new Main Camera when spawned by the wizard.
        private const float ThirdPersonFarClip = 5000f;
        // Small local X offset on the orbit target for slight over-shoulder framing.
        private const float ThirdPersonOrbitTargetOffsetX = 0.18f;
        // Starting pitch (degrees) inside vertical orbit range for a neutral third-person angle.
        private const float ThirdPersonVerticalPitchCenterDegrees = 12f;
        // Minimum pitch (degrees); matches Cinemachine Orbital Follow vertical axis range.
        private const float ThirdPersonVerticalPitchMinDegrees = -20f;
        // Maximum pitch (degrees); matches Cinemachine Orbital Follow vertical axis range.
        private const float ThirdPersonVerticalPitchMaxDegrees = 80f;
        // Orbital follow position damping per axis (responsive but stable tracking).
        private const float ThirdPersonPositionDamping = 0.22f;
        // Orbital follow rotation damping per axis (yaw/pitch tracking smoothing).
        private const float ThirdPersonRotationDamping = 0.28f;
        // Rotation composer horizontal screen-space damping.
        private const float ThirdPersonComposerDampingX = 0.32f;
        // Rotation composer vertical screen-space damping.
        private const float ThirdPersonComposerDampingY = 0.28f;
        // Priority so this vcam wins over other default Cinemachine cameras in the scene.
        private const int ThirdPersonCinemachinePriority = 20;
        // Input axis driver smoothing seconds (higher = slower ramp to orbit speed after mouse move).
        private const float ThirdPersonLookOrbitInputAccelTime = 0.38f;
        private const float ThirdPersonLookOrbitInputDecelTime = 0.38f;

        // Optional visual model or prefab instantiated under CharacterVisuals.
        private GameObject sourceModelPrefab;
        // Input asset field; null means load or create at the package default path.
        private InputActionAsset inputActionsAsset;
        // Default: CCS_Default_TP_Follow_CameraProfile.asset (see CCS_CharacterControllerPackagePaths). Null uses package path at create.
        [SerializeField]
        private CCS_CameraProfile cameraProfile;
        // Default: CCS_Base_locomotion_controller.controller (see CCS_CharacterControllerPackagePaths). Null uses package path at create.
        [SerializeField]
        private RuntimeAnimatorController locomotionController;
        // When true, writes a new .inputactions file if the package asset is missing.
        private bool autoCreatePackageInputAsset = true;
        // When true, builds CCSCameraRig after cleanup; when false, only the player is created.
        private bool createCameraRigIfMissing = true;
        // Written onto CCS_CharacterController.moveSpeed for the new player.
        private float defaultMoveSpeed = 4.5f;
        // Written onto CCS_CharacterController.rotationSmoothTime for the new player.
        private float defaultRotationSmoothTime = 0.12f;
        // Written onto CCS_CharacterController.inputDeadZone for the new player.
        private float defaultInputDeadZone = 0.08f;
        // Extra wizard and utility logging for diagnosing setup issues.
        private bool enableWizardDebugLogs;

        // Shown in the window title bar and CCS banner.
        protected override string WindowTitle => "Character Controller";

        // Loads default package assets when fields are empty (dev can override in the wizard window).
        protected override void OnEnable()
        {
            base.OnEnable();
            if (inputActionsAsset == null)
            {
                inputActionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    CCS_CharacterControllerPackagePaths.ResolvedPackageInputActionsPath);
            }

            if (cameraProfile == null)
            {
                cameraProfile = AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(
                    CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath);
            }

            if (locomotionController == null)
            {
                locomotionController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    CCS_CharacterControllerPackagePaths.ResolvedDefaultBaseLocomotionAnimatorControllerPath);
            }
        }

        // Opens the setup wizard with a minimum window size.
        [MenuItem("CCS/Character Controller/Create Character")]
        private static void OpenWindow()
        {
            CCS_CharacterSetupWizard window = GetWindow<CCS_CharacterSetupWizard>();
            window.minSize = new Vector2(420f, 480f);
        }

        // Draws wizard sections (Source, Input, Default Assets, Defaults) and Create Character.
        protected override void DrawBody()
        {
            CCSEditorStyles.DrawSectionLabel("Source");
            sourceModelPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Model or prefab", "Instantiated under CharacterVisuals. Leave empty for hierarchy only."),
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

            CCSEditorStyles.DrawSectionLabel("Default Assets");
            cameraProfile = (CCS_CameraProfile)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Camera Profile",
                    "Default: CCS_Default_TP_Follow_CameraProfile. Assigned to CCS_CameraRig after rig creation. Clear to resolve from the package path at create time."),
                cameraProfile,
                typeof(CCS_CameraProfile),
                false);
            locomotionController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Locomotion Controller",
                    "Default: CCS_Base_locomotion_controller. Assigned to the character Animator. Clear to resolve from the package path at create time."),
                locomotionController,
                typeof(RuntimeAnimatorController),
                false);

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

        // Full setup: undo group, clear old CCS objects, create player, optional rig, frame scene.
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

                WarnIfDefaultPackageAssetsMissing();

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
                    Debug.Log("[CCS_CharacterSetupWizard] Character setup completed successfully.", playerRoot);
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

        // Returns the wizard asset or ensures the package asset exists and validates it.
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

        // Warns when optional wizard slots are empty and the package default file is missing (creation still proceeds).
        private void WarnIfDefaultPackageAssetsMissing()
        {
            if (cameraProfile == null
                && AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(
                    CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath) == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterSetupWizard] Camera profile is not assigned and the package default was not found at "
                    + CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath
                    + ". Assign a profile in the wizard or restore the asset.",
                    this);
            }

            if (locomotionController == null
                && AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    CCS_CharacterControllerPackagePaths.ResolvedDefaultBaseLocomotionAnimatorControllerPath) == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterSetupWizard] Locomotion animator is not assigned and the package default was not found at "
                    + CCS_CharacterControllerPackagePaths.ResolvedDefaultBaseLocomotionAnimatorControllerPath
                    + ". Assign a controller in the wizard or restore the asset.",
                    this);
            }
        }

        // Effective profile: wizard slot or package default on disk.
        private CCS_CameraProfile ResolveEffectiveCameraProfile()
        {
            if (cameraProfile != null)
            {
                return cameraProfile;
            }

            return AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(
                CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath);
        }

        // Effective locomotion controller: wizard slot or package default on disk.
        private RuntimeAnimatorController ResolveEffectiveLocomotionController()
        {
            if (locomotionController != null)
            {
                return locomotionController;
            }

            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                CCS_CharacterControllerPackagePaths.ResolvedDefaultBaseLocomotionAnimatorControllerPath);
        }

        // Resolves Gameplay/Move from the CCS input asset.
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

        // Resolves Gameplay/Look from the CCS input asset (Cinemachine Input Axis Controller).
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

        // Destroys all CCS rigs, CCS players, and MainCamera-tagged cameras in loaded scenes (undoable).
        private void RemoveExistingCcsCharacterSetupFromOpenScenes()
        {
            // One set of roots avoids destroying the same hierarchy twice when tags overlap.
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

        // Builds CCSPlayer root, motor, character script, camera targets, optional model, and bindings.
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

            Transform visualRootTransform = visuals.transform;
            if (sourceModelPrefab != null)
            {
                GameObject modelInstance = InstantiateSourceModel(sourceModelPrefab, visuals.transform);
                if (modelInstance != null)
                {
                    visualRootTransform = modelInstance.transform;
                }
            }

            ApplyCharacterBindings(
                character,
                motor,
                follow.transform,
                look.transform,
                visualRootTransform,
                moveReference,
                defaultMoveSpeed,
                defaultRotationSmoothTime,
                defaultInputDeadZone,
                enableWizardDebugLogs);

            Animator locomotionAnimator = ApplyLocomotionAnimatorController(visualRootTransform, playerRoot);
            ApplyAnimatorDriver(playerRoot, character, locomotionAnimator);

            EditorUtility.SetDirty(playerRoot);
            return playerRoot;
        }

        // Ensures an Animator on the visual root (or model root) with the wizard locomotion controller and no root motion.
        private Animator ApplyLocomotionAnimatorController(Transform visualRoot, GameObject playerRoot)
        {
            if (visualRoot == null)
            {
                return null;
            }

            GameObject host = visualRoot.gameObject;
            Animator animator = host.GetComponent<Animator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(host);
            }

            RuntimeAnimatorController controller = ResolveEffectiveLocomotionController();
            if (controller == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterSetupWizard] No locomotion animator controller resolved; assign one in the wizard or restore "
                    + CCS_CharacterControllerPackagePaths.ResolvedDefaultBaseLocomotionAnimatorControllerPath,
                    playerRoot);
                return null;
            }

            Undo.RecordObject(animator, "Assign CCS locomotion animator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
            return animator;
        }

        // Adds CCS_AnimatorDriver on the player root and wires character + locomotion Animator references.
        private static void ApplyAnimatorDriver(GameObject playerRoot, CCS_CharacterController character, Animator locomotionAnimator)
        {
            if (character == null || locomotionAnimator == null)
            {
                return;
            }

            CCS_AnimatorDriver driver = Undo.AddComponent<CCS_AnimatorDriver>(playerRoot);
            SerializedObject serializedDriver = new SerializedObject(driver);
            serializedDriver.FindProperty("characterController").objectReferenceValue = character;
            serializedDriver.FindProperty("locomotionAnimator").objectReferenceValue = locomotionAnimator;
            serializedDriver.ApplyModifiedProperties();
            EditorUtility.SetDirty(driver);
        }

        // Instantiates a prefab asset or scene object under CharacterVisuals with undo support.
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

        // Reuses a scene MainCamera when present; otherwise creates Main Camera child under the rig.
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

        // Finds Camera.main or any MainCamera-tagged object with a Camera component.
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

        // Adds CinemachineBrain to the render camera when missing so the vcam can drive the view.
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

        // Assigns MainCamera tag when needed so Camera.main and CCS_CharacterController resolve correctly.
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

        // Tags the new player root as Player; warns if the tag is missing from the project.
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

        // Creates CCSCameraRig, Main Camera, third-person vcam, configures Cinemachine, binds CCS_CameraRig.
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

            CCS_CameraProfile profileToAssign = ResolveEffectiveCameraProfile();
            if (profileToAssign == null)
            {
                Debug.LogWarning(
                    "[CCS_CharacterSetupWizard] No camera profile resolved; assign one in the wizard or restore "
                    + CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath,
                    rigRoot);
            }
            else
            {
                SerializedObject rigProfileObject = new SerializedObject(cameraRig);
                rigProfileObject.FindProperty("cameraProfile").objectReferenceValue = profileToAssign;
                rigProfileObject.ApplyModifiedProperties();
            }

            Undo.RecordObject(cameraRig, "Apply CCS camera profile");
            Undo.RecordObject(cinemachineCamera, "Apply CCS camera profile");
            Undo.RecordObject(orbitalFollow, "Apply CCS camera profile");
            Undo.RecordObject(rotationComposer, "Apply CCS camera profile");
            if (renderCamera != null)
            {
                Undo.RecordObject(renderCamera, "Apply CCS camera profile");
            }

            cameraRig.ApplyProfile();

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

        // Applies CCS third-person defaults: targets, priority, lens, orbit, damping, composer.
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

        // Adds CinemachineInputAxisController and binds Gameplay/Look to Orbital Follow X/Y (single input owner).
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

        // Verifies hierarchy, components, serialized refs, vcam targets, and Cinemachine Look wiring; logs results.
        private void ValidateAndLogCcsThirdPersonRig(
            GameObject rigRoot,
            Transform expectedFollow,
            Transform expectedLook,
            CCS_CharacterController expectedPlayer,
            InputActionReference expectedLookActionForCinemachine)
        {
            int errors = 0;
            int warnings = 0;

            // Root object and CCS_CameraRig presence.
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

            // Named third-person child and required Cinemachine 3 pipeline components.
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

            // Main Camera under rig preferred; otherwise accept a scene MainCamera with a warning.
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

            // Serialized CCS_CameraRig references and optional equality against expected objects.
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

                // CinemachineCamera Target must match the rig's follow and look transforms.
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

        // Writes motor, camera targets, visual root, move action, and tuning fields on the character.
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

        // Writes follow/look targets, main camera, vcam, and player on CCS_CameraRig (orbit input is on CinemachineInputAxisController).
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

        // Appends numeric suffixes so new roots do not collide with existing scene object names.
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
    }
}
