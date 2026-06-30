using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Linq;
using Unity.AppUI.UI;
using UnityEngine.Localization;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Industry.Viewer.Streaming.Environment
{
    public class EnvironmentToolSceneListener: MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        Camera m_EnvironmentCamera;
        
        [SerializeField] private LayerMask m_EnvironmentLayerMask;
        
        private Camera m_OriginalCamera;
        
        public EnvironmentsSettingsAsset Settings => m_Settings;
        
        [SerializeField]
        private EnvironmentsSettingsAsset m_Settings;
        
        [SerializeField]
        LocalizedString[] m_NavigationOptionsLeadsDisability = Array.Empty<LocalizedString>();
        
        private static HashSet<int> m_EnvSceneRootObjectIDs;

        [SerializeField] private StreamingToolAsset m_ToolAsset;
        
        IPressable m_EnvironmentToolButton;
        
        private bool disallowInNavigationMode = false;
        
        void Start()
        {
            if (EnvironmentToolController.CurrentEnvironmentSettings == null)
            {
                AssignDefaultEnvironment();
            }
            StreamToolSubmenuController.InitializeTools += OnInitializeTools;
            EnvironmentToolController.SetEnvironment += SetEnvironment;
            NavigationController.OnNavigationOptionChanged += OnNavigationOptionChanged;
            EnvironmentToolController.EnvironmentLoaded += EnvironmentLoaded;
            EnvironmentToolController.DefaultEnvironmentLoaded += DefaultEnvironmentLoaded;
            StreamSceneController.ExitSceneConfirmed += ExitSceneConfirmed;
        }

        private void LateUpdate()
        {
            if(Camera.main == null || m_EnvironmentCamera == null) return;
            m_EnvironmentCamera.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
            m_EnvironmentCamera.fieldOfView = Camera.main.fieldOfView;
        }

        void OnDestroy()
        {
            StreamToolSubmenuController.InitializeTools -= OnInitializeTools;
            StreamSceneController.ExitSceneConfirmed -= ExitSceneConfirmed;
            EnvironmentToolController.SetEnvironment -= SetEnvironment;
            NavigationController.OnNavigationOptionChanged -= OnNavigationOptionChanged;
            EnvironmentToolController.EnvironmentLoaded -= EnvironmentLoaded;
            EnvironmentToolController.DefaultEnvironmentLoaded -= DefaultEnvironmentLoaded;
        }

        private void OnInitializeTools(StreamingToolAsset[] obj)
        {
            StartCoroutine(WaitForUICompleted());
            return;

            IEnumerator WaitForUICompleted()
            {
                yield return new WaitForEndOfFrame();
                if (disallowInNavigationMode)
                {
                    DisableUIButton();
                }
            }
        }

        private void DisableUIButton()
        {
            var streamToolUIControllers =
                FindObjectsByType<StreamToolsUIControllerBase>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            foreach (var streamToolsUIControllerBase in streamToolUIControllers)
            {
                if(streamToolsUIControllerBase.ToolButtons == null) continue;
                if(streamToolsUIControllerBase.ToolButtons.TryGetValue(m_ToolAsset, out m_EnvironmentToolButton))
                {
                    var toolButton = m_EnvironmentToolButton as VisualElement;
                    toolButton?.SetEnabled(false);
                }
            }
        }

        private void OnNavigationOptionChanged(NavigationOption newNavigation)
        {
            StartCoroutine(WaitForToCheckARSession());

            IEnumerator WaitForToCheckARSession()
            {
                yield return null;
                
                // Check if ARSession is enabled
                ARSession arSession = FindFirstObjectByType<ARSession>(FindObjectsInactive.Exclude);
                if(arSession != null && arSession.enabled)
                {
                    EnvironmentToolController.SetEnvironment?.Invoke(EnvironmentToolController.k_DefaultEnvironmentId);
                    if (m_NavigationOptionsLeadsDisability.Any(x =>
    x.TableReference.Equals(newNavigation.NavigationName.TableReference) &&
    x.TableEntryReference.Equals(newNavigation.NavigationName.TableEntryReference)))
                    {
                        disallowInNavigationMode = true;
                        // Disable the environment tool button when in AR mode and using a navigation option that leads to disability
                        DisableUIButton();
                    }
                }
                else
                {
                    disallowInNavigationMode = false;
                    if (m_EnvironmentToolButton != null)
                    {
                        var toolButton = m_EnvironmentToolButton as VisualElement;
                        toolButton?.SetEnabled(true);
                    }
                    SetUpEnvironmentCamera();
                }
            }
        }

        private void DefaultEnvironmentLoaded()
        {
            RevertCamera();
        }
        
        private void ExitSceneConfirmed()
        {
            RevertCamera();
        }

        private void RevertCamera()
        {
            if(m_EnvironmentCamera == null || m_OriginalCamera == null) return;
            m_OriginalCamera.cullingMask |= m_EnvironmentLayerMask;
            var envUrpCameraData = m_EnvironmentCamera.GetUniversalAdditionalCameraData();
            envUrpCameraData.cameraStack.Remove(m_OriginalCamera);
                            
            var urpCameraData = m_OriginalCamera.GetUniversalAdditionalCameraData();
            urpCameraData.renderType = CameraRenderType.Base;
                            
            Destroy(m_EnvironmentCamera.gameObject);
            m_EnvironmentCamera = null;
            m_OriginalCamera = null;
        }

        private void SetUpEnvironmentCamera()
        {
            if (m_EnvironmentCamera != null || EnvironmentToolController.CurrentEnvironmentSettings.id == EnvironmentToolController.k_DefaultEnvironmentId) return;
            var cameraGO = new GameObject("Environment Camera");
            m_EnvironmentCamera = cameraGO.AddComponent<Camera>();
            m_EnvironmentCamera.cullingMask = m_EnvironmentLayerMask;
            m_EnvironmentCamera.clearFlags = CameraClearFlags.Skybox;
            
            m_OriginalCamera = Camera.main;
            m_OriginalCamera.cullingMask &= ~m_EnvironmentLayerMask;
            // If using URP, you can use the camera stack feature
            var urpCameraData = m_OriginalCamera.GetUniversalAdditionalCameraData();
            var envUrpCameraData = m_EnvironmentCamera.GetUniversalAdditionalCameraData();

            envUrpCameraData.renderType = CameraRenderType.Base;
            envUrpCameraData.renderPostProcessing = urpCameraData.renderPostProcessing;
            urpCameraData.renderType = CameraRenderType.Overlay;
            envUrpCameraData.cameraStack.Add(m_OriginalCamera);
        }

        private void EnvironmentLoaded(LayerMask targetLayer)
        {
            m_EnvironmentLayerMask = targetLayer;
            SetUpEnvironmentCamera();
        }
        
        private void SetEnvironment(string newEnvironmentId)
        {
            if (EnvironmentToolController.IsLoading
                ||(EnvironmentToolController.CurrentEnvironmentSettings != null && EnvironmentToolController.CurrentEnvironmentSettings.id == newEnvironmentId)
                || m_Settings == null || m_Settings.Scenes == null || m_Settings.Scenes.Length == 0)
            {
                return;
            }

            EnvironmentToolController.IsLoading = true;

            var newEnvironment = m_Settings.Scenes.FirstOrDefault(environment => environment.id == newEnvironmentId);
            if (newEnvironment == null)
            {
                Debug.LogError($"Unable to set environment with ID '{newEnvironmentId}'. EnvironmentsSettings doesn't contain such entry.");
                EnvironmentToolController.IsLoading = false;
                return;
            }

            UnloadCurrentEnvironment();
            EnvironmentToolController.CurrentEnvironmentSettings = newEnvironment;
            LoadEnvironment(newEnvironment);
        }

        private void UnloadCurrentEnvironment()
        {
            if (EnvironmentToolController.CurrentEnvironmentSettings == null || EnvironmentToolController.CurrentEnvironmentSettings.Scene == null) return;
            var envScene = SceneManager.GetSceneByName(EnvironmentToolController.CurrentEnvironmentSettings.Scene.SceneName);

            if (!envScene.IsValid() || !envScene.isLoaded) return;

            // Move all Streaming objects from the Environment scene to the Streaming scene (it should be empty at this point)
            // To understand what to move we will register all Env scene objects on Env scene loaded
            var streamingScene = SceneManager.GetSceneByName(SceneUtility.GetStreamingSceneName());
            using var objects = new NativeArray<int>(envScene.GetRootGameObjects()
                .Select(go => go.GetInstanceID())
                .Where(id => !m_EnvSceneRootObjectIDs.Contains(id))
                .ToArray(), Allocator.Temp);
            SceneManager.MoveGameObjectsToScene(objects, streamingScene);

            // Make Streaming scene active
            SceneManager.SetActiveScene(streamingScene);

            // Unload the environment scene
            SceneManager.UnloadSceneAsync(envScene).GetAwaiter().GetResult();
            m_EnvSceneRootObjectIDs = null;
        }

        private void LoadEnvironment(EnvironmentSceneSettings environmentSettings)
        {
            if (environmentSettings.Scene == null
                || string.IsNullOrEmpty(environmentSettings.Scene.SceneName)
                || environmentSettings.Scene.SceneName == gameObject.scene.name)
            {
                EnvironmentToolController.IsLoading = false;
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(environmentSettings.Scene.SceneName, LoadSceneMode.Additive);

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == environmentSettings.Scene.SceneName)
                {
                    Debug.Log($"Environment scene '{environmentSettings.Scene.SceneName}' loaded successfully.");
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    SceneManager.sceneUnloaded += OnSceneUnloaded;
                    SceneManager.SetActiveScene(SceneManager.GetSceneByName(environmentSettings.Scene.SceneName));

                    // Register all root objects of the environment scene to static list
                    var envScene = SceneManager.GetActiveScene();

                    var listOfRootObjects = envScene.GetRootGameObjects();
                    
                    m_EnvSceneRootObjectIDs = listOfRootObjects.Select(go => go.GetInstanceID()).ToHashSet();

                    //If not in the default streaming scene
                    // Resolve the environment layer once. Mathf.Log(value,2) is only valid for a
                    // single-bit mask and returns -Infinity for an empty mask (cast to a garbage
                    // layer index); isolate the lowest set bit and skip assignment when unset.
                    int environmentLayer = -1;
                    int maskValue = m_EnvironmentLayerMask.value;
                    if (maskValue != 0)
                    {
                        environmentLayer = (int)Mathf.Log(maskValue & -maskValue, 2);
                    }
                    foreach (var rootObject in listOfRootObjects)
                    {
                        var allChildren = rootObject.GetComponentsInChildren<Transform>(true);
                        foreach (var child in allChildren)
                        {
                            if (environmentLayer >= 0)
                            {
                                child.gameObject.layer = environmentLayer;
                            }
                        }
                    }
                    
                    // Disable all directional lights outside the environment scene to avoid light conflicts
                    var directionalLights =
                        FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(x => x.type == LightType.Directional).ToArray();
                        
                    if (directionalLights.Length > 1)
                    {
                        foreach (var directionalLight in directionalLights)
                        {
                            if (directionalLight.gameObject.scene != envScene)
                            {
                                directionalLight.gameObject.SetActive(false);
                            }
                        }
                    }

                    // Move all objects from Streaming scene to the environment scene
                    // Leave Streaming scene inactive and empty to keep its rendering settings
                    var streamingScene = SceneManager.GetSceneByName(SceneUtility.GetStreamingSceneName());
                    using var objects = new NativeArray<int>(streamingScene.GetRootGameObjects()
                        .Select(go => go.GetInstanceID())
                        .ToArray(), Allocator.Temp);
                    SceneManager.MoveGameObjectsToScene(objects, envScene);

                    EnvironmentToolController.CurrentEnvironmentSettings = environmentSettings;
                    EnvironmentToolController.IsLoading = false;
                    
                    EnvironmentToolController.EnvironmentLoaded?.Invoke(m_EnvironmentLayerMask);
                }
            }

            void OnSceneUnloaded(Scene scene)
            {
                if (scene.name == environmentSettings.Scene.SceneName)
                {
                    SceneManager.sceneUnloaded -= OnSceneUnloaded;
                    Debug.Log($"Environment scene '{scene.name}' unloaded successfully.");

                    if (EnvironmentToolController.CurrentEnvironmentSettings.id == EnvironmentToolController.k_DefaultEnvironmentId)
                    {
                        // If we are loading the default streaming scene, enable the directional lights from the default scene
                        var directionalLights =
                            FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.type == LightType.Directional).ToArray();
                        foreach (var directionalLight in directionalLights)
                        {
                            directionalLight.gameObject.SetActive(true);
                        }
                        
                        EnvironmentToolController.DefaultEnvironmentLoaded?.Invoke();
                    }

                    if (!EnvironmentToolController.IsLoading
                        && EnvironmentToolController.CurrentEnvironmentSettings != null
                        && EnvironmentToolController.CurrentEnvironmentSettings.id == environmentSettings.id)
                    {
                        AssignDefaultEnvironment();
                    }
                }
            }
        }
        
        private void AssignDefaultEnvironment()
        {
            if (m_Settings == null || m_Settings.Scenes == null || m_Settings.Scenes.Length == 0)
            {
                Debug.LogError("EnvironmentsSettingsAsset is not set or no scenes in EnvironmentToolController.");
                return;
            }

            EnvironmentToolController.CurrentEnvironmentSettings = m_Settings.Scenes.FirstOrDefault(envSettings => envSettings.id == EnvironmentToolController.k_DefaultEnvironmentId);
            Debug.Log("Current environment set to default.");

            if (EnvironmentToolController.CurrentEnvironmentSettings == null)
            {
                Debug.LogError("Default environment settings not found in EnvironmentsSettingsAsset.");
                return;
            }
        }
    }
}
