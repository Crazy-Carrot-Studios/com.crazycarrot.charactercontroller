using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_InputAssetUtility
// Purpose: Resolves package root (UPM or Assets/CCS/CharacterController); paths for input actions,
//          CCS_Idle_Controller, AC_CCS_Locomotion_Base; ensures input asset exists, validates maps/actions.
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    internal static class CCS_InputAssetUtility
    {
        private const string UpmPackageRoot = "Packages/com.crazycarrot.charactercontroller";
        private const string EmbeddedPackageRoot = "Assets/CCS/CharacterController";
        private const string RelativeInputActions = "Settings/Input/CCS_CharacterController_InputActions.inputactions";

        private const string RelativeIdleAnimatorController =
            "Animations/Controllers/CCS_Idle_Controller.controller";

        private const string RelativeLocomotionBaseAnimatorController =
            "Animations/Controllers/AC_CCS_Locomotion_Base.controller";

        private const string RelativeBasicControllerTemplatePrefab =
            "Prefabs/PF_CCS_BasicController_Template.prefab";

        private const string RelativeStarterVisualPrefab =
            "Characters/CCS_StarterCharacter/Prefabs/PF_CCS_StarterCharacter_Visual.prefab";

        internal static string GetResolvedPackageRoot()
        {
            if (AssetDatabase.IsValidFolder(UpmPackageRoot))
            {
                return UpmPackageRoot;
            }

            if (AssetDatabase.IsValidFolder(EmbeddedPackageRoot))
            {
                return EmbeddedPackageRoot;
            }

            return UpmPackageRoot;
        }

        internal static string ResolvedPackageInputActionsPath =>
            $"{GetResolvedPackageRoot()}/{RelativeInputActions}";

        internal static string ResolvedIdleAnimatorControllerPath =>
            $"{GetResolvedPackageRoot()}/{RelativeIdleAnimatorController}";

        internal static string ResolvedLocomotionBaseAnimatorControllerPath =>
            $"{GetResolvedPackageRoot()}/{RelativeLocomotionBaseAnimatorController}";

        internal static string ResolvedBasicLocomotionMinimalAnimatorControllerPath =>
            $"{GetResolvedPackageRoot()}/{CCS_BasicLocomotionMinimalControllerAuthoring.RelativeControllerPath}";

        /// <summary>
        /// Package-relative path to <c>PF_CCS_BasicController_Template.prefab</c> (UPM or embedded <c>Assets/CCS/CharacterController</c>).
        /// </summary>
        internal static string ResolvedBasicControllerTemplatePrefabPath =>
            $"{GetResolvedPackageRoot()}/{RelativeBasicControllerTemplatePrefab}";

        /// <summary>
        /// Package-relative path to <c>PF_CCS_StarterCharacter_Visual.prefab</c>.
        /// </summary>
        internal static string ResolvedStarterVisualPrefabPath =>
            $"{GetResolvedPackageRoot()}/{RelativeStarterVisualPrefab}";

        /// <summary>
        /// Ensures every folder segment exists for an asset path under <c>Assets/</c> or <c>Packages/</c>.
        /// </summary>
        internal static bool EnsureFolderHierarchyExistsForAssetPath(string assetPath)
        {
            string normalized = assetPath.Replace("\\", "/");
            int lastSlash = normalized.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return true;
            }

            string folder = normalized.Substring(0, lastSlash);
            if (AssetDatabase.IsValidFolder(folder))
            {
                return true;
            }

            string rootPrefix;
            string relative;
            if (folder.StartsWith("Packages/", System.StringComparison.Ordinal))
            {
                rootPrefix = "Packages";
                relative = folder.Length > "Packages/".Length ? folder.Substring("Packages/".Length) : string.Empty;
            }
            else if (folder.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                rootPrefix = "Assets";
                relative = folder.Length > "Assets/".Length ? folder.Substring("Assets/".Length) : string.Empty;
            }
            else
            {
                return false;
            }

            string[] parts = relative.Split('/');
            string current = rootPrefix;
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

        // Expected path first; if missing (e.g. wrong folder after copy), finds CCS_Idle_Controller under this package.
        internal static RuntimeAnimatorController TryLoadIdleAnimatorController(out string usedAssetPath)
        {
            usedAssetPath = ResolvedIdleAnimatorControllerPath;
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(usedAssetPath);
            if (controller != null)
            {
                return controller;
            }

            string[] guids = AssetDatabase.FindAssets("CCS_Idle_Controller t:AnimatorController");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string normalized = path.Replace('\\', '/');
                if (!normalized.Contains("CCS/CharacterController")
                    && !normalized.Contains("com.crazycarrot.charactercontroller"))
                {
                    continue;
                }

                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                if (controller != null)
                {
                    usedAssetPath = path;
                    return controller;
                }
            }

            usedAssetPath = null;
            return null;
        }

        // Expected path first; if missing, finds AC_CCS_Locomotion_Base under this package.
        internal static RuntimeAnimatorController TryLoadLocomotionBaseAnimatorController(out string usedAssetPath)
        {
            usedAssetPath = ResolvedLocomotionBaseAnimatorControllerPath;
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(usedAssetPath);
            if (controller != null)
            {
                return controller;
            }

            string[] guids = AssetDatabase.FindAssets("AC_CCS_Locomotion_Base t:AnimatorController");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string normalized = path.Replace('\\', '/');
                if (!normalized.Contains("CCS/CharacterController")
                    && !normalized.Contains("com.crazycarrot.charactercontroller"))
                {
                    continue;
                }

                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                if (controller != null)
                {
                    usedAssetPath = path;
                    return controller;
                }
            }

            usedAssetPath = null;
            return null;
        }

        /// <summary>
        /// Loads <c>AC_CCS_BasicLocomotion_Minimal.controller</c> from the expected package path, with FindAssets fallback.
        /// Does not build the asset; call <see cref="CCS_BasicLocomotionMinimalControllerAuthoring.EnsureMinimalControllerExists"/> first when authoring.
        /// </summary>
        internal static RuntimeAnimatorController TryLoadBasicLocomotionMinimalAnimatorController(out string usedAssetPath)
        {
            usedAssetPath = ResolvedBasicLocomotionMinimalAnimatorControllerPath;
            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(usedAssetPath);
            if (controller != null)
            {
                return controller;
            }

            string[] guids = AssetDatabase.FindAssets("AC_CCS_BasicLocomotion_Minimal t:AnimatorController");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string normalized = path.Replace('\\', '/');
                if (!normalized.Contains("CCS/CharacterController")
                    && !normalized.Contains("com.crazycarrot.charactercontroller"))
                {
                    continue;
                }

                controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                if (controller != null)
                {
                    usedAssetPath = path;
                    return controller;
                }
            }

            usedAssetPath = null;
            return null;
        }

        // Loads the package input asset or creates it on disk when allowed; validates required actions.
        internal static bool EnsurePackageInputAssetExists(bool createIfMissing, bool enableDebugLogs, out InputActionAsset asset)
        {
            string inputPath = ResolvedPackageInputActionsPath;
            asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            if (asset != null)
            {
                ValidatePackageInputAsset(asset, enableDebugLogs);
                return true;
            }

            if (!createIfMissing)
            {
                Debug.LogError(
                    $"[CCS_InputAssetUtility] Input asset not found at '{inputPath}'. Enable auto-create in the setup wizard or restore the package file.",
                    null);
                return false;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, inputPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            InputActionAsset built = BuildDefaultPackageInputActionAsset();
            string json = built.ToJson();
            Object.DestroyImmediate(built);

            try
            {
                File.WriteAllText(fullPath, json);
            }
            catch (IOException exception)
            {
                Debug.LogError(
                    $"[CCS_InputAssetUtility] Failed to write input asset: {exception.Message}",
                    null);
                return false;
            }

            AssetDatabase.ImportAsset(inputPath, ImportAssetOptions.ForceUpdate);
            asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            if (asset == null)
            {
                Debug.LogError(
                    "[CCS_InputAssetUtility] Import completed but the InputActionAsset could not be loaded.",
                    null);
                return false;
            }

            ValidatePackageInputAsset(asset, enableDebugLogs);
            Debug.Log(
                $"[CCS_InputAssetUtility] Created package input asset at '{inputPath}'.",
                asset);

            return true;
        }

        // Checks Gameplay/UI/Debug maps and required actions; logs errors or optional success.
        internal static void ValidatePackageInputAsset(InputActionAsset asset, bool enableDebugLogs)
        {
            if (asset == null)
            {
                return;
            }

            bool valid = true;
            InputActionMap gameplay = asset.FindActionMap("Gameplay", throwIfNotFound: false);
            if (gameplay == null)
            {
                Debug.LogError("[CCS_InputAssetUtility] Missing action map 'Gameplay'.", asset);
                valid = false;
            }
            else
            {
                valid &= LogIfActionMissing(asset, gameplay, "Move", required: true);
                valid &= LogIfActionMissing(asset, gameplay, "Look", required: true);
                valid &= LogIfActionMissing(asset, gameplay, "Sprint", required: true);
                LogIfActionMissing(asset, gameplay, "Jump", required: false);
                LogIfActionMissing(asset, gameplay, "Crouch", required: false);
                LogIfActionMissing(asset, gameplay, "Interact", required: false);
            }

            if (asset.FindActionMap("UI", throwIfNotFound: false) == null)
            {
                Debug.LogWarning("[CCS_InputAssetUtility] Missing action map 'UI'. Add it for UI workflows.", asset);
            }

            if (asset.FindActionMap("Debug", throwIfNotFound: false) == null)
            {
                Debug.LogWarning("[CCS_InputAssetUtility] Missing action map 'Debug'.", asset);
            }

            if (enableDebugLogs && valid)
            {
                Debug.Log(
                    "[CCS_InputAssetUtility] Input asset validation passed (Gameplay: Move, Look, Sprint required).",
                    asset);
            }
        }

        // Logs error (baseline required) or warning (optional) when an action is missing.
        private static bool LogIfActionMissing(Object context, InputActionMap map, string actionName, bool required)
        {
            if (map.FindAction(actionName, throwIfNotFound: false) != null)
            {
                return true;
            }

            if (required)
            {
                Debug.LogError($"[CCS_InputAssetUtility] Required Gameplay action '{actionName}' is missing.", context);
                return false;
            }

            Debug.LogWarning(
                $"[CCS_InputAssetUtility] Optional Gameplay action '{actionName}' is missing (baseline does not require it).",
                context);
            return true;
        }

        // Constructs the default CCS maps and bindings when generating a missing .inputactions file.
        private static InputActionAsset BuildDefaultPackageInputActionAsset()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "CCS_CharacterController_InputActions";

            asset.AddControlScheme("Keyboard&Mouse")
                .WithBindingGroup("Keyboard&Mouse")
                .WithRequiredDevice("<Keyboard>")
                .WithRequiredDevice("<Mouse>");

            asset.AddControlScheme("Gamepad")
                .WithBindingGroup("Gamepad")
                .WithRequiredDevice("<Gamepad>");

            InputActionMap gameplay = asset.AddActionMap("Gameplay");

            InputAction move = gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", groups: "Keyboard&Mouse")
                .With("Down", "<Keyboard>/s", groups: "Keyboard&Mouse")
                .With("Left", "<Keyboard>/a", groups: "Keyboard&Mouse")
                .With("Right", "<Keyboard>/d", groups: "Keyboard&Mouse");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", groups: "Keyboard&Mouse")
                .With("Down", "<Keyboard>/downArrow", groups: "Keyboard&Mouse")
                .With("Left", "<Keyboard>/leftArrow", groups: "Keyboard&Mouse")
                .With("Right", "<Keyboard>/rightArrow", groups: "Keyboard&Mouse");

            InputAction look = gameplay.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            look.AddBinding("<Mouse>/delta", groups: "Keyboard&Mouse");
            look.AddBinding("<Gamepad>/rightStick", groups: "Gamepad");

            InputAction jump = gameplay.AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
            jump.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");

            InputAction sprint = gameplay.AddAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift", groups: "Keyboard&Mouse");
            sprint.AddBinding("<Gamepad>/leftStickPress", groups: "Gamepad");

            InputAction crouch = gameplay.AddAction("Crouch", InputActionType.Button);
            crouch.AddBinding("<Keyboard>/leftCtrl", groups: "Keyboard&Mouse");
            crouch.AddBinding("<Gamepad>/buttonEast", groups: "Gamepad");

            InputAction interact = gameplay.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e", groups: "Keyboard&Mouse");
            interact.AddBinding("<Gamepad>/buttonWest", groups: "Gamepad");

            InputActionMap ui = asset.AddActionMap("UI");
            ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter", groups: "Keyboard&Mouse");
            ui.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape", groups: "Keyboard&Mouse");

            InputActionMap debug = asset.AddActionMap("Debug");
            debug.AddAction("ToggleDebugOverlay", InputActionType.Button, "<Keyboard>/f3", groups: "Keyboard&Mouse");
            debug.AddAction("ToggleVerboseLogs", InputActionType.Button, "<Keyboard>/f4", groups: "Keyboard&Mouse");

            return asset;
        }
    }
}
