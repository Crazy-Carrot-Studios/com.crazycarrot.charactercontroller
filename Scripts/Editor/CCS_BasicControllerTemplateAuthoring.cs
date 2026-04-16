using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_BasicControllerTemplateAuthoring
// Purpose: One-shot authoring for PF_CCS_BasicController_Template (Invector-style shell: motor + CCS_CharacterController on root, input wired).
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-15
//==============================================================================

namespace CCS.CharacterController.Editor
{
    public static class CCS_BasicControllerTemplateAuthoring
    {
        #region Menu

        public static void BuildFromCommandLine()
        {
            BuildTemplatePrefab();
            EditorApplication.Exit(0);
        }

        [MenuItem("CCS/Character Controller/Authoring/Build PF_CCS_BasicController_Template", priority = 100)]
        private static void BuildTemplatePrefab()
        {
            string prefabSavePath = CCS_InputAssetUtility.ResolvedBasicControllerTemplatePrefabPath;
            string inputActionsPath = CCS_InputAssetUtility.ResolvedPackageInputActionsPath;
            string starterVisualPrefabPath = CCS_InputAssetUtility.ResolvedStarterVisualPrefabPath;

            if (!CCS_InputAssetUtility.EnsureFolderHierarchyExistsForAssetPath(prefabSavePath))
            {
                Debug.LogError("[CCS_BasicControllerTemplateAuthoring] Could not create folder for " + prefabSavePath);
                return;
            }

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionsPath);
            if (inputAsset == null)
            {
                Debug.LogError("[CCS_BasicControllerTemplateAuthoring] Missing InputActionAsset at " + inputActionsPath);
                return;
            }

            AnimatorController minimalController =
                CCS_BasicLocomotionMinimalControllerAuthoring.EnsureMinimalControllerExists(false, out string minimalErr);
            if (minimalController == null)
            {
                Debug.LogError("[CCS_BasicControllerTemplateAuthoring] " + minimalErr);
                return;
            }

            GameObject starterVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(starterVisualPrefabPath);
            if (starterVisualPrefab == null)
            {
                Debug.LogError("[CCS_BasicControllerTemplateAuthoring] Missing starter visual prefab at " + starterVisualPrefabPath);
                return;
            }

            GameObject root = new GameObject("CCSPlayer");
            root.tag = "Player";

            UnityEngine.CharacterController motor = root.AddComponent<UnityEngine.CharacterController>();
            motor.height = 2f;
            motor.radius = 0.35f;
            motor.slopeLimit = 45f;
            motor.stepOffset = 0.3f;
            motor.skinWidth = 0.08f;
            motor.minMoveDistance = 0.001f;
            motor.center = new Vector3(0f, 1f, 0f);

            CCS_CharacterController ccs = root.AddComponent<CCS_CharacterController>();

            Transform visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            GameObject visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(starterVisualPrefab, visualRoot);
            if (visualInstance != null)
            {
                visualInstance.transform.localPosition = Vector3.zero;
                visualInstance.transform.localRotation = Quaternion.identity;
                visualInstance.transform.localScale = Vector3.one;
            }

            CCS_AnimatorSetupUtility.TryAssignPackageMinimalLocomotionOnPrimaryVisual(
                visualRoot,
                root.transform,
                null,
                true);

            Transform cameraTargets = new GameObject("CameraTargets").transform;
            cameraTargets.SetParent(root.transform, false);
            cameraTargets.localPosition = Vector3.zero;
            cameraTargets.localRotation = Quaternion.identity;
            cameraTargets.localScale = Vector3.one;

            Transform cameraFollowTarget = new GameObject("CameraFollowTarget").transform;
            cameraFollowTarget.SetParent(cameraTargets, false);
            cameraFollowTarget.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraFollowTarget.localRotation = Quaternion.identity;
            cameraFollowTarget.localScale = Vector3.one;

            Transform cameraLookTarget = new GameObject("CameraLookTarget").transform;
            cameraLookTarget.SetParent(cameraTargets, false);
            cameraLookTarget.localPosition = new Vector3(0f, 1.6f, 0.1f);
            cameraLookTarget.localRotation = Quaternion.identity;
            cameraLookTarget.localScale = Vector3.one;

            InputActionMap gameplayMap = inputAsset.FindActionMap("Gameplay");
            if (gameplayMap == null)
            {
                Debug.LogError("[CCS_BasicControllerTemplateAuthoring] Missing action map 'Gameplay' on input asset.");
                Object.DestroyImmediate(root);
                return;
            }

            InputAction move = gameplayMap.FindAction("Move");
            InputAction sprint = gameplayMap.FindAction("Sprint");
            if (move == null || sprint == null)
            {
                Debug.LogError("[CCS_BasicControllerTemplateAuthoring] Could not find Move or Sprint in Gameplay map.");
                Object.DestroyImmediate(root);
                return;
            }

            InputActionReference moveReference = InputActionReference.Create(move);
            InputActionReference sprintReference = InputActionReference.Create(sprint);

            CCS_AnimatorSetupUtility.PrimaryAnimatorResult animPick =
                CCS_AnimatorSetupUtility.SelectPrimaryLocomotionAnimator(visualRoot, root.transform);

            SerializedObject serializedCcs = new SerializedObject(ccs);
            serializedCcs.FindProperty("characterMotor").objectReferenceValue = motor;
            serializedCcs.FindProperty("cameraFollowTarget").objectReferenceValue = cameraFollowTarget;
            serializedCcs.FindProperty("cameraLookTarget").objectReferenceValue = cameraLookTarget;
            serializedCcs.FindProperty("characterVisualRoot").objectReferenceValue = visualRoot;
            serializedCcs.FindProperty("moveAction").objectReferenceValue = moveReference;
            serializedCcs.FindProperty("sprintAction").objectReferenceValue = sprintReference;
            if (animPick.PrimaryAnimator != null)
            {
                serializedCcs.FindProperty("locomotionAnimator").objectReferenceValue = animPick.PrimaryAnimator;
            }

            serializedCcs.FindProperty("driveMinimalLocomotionParameterSet").boolValue = true;
            serializedCcs.FindProperty("driveLocomotionAnimator").boolValue = true;
            serializedCcs.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabSavePath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefabSavePath);
            EditorGUIUtility.PingObject(saved);
            Debug.Log("[CCS_BasicControllerTemplateAuthoring] Wrote " + prefabSavePath, saved);
        }

        #endregion
    }
}
