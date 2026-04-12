using CCS.CharacterController;
using UnityEditor;
using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraRigEditor
// Purpose: Binds default CCS_CameraProfile when missing (after Hub / broken asset repair).
// Placement: Editor / CustomEditor for CCS_CameraRig.
// Author: James Schilz
// Date: 2026-04-11
//==============================================================================

namespace CCS.CharacterController.Editor
{
    [CustomEditor(typeof(CCS_CameraRig))]
    public sealed class CCS_CameraRigEditor : UnityEditor.Editor
    {
        private bool attemptedDefaultProfileBind;

        private void OnEnable()
        {
            attemptedDefaultProfileBind = false;
        }

        public override void OnInspectorGUI()
        {
            if (!attemptedDefaultProfileBind)
            {
                attemptedDefaultProfileBind = true;
                TryBindDefaultCameraProfile();
            }

            DrawDefaultInspector();
        }

        private void TryBindDefaultCameraProfile()
        {
            serializedObject.Update();
            SerializedProperty profileProperty = serializedObject.FindProperty("cameraProfile");
            if (profileProperty == null || profileProperty.objectReferenceValue != null)
            {
                return;
            }

            CCS_CameraProfile loaded = CCS_CameraProfileAssetUtility.TryLoadDefaultProfileAfterRepair();
            if (loaded == null)
            {
                return;
            }

            profileProperty.objectReferenceValue = loaded;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
