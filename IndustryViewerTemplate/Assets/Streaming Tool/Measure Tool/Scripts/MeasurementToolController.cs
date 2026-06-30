using System;
using UnityEngine;
using System.IO;
using Unity.Cloud.Common;
using System.Threading.Tasks;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Cloud.DataStreaming.Runtime;
using Newtonsoft.Json;
using System.Collections.Generic;
#if VR_MODE
using Unity.Industry.Viewer.VR;
#endif

namespace Unity.Industry.Viewer.Streaming.Measurement
{
    public class MeasurementToolController: StreamToolControllerBase
    {
        public static Action UpdatedMeasurement;
        public static Action<bool> ResetCurrentMeasurement;
        
        private static string BaseUri => Path.Combine(Application.persistentDataPath, "MeasurementTool");
        public static string StorageUri => Path.Combine(BaseUri, _organizationId.ToString(), _projectId.ToString(), _AssetId.ToString(), _AssetVersion.ToString(), "lines.json");
        private static AssetId _AssetId => StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetId;
        private static OrganizationId _organizationId => StreamingModelController.StreamingAsset.Value.Asset.Descriptor.OrganizationId;
        private static ProjectId _projectId => StreamingModelController.StreamingAsset.Value.Asset.Descriptor.ProjectId;
        private static AssetVersion _AssetVersion => StreamingModelController.StreamingAsset.Value.Asset.Descriptor.AssetVersion;
        
        private StreamingModelController m_StreamingModelController;
        
        public MeasureMode CurrentMeasureMode {get; private set;} = MeasureMode.TwoPoint;
        
        [SerializeField]
        LayerMask m_UILayerMask;

        [SerializeField] private float m_DefaultOrthogonalCheckDistance = 1000f;
        
        public MeasureLineData MeasureLineData { get; private set; }
        
        public MeasureFormat CurrentMeasureFormat { get; private set; }

        private MeasureFormat m_MeasureFormatBeforeEdit;
        
        private void Awake()
        {
            CurrentMeasureFormat = MeasureUnit.GetSystemUnit();
        }

        private void Start()
        {
            m_StreamingModelController = FindFirstObjectByType<StreamingModelController>();
            ResetCurrentMeasurement += OnResetCurrentMeasurement;
        }

        private void OnDestroy()
        {
            ResetCurrentMeasurement -= OnResetCurrentMeasurement;
            SubscribeToInteraction(false);
        }

        private void SubscribeToInteraction(bool subscribe)
        {
            if (subscribe)
            {
#if VR_MODE
                VRInteractionController.SubscribeSingleActivate(this, OnSingleTapTrigger);
#else
                InteractionController.SubscribeTap(this, OnTapAction);
#endif
            }
            else
            {
#if VR_MODE
                VRInteractionController.UnsubscribeSingleActivate(this);
#else
                InteractionController.UnsubscribeTap(this);
#endif
            }
        }

#if VR_MODE
        private void OnSingleTapTrigger(Ray ray, int instanceId)
        {
            _ = RayCast(ray);
        }
#endif

        private void OnResetCurrentMeasurement(bool resetUnit)
        {
            MeasureLineData = null;
            if (resetUnit)
            {
                CurrentMeasureFormat = MeasureUnit.GetSystemUnit();
            }
        }

        public override void OnToolOpened()
        {
            ToolOpened?.Invoke();
            SubscribeToInteraction(true);
        }

#if !VR_MODE
        private void OnTapAction(Vector3 position)
        {
            Ray ray = m_StreamingModelController.ActiveCamera.ScreenPointToRay(position);
            _ = RayCast(ray);
        }
#endif

        public override void OnToolClosed()
        {
            ToolClosed?.Invoke();
        }

        public void UpdateCurrentAnchorPosition(int index, Ray ray, Action<RaycastResult> callback)
        {
            _ = UpdateAnchorPosition();
            return;

            async Task UpdateAnchorPosition()
            {
                var raycastResult = await UseRayCast(ray, m_StreamingModelController.ActiveCamera.farClipPlane);
                if (raycastResult.InstanceId == InstanceId.None)
                {
                    callback?.Invoke(raycastResult);
                    return;
                }
                if(MeasureLineData == null) return;
                MeasureLineData.SetAnchor(index, new Anchor(raycastResult.Point.ToVector3(), raycastResult.Normal));
                callback?.Invoke(raycastResult);
            }
        }
        
        public void SetMeasureUnit(MeasureFormat measureFormat)
        {
            CurrentMeasureFormat = measureFormat;
            if (MeasureLineData != null)
            {
                MeasureLineData.MeasureFormat = CurrentMeasureFormat;
            }
        }

        public void SaveCurrentMeasurement(string measurementName, Color savedColor, bool systemUnit, Action<bool, List<MeasureLineData>> callback)
        {
            if(MeasureLineData == null || MeasureLineData.Anchors.Count < 2)
            {
                callback?.Invoke(false, null);
                return;
            }
            MeasureLineData.Name = measurementName;
            MeasureLineData.Color = savedColor;
            MeasureLineData.MeasureFormat = systemUnit? MeasureUnit.GetSystemUnit() : CurrentMeasureFormat;
            MeasureLineData.HasMeasureFormatOverride = !systemUnit;

            _ = SaveToFile();
            return;
            
            async Task SaveToFile()
            {
                List<MeasureLineData> collections = null;
                if (File.Exists(StorageUri))
                {
                    //Read existing lines
                    collections = await MeasureUnit.ReadCollections();
                }
                collections ??= new List<MeasureLineData>();
                collections.Add(MeasureLineData);
                var json = JsonConvert.SerializeObject(collections, Formatting.None);
                var directory = Path.GetDirectoryName(StorageUri);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                await File.WriteAllTextAsync(StorageUri, json);
                ResetCurrentMeasurement?.Invoke(false);
                callback?.Invoke(true, collections);
            }
        }

        public void SetEditingMeasurement(MeasureLineData measureLineData)
        {
            m_MeasureFormatBeforeEdit = CurrentMeasureFormat;
            SubscribeToInteraction(false);
            MeasureLineData = measureLineData;
            SetMeasureUnit(measureLineData.MeasureFormat);
        }

        public void SetMeasureMode(MeasureMode newMeasureMode)
        {
            if (CurrentMeasureMode == newMeasureMode) return;
            CurrentMeasureMode = newMeasureMode;
            if (MeasureLineData != null)
            {
                MeasureLineData.MeasureMode = CurrentMeasureMode;
            }
        }

        public void FinishedEdit(bool saveChanges, Action<bool> callback = null)
        {
            CurrentMeasureFormat = m_MeasureFormatBeforeEdit;
            SubscribeToInteraction(true);
            if (!saveChanges)
            {
                MeasureLineData = null;
                callback?.Invoke(false);
                return;
            }
            _ = SaveNewMeasurement();
            return;

            async Task SaveNewMeasurement()
            {
                var collections = await MeasureUnit.ReadCollections();
                if (collections == null)
                {
                    Debug.LogError("No existing measurements found when trying to save edited measurement.");
                    callback?.Invoke(false);
                    return;
                }
                var index = collections.FindIndex(line => line.Id == MeasureLineData.Id);
                if (index < 0)
                {
                    Debug.LogError("Could not find existing measurement when trying to save edited measurement.");
                    callback?.Invoke(false);
                    return;
                }
                collections[index] = MeasureLineData;
                var json = JsonConvert.SerializeObject(collections, Formatting.None);
                await File.WriteAllTextAsync(StorageUri, json);
                callback?.Invoke(true);
                MeasureLineData = null;
            }
        }

        private async Task<RaycastResult> UseRayCast(Ray ray, float distance)
        {
            var raycastOptions = RaycastOptions.ExcludeHiddenInstances;
            return await m_StreamingModelController.Stage.RaycastAsync((DoubleRay) ray, distance, raycastOptions);
        }
        
        private async Task RayCast(Ray ray)
        {
            var raycastResult = await UseRayCast(ray, m_StreamingModelController.ActiveCamera.farClipPlane);
            if (raycastResult.InstanceId == InstanceId.None)
            {
                Debug.Log("No hit on the stage");
                // No hit on the stage, never raycast;
                return;
            }

            Vector3? hitPoint = raycastResult.Point.ToVector3();
            Vector3 normal = raycastResult.Normal;
            if (Physics.Raycast(ray, out var hit, m_StreamingModelController.ActiveCamera.farClipPlane, m_UILayerMask))
            {
                var uiRaycastPoint = hit.point;
                        
                // Calculate distances along the ray using dot product
                float uiDistance = Vector3.Dot(uiRaycastPoint - ray.origin, ray.direction);
                float stageDistance = Vector3.Dot(hitPoint.Value - ray.origin, ray.direction);

                if (uiDistance < stageDistance)
                {
                    Debug.Log("Hit on the UI, ignoring this raycast");
                    // UI is in front of the stage, ignore this raycast
                    return;
                }
            }
            
            if(MeasureLineData != null && MeasureLineData.Anchors.Count >= 2)
            {
                ResetCurrentMeasurement?.Invoke(false);
            }
            
            // Hit on the stage, check measure mode and add anchor
            if (CurrentMeasureMode == MeasureMode.Orthogonal && MeasureLineData == null)
            {
                // In orthogonal mode, we need to get the normal from the raycast result
                
                Ray orthogonalRay = new Ray(hitPoint.Value, normal);
                var orthogonalRaycastResult = await UseRayCast(orthogonalRay, m_DefaultOrthogonalCheckDistance);
                MeasureLineData = new MeasureLineData
                {
                    MeasureFormat = CurrentMeasureFormat
                };
                MeasureLineData.AddAnchor(new Anchor(hitPoint.Value, normal));
                if (orthogonalRaycastResult.InstanceId == InstanceId.None)
                {
                    //No hit on the stage, show no hit feedback
                    UpdatedMeasurement?.Invoke();
                    return;
                }
                MeasureLineData.AddAnchor(new Anchor(orthogonalRaycastResult.Point.ToVector3(), orthogonalRaycastResult.Normal));
                UpdatedMeasurement?.Invoke();
            }
            else
            {
                MeasureLineData ??= new MeasureLineData();
                MeasureLineData.MeasureFormat = CurrentMeasureFormat;
                MeasureLineData.AddAnchor(new Anchor(hitPoint.Value, normal));
                UpdatedMeasurement?.Invoke();
            }
        }
    }
}
