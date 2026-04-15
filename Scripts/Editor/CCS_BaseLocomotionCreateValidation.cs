using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_BaseLocomotionCreateValidation
// Purpose: Fail-fast checks for Phase 1 Create flow (humanoid visual, Cinemachine, input asset).
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-15
//==============================================================================

namespace CCS.CharacterController.Editor
{
    internal static class CCS_BaseLocomotionCreateValidation
    {
        /// <summary>
        /// Validates that the package input actions asset exists and loads.
        /// </summary>
        internal static bool TryValidatePackageInputActionsPresent(out string errorMessage)
        {
            errorMessage = null;
            string path = CCS_InputAssetUtility.ResolvedPackageInputActionsPath;
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null)
            {
                errorMessage = "Input actions asset missing at '" + path + "'. Restore package files or run input setup.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that <paramref name="controllerTemplate"/> is a prefab with VisualRoot and CameraTargets children.
        /// </summary>
        internal static bool TryValidateBasicControllerTemplateHierarchy(
            GameObject controllerTemplate,
            out string errorMessage)
        {
            errorMessage = null;
            if (controllerTemplate == null)
            {
                errorMessage = "Controller template is not assigned.";
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabAsset(controllerTemplate))
            {
                errorMessage = "Controller template must be a prefab asset.";
                return false;
            }

            Transform root = controllerTemplate.transform;
            if (root.Find("VisualRoot") == null)
            {
                errorMessage = "Controller template must contain child 'VisualRoot'.";
                return false;
            }

            if (root.Find("CameraTargets/CameraFollowTarget") == null
                || root.Find("CameraTargets/CameraLookTarget") == null)
            {
                errorMessage = "Controller template must contain 'CameraTargets/CameraFollowTarget' and 'CameraTargets/CameraLookTarget'.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Appends humanoid visual, Cinemachine, input, and template hierarchy checks used to gate the Create button.
        /// </summary>
        internal static void AppendPhase1CreateBlockingErrors(
            GameObject controllerTemplate,
            GameObject visualModelTemplate,
            ICollection<string> errors)
        {
            if (errors == null)
            {
                return;
            }

            if (!TryValidatePackageInputActionsPresent(out string inputErr))
            {
                errors.Add(inputErr);
            }

            if (!TryValidateBasicControllerTemplateHierarchy(controllerTemplate, out string templateErr))
            {
                errors.Add(templateErr);
            }

            if (!TryValidateVisualPrefabForPhase1Humanoid(visualModelTemplate, out string visualErr))
            {
                errors.Add(visualErr);
            }
        }

        /// <summary>
        /// Validates that <paramref name="visualPrefab"/> resolves to a primary Humanoid Animator with a valid Avatar.
        /// Uses prefab contents (edit isolation) when the asset is a prefab or model.
        /// </summary>
        internal static bool TryValidateVisualPrefabForPhase1Humanoid(
            GameObject visualPrefab,
            out string errorMessage)
        {
            errorMessage = null;
            if (visualPrefab == null)
            {
                errorMessage = "Visual Model Template is not assigned.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(visualPrefab);
            if (string.IsNullOrEmpty(path))
            {
                errorMessage = "Visual Model Template must be a project asset (prefab or model), not a scene-only object.";
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
            {
                errorMessage = "Could not load prefab contents for validation: " + path;
                return false;
            }

            try
            {
                CCS_AnimatorSetupUtility.PrimaryAnimatorResult pick =
                    CCS_AnimatorSetupUtility.SelectPrimaryLocomotionAnimator(contents.transform, contents.transform);
                if (pick.PrimaryAnimator == null)
                {
                    errorMessage =
                        "No Animator found under the visual prefab. Add a Humanoid rig with an Animator. ("
                        + pick.SelectionSummary
                        + ")";
                    return false;
                }

                if (!pick.PrimaryAnimator.isHuman)
                {
                    errorMessage =
                        "Primary Animator on the visual is not Humanoid. Set the model Rig to Humanoid in the FBX importer.";
                    return false;
                }

                if (pick.PrimaryAnimator.avatar == null || !pick.PrimaryAnimator.avatar.isValid)
                {
                    errorMessage =
                        "Primary Animator has no valid Humanoid Avatar. Reimport the model or fix the rig definition.";
                    return false;
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
