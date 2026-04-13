#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CCS.CharacterController
{
    // Resolves package root for UPM install (UpmRoot) or Hub-embedded copy (EmbeddedRoot).
    public static class CCS_CharacterControllerPackagePaths
    {
        public const string PackageId = "com.crazycarrot.charactercontroller";

        public const string UpmRoot = "Packages/com.crazycarrot.charactercontroller";

        public const string EmbeddedRoot = "Assets/CCS/CharacterController";

        public const string RelativeInputActions = "Settings/Input/CCS_CharacterController_InputActions.inputactions";

#if UNITY_EDITOR
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

        public static string ResolvedPackageInputActionsPath =>
            $"{GetResolvedPackageRoot()}/{RelativeInputActions}";
#endif
    }
}
