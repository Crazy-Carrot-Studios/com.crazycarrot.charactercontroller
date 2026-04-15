using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_AnimatorSetupUtility
// Purpose: Phase 1 — find a single primary locomotion Animator under a visual hierarchy,
//          apply root motion on primary only, optional package idle fallback on primary only.
//          Never modifies secondary Animators (no nulling, no controller swaps on children).
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    /// <summary>
    /// Safe animator selection and assignment for Basic Locomotion creation (Phase 1).
    /// </summary>
    internal static class CCS_AnimatorSetupUtility
    {
        #region Nested Types

        internal struct PrimaryAnimatorResult
        {
            public Animator PrimaryAnimator;
            public int AnimatorCount;
            public string SelectionSummary;
        }

        internal struct LocomotionAnimatorApplyResult
        {
            public bool AssignedFallbackController;
            public string FallbackControllerPathOrEmpty;
            public bool PreservedExistingOnPrimary;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Finds Animators under <paramref name="searchRoot"/>; picks one primary using humanoid preference,
        /// renderer coverage, and shallowest depth as tie-breaker.
        /// </summary>
        internal static PrimaryAnimatorResult SelectPrimaryLocomotionAnimator(
            Transform searchRoot,
            Transform pathRootForLogging)
        {
            PrimaryAnimatorResult result = default;
            if (searchRoot == null)
            {
                result.SelectionSummary = "searchRoot was null.";
                return result;
            }

            Animator[] animators = searchRoot.GetComponentsInChildren<Animator>(true);
            result.AnimatorCount = animators.Length;
            if (animators.Length == 0)
            {
                result.SelectionSummary = "No Animator components under visual hierarchy.";
                return result;
            }

            int bestIndex = 0;
            int bestScore = int.MinValue;
            int bestDepth = int.MaxValue;
            var reasons = new StringBuilder(128);

            for (int i = 0; i < animators.Length; i++)
            {
                Animator candidate = animators[i];
                if (candidate == null)
                {
                    continue;
                }

                int tier = GetHumanoidTier(candidate);
                int rendererScore = CountRenderersInHierarchy(candidate.transform);
                int depth = GetTransformDepth(candidate.transform, searchRoot);

                int score = tier * 100000 + rendererScore;
                if (score > bestScore || (score == bestScore && depth < bestDepth))
                {
                    bestScore = score;
                    bestDepth = depth;
                    bestIndex = i;
                }
            }

            result.PrimaryAnimator = animators[bestIndex];
            Transform pathTransform = result.PrimaryAnimator.transform;
            string path = pathRootForLogging != null
                ? BuildTransformPath(pathTransform, pathRootForLogging)
                : pathTransform.name;

            reasons.Append("Chosen '").Append(path).Append("' (index ").Append(bestIndex + 1)
                .Append("/").Append(animators.Length).Append("). ");
            reasons.Append("Humanoid tier ").Append(GetHumanoidTier(result.PrimaryAnimator))
                .Append(", renderer score ")
                .Append(CountRenderersInHierarchy(result.PrimaryAnimator.transform))
                .Append(", depth ").Append(GetTransformDepth(result.PrimaryAnimator.transform, searchRoot))
                .Append(".");

            result.SelectionSummary = reasons.ToString();
            return result;
        }

        /// <summary>
        /// Assigns the package <c>AC_CCS_Locomotion_Base</c> controller to the primary visual <see cref="Animator"/>
        /// when its controller slot is empty (Invector-style: animator lives on the skinned rig with Humanoid avatar).
        /// </summary>
        internal static void TryAssignPackageLocomotionBaseOnPrimaryVisual(
            Transform visualRoot,
            Transform hierarchyRootForLogging,
            List<string> warnings)
        {
            if (visualRoot == null)
            {
                return;
            }

            PrimaryAnimatorResult pick = SelectPrimaryLocomotionAnimator(visualRoot, hierarchyRootForLogging);
            if (pick.PrimaryAnimator == null)
            {
                warnings?.Add(
                    "No Animator under VisualRoot; add a Humanoid Animator on the model or assign AC_CCS_Locomotion_Base manually. ("
                    + pick.SelectionSummary
                    + ")");
                return;
            }

            if (pick.PrimaryAnimator.runtimeAnimatorController != null)
            {
                return;
            }

            RuntimeAnimatorController locomotion =
                CCS_InputAssetUtility.TryLoadLocomotionBaseAnimatorController(out string _);
            if (locomotion == null)
            {
                warnings?.Add(
                    "Could not load AC_CCS_Locomotion_Base.controller from the package; assign it on the model's Animator.");
                return;
            }

            Undo.RecordObject(pick.PrimaryAnimator, "CCS: assign locomotion base on visual Animator");
            pick.PrimaryAnimator.runtimeAnimatorController = locomotion;
            pick.PrimaryAnimator.applyRootMotion = false;
            pick.PrimaryAnimator.enabled = true;
            EditorUtility.SetDirty(pick.PrimaryAnimator);
        }

        /// <summary>
        /// Assigns <c>AC_CCS_BasicLocomotion_Minimal.controller</c> to the primary visual <see cref="Animator"/>.
        /// When <paramref name="forceReplaceController"/> is false, skips if a controller is already assigned.
        /// </summary>
        internal static void TryAssignPackageMinimalLocomotionOnPrimaryVisual(
            Transform visualRoot,
            Transform hierarchyRootForLogging,
            List<string> warnings,
            bool forceReplaceController)
        {
            if (visualRoot == null)
            {
                return;
            }

            PrimaryAnimatorResult pick = SelectPrimaryLocomotionAnimator(visualRoot, hierarchyRootForLogging);
            if (pick.PrimaryAnimator == null)
            {
                warnings?.Add(
                    "No Animator under VisualRoot; add a Humanoid Animator on the model or assign AC_CCS_BasicLocomotion_Minimal manually. ("
                    + pick.SelectionSummary
                    + ")");
                return;
            }

            if (pick.PrimaryAnimator.runtimeAnimatorController != null && !forceReplaceController)
            {
                return;
            }

            RuntimeAnimatorController locomotion =
                CCS_InputAssetUtility.TryLoadBasicLocomotionMinimalAnimatorController(out string _);
            if (locomotion == null)
            {
                warnings?.Add(
                    "Could not load AC_CCS_BasicLocomotion_Minimal.controller; build it via CCS/Character Controller/Authoring/Build AC_CCS_BasicLocomotion_Minimal.");
                return;
            }

            Undo.RecordObject(pick.PrimaryAnimator, "CCS: assign minimal locomotion on visual Animator");
            pick.PrimaryAnimator.runtimeAnimatorController = locomotion;
            pick.PrimaryAnimator.applyRootMotion = false;
            pick.PrimaryAnimator.enabled = true;
            EditorUtility.SetDirty(pick.PrimaryAnimator);
        }

        /// <summary>
        /// Applies Phase 1 rules to the <b>primary</b> Animator only. Secondary Animators are never touched.
        /// </summary>
        internal static LocomotionAnimatorApplyResult ApplyPrimaryLocomotionRules(
            Animator primaryAnimator,
            bool preserveExistingControllers,
            bool preferMinimalLocomotionController = false)
        {
            LocomotionAnimatorApplyResult result = default;
            if (primaryAnimator == null)
            {
                return result;
            }

            bool hadController = primaryAnimator.runtimeAnimatorController != null;

            if (preserveExistingControllers)
            {
                if (hadController)
                {
                    result.PreservedExistingOnPrimary = true;
                    Undo.RecordObject(primaryAnimator, "CCS Basic: primary locomotion root motion");
                    primaryAnimator.applyRootMotion = false;
                    EditorUtility.SetDirty(primaryAnimator);
                    return result;
                }

                RuntimeAnimatorController fallback =
                    CCS_InputAssetUtility.TryLoadIdleAnimatorController(out string path);
                if (fallback == null)
                {
                    Undo.RecordObject(primaryAnimator, "CCS Basic: primary locomotion root motion");
                    primaryAnimator.applyRootMotion = false;
                    EditorUtility.SetDirty(primaryAnimator);
                    return result;
                }

                Undo.RecordObject(primaryAnimator, "CCS Basic: assign fallback idle on primary");
                primaryAnimator.runtimeAnimatorController = fallback;
                primaryAnimator.applyRootMotion = false;
                primaryAnimator.enabled = true;
                EditorUtility.SetDirty(primaryAnimator);
                result.AssignedFallbackController = true;
                result.FallbackControllerPathOrEmpty = path ?? string.Empty;
                return result;
            }

            // Preservation OFF: only the primary may receive a forced package controller.
            RuntimeAnimatorController packageController = null;
            string packagePath = null;
            if (preferMinimalLocomotionController)
            {
                packageController =
                    CCS_InputAssetUtility.TryLoadBasicLocomotionMinimalAnimatorController(out packagePath);
            }

            if (packageController == null)
            {
                packageController =
                    CCS_InputAssetUtility.TryLoadIdleAnimatorController(out packagePath);
            }

            if (packageController != null)
            {
                Undo.RecordObject(primaryAnimator, "CCS Basic: assign package controller on primary");
                primaryAnimator.runtimeAnimatorController = packageController;
                result.AssignedFallbackController = true;
                result.FallbackControllerPathOrEmpty = packagePath ?? string.Empty;
            }

            primaryAnimator.applyRootMotion = false;
            primaryAnimator.enabled = true;
            EditorUtility.SetDirty(primaryAnimator);
            return result;
        }

        #endregion

        #region Private Methods

        private static int GetHumanoidTier(Animator animator)
        {
            if (animator == null)
            {
                return 0;
            }

            if (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman)
            {
                return 3;
            }

            if (animator.avatar != null && animator.avatar.isValid)
            {
                return 2;
            }

            return 1;
        }

        private static int CountRenderersInHierarchy(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            return renderers != null ? renderers.Length : 0;
        }

        private static int GetTransformDepth(Transform leaf, Transform ancestor)
        {
            if (leaf == null)
            {
                return 9999;
            }

            int depth = 0;
            Transform t = leaf;
            while (t != null && t != ancestor)
            {
                depth++;
                t = t.parent;
            }

            return depth;
        }

        private static string BuildTransformPath(Transform leaf, Transform ancestor)
        {
            if (leaf == null)
            {
                return "(null)";
            }

            if (ancestor == null || leaf == ancestor)
            {
                return leaf.name;
            }

            var parts = new List<string>(8);
            Transform walk = leaf;
            while (walk != null && walk != ancestor)
            {
                parts.Add(walk.name);
                walk = walk.parent;
            }

            parts.Reverse();
            return parts.Count == 0 ? leaf.name : string.Join("/", parts);
        }

        #endregion
    }
}
