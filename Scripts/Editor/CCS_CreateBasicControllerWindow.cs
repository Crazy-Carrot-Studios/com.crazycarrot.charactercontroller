using System.Collections.Generic;
using System.Text;
using CCS.Editor.CustomInspectors.Branding;
using UnityEditor;
using UnityEngine;

//==============================================================================
// CCS Script Summary
// Name: CCS_CreateBasicControllerWindow
// Purpose: Minimal CCS setup window that assigns and previews a Visual Model Template in a real 3D viewport.
// Placement: Assets/CCS/CharacterController/Scripts/Editor/
// Author: James Schilz
// Date: 2026-04-15
//==============================================================================

namespace CCS.CharacterController.Editor
{
    public sealed class CCS_CreateBasicControllerWindow : CCSEditorWindowBase
    {
        #region Variables

        [Header("Visual Model")]
        [Tooltip(
            "Controller shell prefab (Invector-style): root should carry Unity CharacterController + CCS_CharacterController, "
            + "VisualRoot, and CameraTargets. Default: PF_CCS_BasicController_Template. Missing components are added when you press Create.")]
        [SerializeField]
        private GameObject controllerTemplate;

        [Tooltip("Assign the visual model template prefab or FBX to preview in this window.")]
        [SerializeField]
        private GameObject visualModelTemplate;

        private PreviewRenderUtility previewRenderUtility;
        private GameObject previewInstance;
        private Vector2 previewOrbit = new Vector2(0f, 0f);
        private float previewDistanceMultiplier = 1f;
        private Bounds previewBounds;
        private bool hasValidPreviewBounds;

        private const float WindowWidth = 520f;
        private const float WindowHeight = 560f;
        private const float PreviewHeight = 300f;
        private const float PreviewVerticalFov = 30f;
        private const float MinZoomMultiplier = 0.6f;
        private const float MaxZoomMultiplier = 2.5f;
        private const float OrbitSpeed = 0.35f;
        private const float ZoomSpeed = 0.05f;
        private const float FramePaddingMultiplier = 1.08f;
        private const string DefaultTemplateAssetName = "PF_CCS_BasicController_Template";
        private const string DefaultVisualModelAssetName = "PF_CCS_StarterCharacter_Visual";

        private static string[] BuildDefaultTemplatePreferredPaths()
        {
            return new[]
            {
                CCS_InputAssetUtility.ResolvedBasicControllerTemplatePrefabPath,
                "Assets/CCS/CharacterController/Prefabs/PF_CCS_BasicController_Template.prefab",
            };
        }

        private static string[] BuildDefaultVisualModelPreferredPaths()
        {
            return new[]
            {
                CCS_InputAssetUtility.ResolvedStarterVisualPrefabPath,
                "Assets/CCS/CharacterController/Characters/CCS_StarterCharacter/Prefabs/PF_CCS_StarterCharacter_Visual.prefab",
                "Assets/CCS/CharacterController/Prefabs/PF_CCS_StarterCharacter_Visual.prefab",
            };
        }

        #endregion

        #region CCSEditorWindowBase

        protected override string WindowTitle => "Create Basic Controller";

        protected override void OnEnable()
        {
            base.OnEnable();
            CreatePreviewUtility();
            TryAutoAssignDefaultTemplate();
            TryAutoAssignDefaultVisualModelTemplate();
            RebuildPreviewInstance();
        }

        protected override void DrawBody()
        {
            DrawHeader();
            DrawMainCreatorPanel();
        }

        #endregion

        #region Unity Callbacks

        [MenuItem("CCS/Character Controller/Basic Locomotion/Create Basic Controller", priority = 10)]
        private static void OpenWindow()
        {
            CCS_CreateBasicControllerWindow window = GetWindow<CCS_CreateBasicControllerWindow>(true, "Create Basic Controller", true);
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth, WindowHeight);
            window.ShowUtility();
        }

        private void OnDisable()
        {
            CleanupPreviewInstance();
            CleanupPreviewUtility();
        }

        #endregion

        #region Private Methods

        private void DrawHeader()
        {
            EditorGUILayout.Space(2f);
            CCSEditorStyles.DrawSectionLabel("Visual model preview");
            EditorGUILayout.Space(2f);
            EditorGUILayout.HelpBox(
                "Assign a Visual Model Template. Drag to orbit and scroll to zoom.",
                MessageType.Info);
            EditorGUILayout.Space(4f);
        }

        private void DrawMainCreatorPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawTemplateFieldsInsideMainPanel();
            EditorGUILayout.Space(8f);
            DrawPreviewAreaInsideMainPanel();
            EditorGUILayout.Space(8f);
            DrawCreateButtonInsideMainPanel();
            EditorGUILayout.Space(6f);
            DrawAssetNameInsideMainPanel();
            EditorGUILayout.EndVertical();
        }

        private void DrawTemplateFieldsInsideMainPanel()
        {

            controllerTemplate = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Controller template",
                    "Shell prefab with motor + CCS_CharacterController on the root (package default wires basic locomotion)."),
                controllerTemplate,
                typeof(GameObject),
                false);

            EditorGUI.BeginChangeCheck();

            visualModelTemplate = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Visual Model Template", "Prefab or FBX model to preview in the setup window."),
                visualModelTemplate,
                typeof(GameObject),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                RebuildPreviewInstance();
                Repaint();
            }
        }

        private void DrawPreviewAreaInsideMainPanel()
        {
            Rect previewRect = GUILayoutUtility.GetRect(0f, PreviewHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(previewRect, new Color(0.23f, 0.23f, 0.23f, 1f));

            if (previewRect.width < 1f || previewRect.height < 1f)
            {
                return;
            }

            if (visualModelTemplate == null)
            {
                EditorGUI.HelpBox(previewRect, "Assign a Visual Model Template to preview it here.", MessageType.None);
                return;
            }

            if (previewRenderUtility == null)
            {
                CreatePreviewUtility();
            }

            if (previewInstance == null)
            {
                RebuildPreviewInstance();
            }

            if (previewInstance == null || !hasValidPreviewBounds)
            {
                EditorGUI.HelpBox(previewRect, "Preview could not be generated for this asset.", MessageType.Warning);
                return;
            }

            HandlePreviewInput(previewRect);
            RenderPreview(previewRect);
        }

        private void DrawCreateButtonInsideMainPanel()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            TryAutoAssignDefaultTemplate();
            TryAutoAssignDefaultVisualModelTemplate();

            var creationBlockers = new List<string>(8);
            CCS_BaseLocomotionCreateValidation.AppendPhase1CreateBlockingErrors(
                controllerTemplate,
                visualModelTemplate,
                creationBlockers);

            if (creationBlockers.Count > 0)
            {
                EditorGUILayout.HelpBox(string.Join("\n", creationBlockers), MessageType.Warning);
            }

            bool canCreate = controllerTemplate != null && visualModelTemplate != null && creationBlockers.Count == 0;
            EditorGUI.BeginDisabledGroup(!canCreate);
            if (GUILayout.Button("Create", GUILayout.Width(100f), GUILayout.Height(28f)))
            {
                ExecuteCreateBasicLocomotion();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void ExecuteCreateBasicLocomotion()
        {
            TryAutoAssignDefaultTemplate();
            TryAutoAssignDefaultVisualModelTemplate();

            if (controllerTemplate == null || visualModelTemplate == null)
            {
                EditorUtility.DisplayDialog(
                    "Create Basic Locomotion",
                    "Assign both Controller template and Visual Model Template (or use package defaults).",
                    "OK");
                return;
            }

            var blockers = new List<string>(8);
            CCS_BaseLocomotionCreateValidation.AppendPhase1CreateBlockingErrors(
                controllerTemplate,
                visualModelTemplate,
                blockers);
            if (blockers.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Create Basic Locomotion",
                    "Fix the following before creating:\n\n" + string.Join("\n", blockers),
                    "OK");
                return;
            }

            CCS_BasicControllerCreator.BasicLocomotionCreateResult result = CCS_BasicControllerCreator.CreateBasicLocomotionCharacter(
                controllerTemplate,
                visualModelTemplate,
                "CCSPlayer");

            if (!result.Success || result.PlayerRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Create Basic Locomotion",
                    "Creation failed. See Console for CCS_BasicControllerCreator errors.",
                    "OK");
                return;
            }

            Selection.activeGameObject = result.PlayerRoot;
            EditorGUIUtility.PingObject(result.PlayerRoot);

            var log = new StringBuilder(512);
            log.AppendLine("[CCS_CreateBasicControllerWindow] Basic locomotion character created.");
            log.AppendLine("- Player: " + result.PlayerRoot.name);
            if (result.CameraRigRoot != null)
            {
                log.AppendLine("- Camera rig: " + result.CameraRigRoot.name
                    + (result.UsedExistingCameraRig ? " (existing scene rig wired)" : " (new)"));
            }

            if (result.Warnings != null && result.Warnings.Count > 0)
            {
                log.AppendLine("- Warnings:");
                for (int i = 0; i < result.Warnings.Count; i++)
                {
                    log.AppendLine("  • " + result.Warnings[i]);
                }

                Debug.LogWarning(log.ToString(), result.PlayerRoot);
            }
            else
            {
                Debug.Log(log.ToString(), result.PlayerRoot);
            }
        }

        private void DrawAssetNameInsideMainPanel()
        {
            if (visualModelTemplate == null)
            {
                EditorGUILayout.LabelField("Assigned Asset: None", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField("Assigned Asset: " + visualModelTemplate.name, EditorStyles.miniLabel);
        }

        private void HandlePreviewInput(Rect previewRect)
        {
            Event currentEvent = Event.current;
            if (!previewRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                previewOrbit.x += currentEvent.delta.x * OrbitSpeed;
                previewOrbit.y -= currentEvent.delta.y * OrbitSpeed;
                previewOrbit.y = Mathf.Clamp(previewOrbit.y, -80f, 80f);
                currentEvent.Use();
                Repaint();
            }

            if (currentEvent.type == EventType.ScrollWheel)
            {
                previewDistanceMultiplier += currentEvent.delta.y * ZoomSpeed;
                previewDistanceMultiplier = Mathf.Clamp(previewDistanceMultiplier, MinZoomMultiplier, MaxZoomMultiplier);
                currentEvent.Use();
                Repaint();
            }
        }

        private void RenderPreview(Rect previewRect)
        {
            if (previewRect.width < 1f || previewRect.height < 1f)
            {
                return;
            }

            float aspect = Mathf.Max(0.01f, previewRect.width / Mathf.Max(1f, previewRect.height));
            Quaternion orbitRotation = Quaternion.Euler(previewOrbit.y, previewOrbit.x, 0f);

            float baseDistance = ComputeFramingDistance(aspect, previewBounds, PreviewVerticalFov);
            float finalDistance = baseDistance * previewDistanceMultiplier;

            Vector3 targetPosition = previewBounds.center;
            Vector3 cameraDirection = orbitRotation * new Vector3(0f, 0.1f, -1f);
            Vector3 cameraPosition = targetPosition - cameraDirection.normalized * finalDistance;

            previewRenderUtility.BeginPreview(previewRect, GUIStyle.none);

            Camera previewCamera = previewRenderUtility.camera;
            previewCamera.transform.position = cameraPosition;
            previewCamera.transform.rotation = Quaternion.LookRotation((targetPosition - cameraPosition).normalized, Vector3.up);
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 1000f;
            previewCamera.fieldOfView = PreviewVerticalFov;
            previewCamera.aspect = aspect;
            previewCamera.clearFlags = CameraClearFlags.Color;
            previewCamera.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

            Light mainLight = previewRenderUtility.lights[0];
            mainLight.transform.rotation = Quaternion.Euler(40f, 40f, 0f);
            mainLight.intensity = 1.2f;

            Light fillLight = previewRenderUtility.lights[1];
            fillLight.transform.rotation = Quaternion.Euler(340f, 218f, 177f);
            fillLight.intensity = 1f;

            previewRenderUtility.ambientColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            previewCamera.Render();

            Texture previewTexture = previewRenderUtility.EndPreview();
            GUI.DrawTexture(previewRect, previewTexture, ScaleMode.StretchToFill, false);
        }

        private static float ComputeFramingDistance(float aspect, Bounds bounds, float verticalFovDegrees)
        {
            float verticalFovRad = verticalFovDegrees * Mathf.Deg2Rad;
            float tanHalfVertical = Mathf.Tan(verticalFovRad * 0.5f);
            float horizontalFovRad = 2f * Mathf.Atan(tanHalfVertical * aspect);
            float tanHalfHorizontal = Mathf.Tan(horizontalFovRad * 0.5f);

            float halfHeight = bounds.extents.y;
            float halfWidth = Mathf.Max(bounds.extents.x, bounds.extents.z);

            if (halfHeight < 1e-4f && halfWidth < 1e-4f)
            {
                return 2f * FramePaddingMultiplier;
            }

            float distVertical = halfHeight / Mathf.Max(1e-4f, tanHalfVertical);
            float distHorizontal = halfWidth / Mathf.Max(1e-4f, tanHalfHorizontal);
            float distance = Mathf.Max(distVertical, distHorizontal);

            return distance * FramePaddingMultiplier;
        }

        private void CreatePreviewUtility()
        {
            if (previewRenderUtility != null)
            {
                return;
            }

            previewRenderUtility = new PreviewRenderUtility();
            previewRenderUtility.cameraFieldOfView = PreviewVerticalFov;
        }

        private void TryAutoAssignDefaultVisualModelTemplate()
        {
            if (visualModelTemplate != null)
            {
                return;
            }

            GameObject resolvedDefault = ResolveDefaultByName(
                DefaultVisualModelAssetName,
                BuildDefaultVisualModelPreferredPaths());
            if (resolvedDefault == null)
            {
                return;
            }

            visualModelTemplate = resolvedDefault;
        }

        private void TryAutoAssignDefaultTemplate()
        {
            if (controllerTemplate != null)
            {
                return;
            }

            GameObject resolvedDefault = ResolveDefaultByName(
                DefaultTemplateAssetName,
                BuildDefaultTemplatePreferredPaths());
            if (resolvedDefault == null)
            {
                return;
            }

            controllerTemplate = resolvedDefault;
        }

        private static GameObject ResolveDefaultByName(string assetName, string[] preferredPaths)
        {
            for (int index = 0; index < preferredPaths.Length; index++)
            {
                string preferredPath = preferredPaths[index];
                GameObject preferredAsset = AssetDatabase.LoadAssetAtPath<GameObject>(preferredPath);
                if (preferredAsset != null)
                {
                    return preferredAsset;
                }
            }

            string[] guids = AssetDatabase.FindAssets(assetName + " t:GameObject");
            for (int index = 0; index < guids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                GameObject foundAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (foundAsset == null)
                {
                    continue;
                }

                if (foundAsset.name == assetName)
                {
                    return foundAsset;
                }
            }

            return null;
        }

        private void RebuildPreviewInstance()
        {
            CleanupPreviewInstance();

            if (visualModelTemplate == null || previewRenderUtility == null)
            {
                hasValidPreviewBounds = false;
                return;
            }

            previewOrbit = new Vector2(0f, 0f);
            previewDistanceMultiplier = 1f;

            previewInstance = Instantiate(visualModelTemplate);
            if (previewInstance == null)
            {
                hasValidPreviewBounds = false;
                return;
            }

            previewInstance.hideFlags = HideFlags.HideAndDontSave;

            Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                hasValidPreviewBounds = false;
                return;
            }

            previewBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                previewBounds.Encapsulate(renderers[index].bounds);
            }

            Vector3 offsetToOrigin = -previewBounds.center;
            previewInstance.transform.position = offsetToOrigin;

            renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            previewBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                previewBounds.Encapsulate(renderers[index].bounds);
            }

            previewRenderUtility.AddSingleGO(previewInstance);
            hasValidPreviewBounds = true;
        }

        private void CleanupPreviewInstance()
        {
            if (previewInstance != null)
            {
                DestroyImmediate(previewInstance);
                previewInstance = null;
            }

            hasValidPreviewBounds = false;
        }

        private void CleanupPreviewUtility()
        {
            if (previewRenderUtility != null)
            {
                previewRenderUtility.Cleanup();
                previewRenderUtility = null;
            }
        }

        #endregion
    }
}
