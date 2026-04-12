using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_CameraProfile
// Purpose: Default Cinemachine third-person tuning (lens, orbit, damping, input) applied by CCS_CameraRig.
// Required components: None (ScriptableObject asset).
// Placement: Scripts/Runtime/Scriptable Objects (same assembly as CCS_CameraRig). Default asset: Scripts/Profiles/CCS_Default_TP_Follow_CameraProfile.asset.
// Author: James Schilz
// Date: 2026-04-10
//==============================================================================

namespace CCS.CharacterController
{
    [CreateAssetMenu(
        fileName = "CCS_CameraProfile",
        menuName = "CCS/Character Controller/Profiles/Camera Profile")]
    public sealed class CCS_CameraProfile : ScriptableObject
    {
        [Header("Lens")]
        [Tooltip("Vertical field of view for the gameplay camera.")]
        public float fieldOfView = 58f;

        [Tooltip("Near clip plane.")]
        public float nearClipPlane = 0.1f;

        [Tooltip("Far clip plane.")]
        public float farClipPlane = 5000f;

        [Header("Orbit")]
        [Tooltip("Orbit radius in meters from the follow target.")]
        public float orbitRadius = 4f;

        [Tooltip("Local target offset for the orbit body.")]
        public Vector3 targetOffset = new Vector3(0.18f, 0f, 0f);

        [Tooltip("Vertical orbit minimum in degrees.")]
        public float verticalAxisMin = -20f;

        [Tooltip("Vertical orbit center in degrees.")]
        public float verticalAxisCenter = 12f;

        [Tooltip("Vertical orbit maximum in degrees.")]
        public float verticalAxisMax = 80f;

        [Tooltip("Whether the vertical axis wraps.")]
        public bool verticalWrap;

        [Tooltip("Whether the horizontal axis wraps.")]
        public bool horizontalWrap = true;

        [Header("Damping")]
        [Tooltip("Position damping applied by Cinemachine Orbital Follow.")]
        public Vector3 positionDamping = new Vector3(0.22f, 0.22f, 0.22f);

        [Tooltip("Rotation damping applied by Cinemachine Orbital Follow.")]
        public Vector3 rotationDamping = new Vector3(0.28f, 0.28f, 0.28f);

        [Tooltip("Composer damping X/Y.")]
        public Vector2 composerDamping = new Vector2(0.32f, 0.28f);

        [Header("Framing")]
        [Tooltip("Optional aim/framing offset for the Rotation Composer.")]
        public Vector3 composerTargetOffset;

        [Header("Input")]
        [Tooltip("Mouse orbit speed applied to Cinemachine Look Orbit X/Y.")]
        [Min(0.01f)]
        public float mouseOrbitSpeed = 0.26f;

        [Header("Priority")]
        [Tooltip("Cinemachine camera priority.")]
        public int priority = 20;

        /// <summary>
        /// Creates a runtime instance with the same tuning as <c>CCS_Default_TP_Follow_CameraProfile</c> (for editor repair / factory / play-mode fallback).
        /// </summary>
        /// <param name="objectName">Optional <see cref="Object.name"/>; defaults to the standard default asset name.</param>
        public static CCS_CameraProfile CreateBaselineDefaultsInstance(string objectName = null)
        {
            CCS_CameraProfile profile = CreateInstance<CCS_CameraProfile>();
            profile.fieldOfView = 58f;
            profile.nearClipPlane = 0.1f;
            profile.farClipPlane = 5000f;
            profile.orbitRadius = 4f;
            profile.targetOffset = new Vector3(0.18f, 0f, 0f);
            profile.verticalAxisMin = -20f;
            profile.verticalAxisCenter = 12f;
            profile.verticalAxisMax = 80f;
            profile.verticalWrap = false;
            profile.horizontalWrap = true;
            profile.positionDamping = new Vector3(0.22f, 0.22f, 0.22f);
            profile.rotationDamping = new Vector3(0.28f, 0.28f, 0.28f);
            profile.composerDamping = new Vector2(0.32f, 0.28f);
            profile.composerTargetOffset = Vector3.zero;
            profile.mouseOrbitSpeed = 0.26f;
            profile.priority = 20;
            profile.name = string.IsNullOrEmpty(objectName)
                ? "CCS_Default_TP_Follow_CameraProfile"
                : objectName;
            return profile;
        }
    }
}
