using System.IO;
using CCS.CharacterController;
using UnityEditor;
using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraProfileAssetUtility
// Purpose: Cross-project camera profile health — script/assembly binding repair, recreate, validation.
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    /// <summary>
    /// Diagnostics and automatic repair for <see cref="CCS_CameraProfile"/> assets after Hub/bootstrap copies
    /// (broken m_Script GUID, wrong m_EditorClassIdentifier, etc.).
    /// </summary>
    internal static class CCS_CameraProfileAssetUtility
    {
        private static readonly string[] KnownCameraProfileAssetsRelativeToPackageRoot =
        {
            "Scripts/Profiles/camera/CCS_Default_TP_Follow_CameraProfile.asset",
            "Scripts/Profiles/CameraProfiles/CCS_TP_Default_CameraProfile.asset",
            "Scripts/Profiles/camera/CCS_CameraProfile_DefaultThirdPerson.asset",
        };

        private static bool s_editorHealthPassRan;

        [InitializeOnLoadMethod]
        private static void EditorStartupHealthPass()
        {
            EditorApplication.delayCall += RunDeferredHealthPass;
        }

        private static void RunDeferredHealthPass()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (s_editorHealthPassRan)
            {
                return;
            }

            s_editorHealthPassRan = true;
            TryRepairKnownCameraProfileAssets();
        }

        /// <summary>
        /// Wizard / tools: repair disk assets, then log if default path is still broken.
        /// </summary>
        internal static void RunCameraProfileHealthPass(Object context)
        {
            TryRepairKnownCameraProfileAssets();
            LogIfDefaultProfileAssetBroken(context);
        }

        /// <summary>
        /// Attempts to load the default follow profile after a repair pass.
        /// </summary>
        internal static CCS_CameraProfile TryLoadDefaultProfileAfterRepair()
        {
            TryRepairKnownCameraProfileAssets();
            return AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(
                CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath);
        }

        /// <summary>
        /// Rebinds <c>m_Script</c> to the compiled <see cref="CCS_CameraProfile"/> MonoScript, or recreates the asset file.
        /// </summary>
        internal static void TryRepairKnownCameraProfileAssets()
        {
            string packageRoot = CCS_CharacterControllerPackagePaths.GetResolvedPackageRoot();
            if (!AssetDatabase.IsValidFolder(packageRoot))
            {
                return;
            }

            MonoScript cameraProfileScript = FindCameraProfileMonoScript();
            if (cameraProfileScript == null)
            {
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            for (int i = 0; i < KnownCameraProfileAssetsRelativeToPackageRoot.Length; i++)
            {
                string assetPath = packageRoot + "/" + KnownCameraProfileAssetsRelativeToPackageRoot[i];
                string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                TryRepairSingleProfileAsset(assetPath, fullPath, cameraProfileScript);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void TryRepairSingleProfileAsset(string assetPath, string fullPath, MonoScript cameraProfileScript)
        {
            if (AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(assetPath) != null)
            {
                return;
            }

            Object untyped = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (untyped == null)
            {
                return;
            }

            if (TryRebindMonoScriptReference(assetPath, cameraProfileScript))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                if (AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(assetPath) != null)
                {
                    Debug.Log("[CCS] Camera profile asset repaired (m_Script rebound): " + assetPath, untyped);
                    return;
                }
            }

            Debug.LogError(
                "[CCS] Camera Profile asset exists but is not a valid CCS_CameraProfile. Check script/assembly binding.",
                untyped);

            TryRecreateProfileAssetAtPath(assetPath, fullPath);
        }

        private static bool TryRebindMonoScriptReference(string assetPath, MonoScript script)
        {
            Object main = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (main == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(main);
            SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
            if (scriptProperty == null)
            {
                return false;
            }

            scriptProperty.objectReferenceValue = script;
            return serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TryRecreateProfileAssetAtPath(string assetPath, string fullPath)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.Refresh();

            string objectName = Path.GetFileNameWithoutExtension(assetPath);
            // Use parameterless factory so bootstrapped projects with an older CCS_CameraProfile (no objectName overload) still compile.
            CCS_CameraProfile profile = CCS_CameraProfile.CreateBaselineDefaultsInstance();
            if (!string.IsNullOrEmpty(objectName))
            {
                profile.name = objectName;
            }

            AssetDatabase.CreateAsset(profile, assetPath);
            Debug.Log("[CCS] Recreated camera profile asset at " + assetPath, profile);
        }

        private static MonoScript FindCameraProfileMonoScript()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith("/CCS_CameraProfile.cs") || path.EndsWith("\\CCS_CameraProfile.cs"))
                {
                    return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                }
            }

            return null;
        }

        /// <summary>
        /// If the default profile file exists but does not load as <see cref="CCS_CameraProfile"/>, logs a clear error.
        /// </summary>
        internal static void LogIfDefaultProfileAssetBroken(Object context)
        {
            string path = CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            string fullPath = Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return;
            }

            Object untyped = AssetDatabase.LoadAssetAtPath<Object>(path);
            CCS_CameraProfile typed = AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(path);
            if (typed != null)
            {
                return;
            }

            if (untyped == null)
            {
                Debug.LogError(
                    "[CCS] Camera profile file exists on disk but Unity did not import it as an asset: "
                    + path
                    + ". Reimport the folder or use CCS → Character Controller → Profiles → Recreate Default Follow Camera Profile.",
                    context);
                return;
            }

            Debug.LogError(
                "[CCS] Camera Profile asset exists but is not a valid CCS_CameraProfile. Check script/assembly binding.",
                untyped);
        }

        [MenuItem("CCS/Character Controller/Profiles/Recreate Default Follow Camera Profile")]
        private static void RecreateDefaultFollowProfile()
        {
            string path = CCS_CharacterControllerPackagePaths.ResolvedDefaultThirdPersonFollowCameraProfilePath;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            string fullPath = Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
            TryRecreateProfileAssetAtPath(path, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CCS_CameraProfile created = AssetDatabase.LoadAssetAtPath<CCS_CameraProfile>(path);
            if (created != null)
            {
                EditorGUIUtility.PingObject(created);
            }
        }
    }
}
