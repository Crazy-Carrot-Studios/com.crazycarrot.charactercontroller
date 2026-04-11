#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CCS.CharacterController
{
    /// <summary>
    /// Paths for package assets. Supports UPM install (<see cref="UpmRoot"/>) and embedded dev layout (<see cref="EmbeddedRoot"/>).
    /// Use editor-only resolved paths for <c>AssetDatabase</c>; runtime code must not rely on filesystem layout.
    /// </summary>
    public static class CCS_CharacterControllerPackagePaths
    {
        /// <summary>Manifest / documentation package id.</summary>
        public const string PackageId = "com.crazycarrot.charactercontroller";

        /// <summary>Typical root when installed via Package Manager (git or registry).</summary>
        public const string UpmRoot = "Packages/com.crazycarrot.charactercontroller";

        /// <summary>Root when the package lives as an embedded folder under Assets (local dev).</summary>
        public const string EmbeddedRoot = "Assets/CCS/CharacterController";

        /// <summary>Path under package root to default third-person camera profile.</summary>
        public const string RelativeDefaultThirdPersonFollowCameraProfile =
            "Scripts/Profiles/CCS_Default_TP_Follow_CameraProfile.asset";

        /// <summary>Path under package root to base locomotion Animator controller (BaseLocomotion baseline).</summary>
        public const string RelativeDefaultBaseLocomotionAnimatorController =
            "Animations/Controllers/CCS_Base_locomotion_controller.controller";

        /// <summary>Path under package root to shared Input System asset.</summary>
        public const string RelativeInputActions = "Settings/Input/CCS_CharacterController_InputActions.inputactions";

#if UNITY_EDITOR
        /// <summary>UPM folder if present, otherwise embedded dev folder, otherwise UPM path for messages.</summary>
        public static string GetResolvedPackageRoot()
        {
            if (AssetDatabase.IsValidFolder(UpmRoot))
            {
                return UpmRoot;
            }

            if (AssetDatabase.IsValidFolder(EmbeddedRoot))
            {
                return EmbeddedRoot;
            }

            return UpmRoot;
        }

        /// <summary>Full project path for <c>AssetDatabase.LoadAssetAtPath</c> (editor).</summary>
        public static string ResolvedDefaultThirdPersonFollowCameraProfilePath =>
            $"{GetResolvedPackageRoot()}/{RelativeDefaultThirdPersonFollowCameraProfile}";

        /// <summary>Full project path for default base locomotion controller (editor).</summary>
        public static string ResolvedDefaultBaseLocomotionAnimatorControllerPath =>
            $"{GetResolvedPackageRoot()}/{RelativeDefaultBaseLocomotionAnimatorController}";

        /// <summary>Full project path for package input actions JSON (editor).</summary>
        public static string ResolvedPackageInputActionsPath =>
            $"{GetResolvedPackageRoot()}/{RelativeInputActions}";
#endif
    }
}
