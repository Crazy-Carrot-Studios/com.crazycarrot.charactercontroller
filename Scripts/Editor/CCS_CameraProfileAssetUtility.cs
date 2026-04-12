using System.IO;
using CCS.CharacterController;
using UnityEditor;
using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraProfileAssetUtility
// Purpose: Validates default camera profile import (script binding) and can recreate the baseline asset.
// Placement: Editor only.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController.Editor
{
    /// <summary>
    /// Diagnostics and repair for <see cref="CCS_CameraProfile"/> assets after Hub/bootstrap copies.
    /// </summary>
    internal static class CCS_CameraProfileAssetUtility
    {
        /// <summary>
        /// If the default profile file exists but does not load as <see cref="CCS_CameraProfile"/>, logs a clear error (broken script / assembly binding).
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
                    "[CCS_CameraProfileAssetUtility] Camera profile file exists on disk but Unity did not import it as an asset: "
                    + path
                    + ". Reimport the folder or run CCS → Character Controller → Profiles → Recreate Default Follow Camera Profile.",
                    context);
                return;
            }

            Debug.LogError(
                "[CCS_CameraProfileAssetUtility] Camera profile asset exists but is NOT a valid CCS_CameraProfile (object picker will not list it). "
                + "This usually means the YAML script binding is wrong (e.g. m_EditorClassIdentifier still says Assembly-CSharp while the script is in assembly "
                + "CCS.CharacterController.Runtime). Fix the .asset or use Recreate Default Follow Camera Profile. Path: "
                + path,
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
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(fullPath))
            {
                AssetDatabase.DeleteAsset(path);
            }

            CCS_CameraProfile profile = CCS_CameraProfile.CreateBaselineDefaultsInstance();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(profile);
            Debug.Log(
                "[CCS_CameraProfileAssetUtility] Recreated default follow camera profile at " + path,
                profile);
        }
    }
}
