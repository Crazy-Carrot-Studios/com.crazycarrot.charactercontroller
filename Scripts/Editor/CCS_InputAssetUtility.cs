using System.IO;
using CCS.CharacterController;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

//==============================================================================
// CCS Script Summary
// Name: CCS_InputAssetUtility
// Purpose: Ensures the package CCS_CharacterController_InputActions asset exists,
//          validates maps/actions, and can generate a default asset on disk.
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    internal static class CCS_InputAssetUtility
    {
        // Loads the package input asset or creates it on disk when allowed; validates required actions.
        internal static bool EnsurePackageInputAssetExists(bool createIfMissing, bool enableDebugLogs, out InputActionAsset asset)
        {
            string inputPath = CCS_CharacterControllerPackagePaths.ResolvedPackageInputActionsPath;
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
