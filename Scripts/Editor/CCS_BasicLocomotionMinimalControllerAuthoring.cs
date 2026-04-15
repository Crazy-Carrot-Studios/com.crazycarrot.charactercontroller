using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_BasicLocomotionMinimalControllerAuthoring
// Purpose: Builds AC_CCS_BasicLocomotion_Minimal.controller — Phase 1 single-layer 1D blend (idle/walk/run/sprint)
//          driven by InputMagnitude + IsGrounded + IsSprinting from CCS_CharacterController.
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-15
//==============================================================================

namespace CCS.CharacterController.Editor
{
    /// <summary>
    /// Authoring for the Phase 1 minimal locomotion AnimatorController (charter §2.2.1).
    /// </summary>
    internal static class CCS_BasicLocomotionMinimalControllerAuthoring
    {
        internal const string RelativeControllerPath =
            "Animations/Controllers/AC_CCS_BasicLocomotion_Minimal.controller";

        private const string RelativeIdleClip = "Animations/Clips/Locomotion/ANIM_CCS_Basic_FreeMovement_Idle.anim";
        private const string RelativeWalkClip = "Animations/Clips/Locomotion/ANIM_CCS_Basic_FreeMovement_Walk.anim";
        private const string RelativeRunClip = "Animations/Clips/Locomotion/ANIM_CCS_Basic_FreeMovement_Run.anim";
        private const string RelativeSprintClip = "Animations/Clips/Locomotion/ANIM_CCS_Basic_FreeMovement_Sprint.anim";

        /// <summary>
        /// Returns the asset path for the minimal controller under the resolved package root.
        /// </summary>
        internal static string ResolvedSavePath =>
            $"{CCS_InputAssetUtility.GetResolvedPackageRoot()}/{RelativeControllerPath}";

        /// <summary>
        /// Ensures <c>AC_CCS_BasicLocomotion_Minimal.controller</c> exists on disk; builds or rebuilds when <paramref name="forceRebuild"/> is true.
        /// </summary>
        internal static AnimatorController EnsureMinimalControllerExists(bool forceRebuild, out string errorMessage)
        {
            errorMessage = null;
            string path = ResolvedSavePath;
            if (!EnsureFolderForAssetPath(path))
            {
                errorMessage = "Could not ensure folder for " + path;
                return null;
            }

            if (!forceRebuild)
            {
                AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (existing != null)
                {
                    return existing;
                }
            }
            else if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AnimationClip idle = LoadClip(RelativeIdleClip, out string idleErr);
            AnimationClip walk = LoadClip(RelativeWalkClip, out string walkErr);
            AnimationClip run = LoadClip(RelativeRunClip, out string runErr);
            AnimationClip sprint = LoadClip(RelativeSprintClip, out string sprintErr);
            if (idle == null || walk == null || run == null || sprint == null)
            {
                errorMessage = $"Missing locomotion clips. Idle: {idleErr} Walk: {walkErr} Run: {runErr} Sprint: {sprintErr}";
                return null;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("InputMagnitude", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            BlendTree blendTree = new BlendTree
            {
                name = "GroundLocomotion1D",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "InputMagnitude",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, 0.28f);
            blendTree.AddChild(run, 0.65f);
            blendTree.AddChild(sprint, 1f);

            AnimatorState locomotionState = root.AddState("Locomotion");
            locomotionState.motion = blendTree;
            root.defaultState = locomotionState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        [MenuItem("CCS/Character Controller/Authoring/Build AC_CCS_BasicLocomotion_Minimal", priority = 101)]
        private static void MenuBuildMinimalController()
        {
            AnimatorController ctrl = EnsureMinimalControllerExists(true, out string err);
            if (ctrl == null)
            {
                Debug.LogError("[CCS_BasicLocomotionMinimalControllerAuthoring] " + err);
                return;
            }

            EditorGUIUtility.PingObject(ctrl);
            Debug.Log("[CCS_BasicLocomotionMinimalControllerAuthoring] Wrote " + ResolvedSavePath, ctrl);
        }

        private static AnimationClip LoadClip(string relativeClipPath, out string error)
        {
            error = null;
            string full = $"{CCS_InputAssetUtility.GetResolvedPackageRoot()}/{relativeClipPath}";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(full);
            if (clip == null)
            {
                error = "Not found: " + full;
            }

            return clip;
        }

        private static bool EnsureFolderForAssetPath(string assetPath)
        {
            string folder = assetPath.Replace("\\", "/");
            int lastSlash = folder.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return true;
            }

            folder = folder.Substring(0, lastSlash);
            if (AssetDatabase.IsValidFolder(folder))
            {
                return true;
            }

            string parent = "Assets";
            string remaining = folder.StartsWith("Assets/") ? folder.Substring("Assets/".Length) : folder;
            string[] parts = remaining.Split('/');
            string current = parent;
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                {
                    continue;
                }

                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }

            return AssetDatabase.IsValidFolder(folder);
        }
    }
}
