using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine;
using Unity.Cloud.HighPrecision.Runtime;
using Unity.Industry.Viewer.Streaming;
using Unity.XR.CoreUtils;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;
using Unity.Mathematics;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace Unity.Industry.Viewer.Navigation.VR
{
    public class VRMovementController : NavigationOption
    {
        XROrigin m_XROrigin;
        DynamicMoveProvider m_DynamicMoveProvider;
        
        DoubleBounds? m_Bounds;
        
        [SerializeField]
        protected CameraSettings m_Settings;
        
        public float MoveSensitivity { get; private set; }
        
        private CameraUtility m_CameraUtility;
        
        private void Start()
        {
            MoveSensitivity = 1f;
            m_XROrigin = FindFirstObjectByType<XROrigin>();
            m_DynamicMoveProvider = FindFirstObjectByType<DynamicMoveProvider>();
            navigationCamera = Camera.main;
        }

        public override void Initialize()
        {
            StreamingModelController.AddObserver += AddObserver;
            StreamingModelController.BoundsUpdated += OnBoundsUpdated;
        }

        private void AddObserver(Camera observerCamera)
        {
            m_CameraUtility ??= new CameraUtility(observerCamera);
        }

        public override void Uninitialize()
        {
            StreamingModelController.AddObserver -= AddObserver;
            StreamingModelController.BoundsUpdated -= OnBoundsUpdated;
        }
        
        public override void SetDefaultView()
        {
            if(m_Bounds == null) return;
            SetView(m_Bounds);
        }

        public override void FocusToPoint(DoubleBounds bounds)
        {
            Vector3 direction = (m_XROrigin.gameObject.transform.position - ((Bounds)bounds).center).normalized; // Step 1: Direction
            Vector3 result = ((Bounds)bounds).center + direction * 5f; // Step 2: Move along direction
            
            m_XROrigin.MoveCameraToWorldLocation(result);
                
            m_XROrigin.MatchOriginUpCameraForward(Vector3.up, direction);
        }

        private void OnBoundsUpdated(DoubleBounds bounds, bool skipCameraUpdate)
        {
            if(m_XROrigin == null) return;
            
            m_Bounds = new DoubleBounds(bounds.Center, bounds.Size * 1.5f);
            
            if (!skipCameraUpdate)
            {
                SetView(bounds);
            }
            
            if(m_DynamicMoveProvider == null) return;
            
            CalculateSpeed();
        }

        private void CalculateSpeed()
        {
            if(m_Bounds == null) return;
            var maxDistanceToMove = (float)math.length(m_Bounds.Value.Extents);
            var speed = maxDistanceToMove / m_Settings.maxTimeToTravelFullSpeed * m_Settings.maxSpeedScaling;
            m_DynamicMoveProvider.moveSpeed = speed * MoveSensitivity;
        }
        
        private void SetView(DoubleBounds? bounds)
        {
            var pitch = 20.0f;
            float fillRatio = 0.9f;
            var fieldOfView = navigationCamera.fieldOfView;
            var aspectRatio = navigationCamera.aspect;
            var nearClipPlane = navigationCamera.nearClipPlane;
            var farClipPlane = navigationCamera.farClipPlane;
                
            var desiredEuler = new Vector3(pitch, 0, 0);
            var distanceFromCenter = GetDistanceFromCenterToFit(bounds.Value, fillRatio, fieldOfView, aspectRatio);
            
            if (distanceFromCenter > farClipPlane)
            {
                distanceFromCenter = (farClipPlane + nearClipPlane) / 2 ;
            }

            var center = new Vector3((float) bounds.Value.Center.x, (float) bounds.Value.Center.y, (float) bounds.Value.Center.z);
                
            var position = center - distanceFromCenter * (Quaternion.Euler(desiredEuler) * Vector3.forward);

            var faceDirection = center - new Vector3(position.x, center.y, position.z); 
                
            m_XROrigin.MoveCameraToWorldLocation(position);
                
            m_XROrigin.MatchOriginUpCameraForward(Vector3.up, faceDirection);
            
            float GetDistanceFromCenterToFit(DoubleBounds bb, float fillRatio, float fovY, float aspectRatio)
            {
                var fovX = GetHorizontalFov(fovY, aspectRatio);
                var distanceToFitXAxisInView = GetDistanceFromCenter(bb, (float)bb.Extents.x, fovX, fillRatio);
                var distanceToFitYAxisInView = GetDistanceFromCenter(bb, (float)bb.Extents.y, fovY, fillRatio);
                return Mathf.Max(distanceToFitXAxisInView, distanceToFitYAxisInView);
            }
            
            float GetHorizontalFov(float fovY, float aspectRatio)
            {
                var ratio = Mathf.Tan(Mathf.Deg2Rad * (fovY / 2.0f));
                return Mathf.Rad2Deg * Mathf.Atan(ratio * aspectRatio) * 2.0f;
            }
            
            float GetDistanceFromCenter(DoubleBounds bb, float opposite, float fov, float fillRatio)
            {
                var lookAt = bb.Center;

                var angle = fov / 2.0f;
                var ratio = Mathf.Tan(Mathf.Deg2Rad * angle);
                var adjacent = opposite / ratio;
                var distanceFromLookAt = lookAt.z - bb.Min.z + adjacent / fillRatio;

                return (float)distanceFromLookAt;
            }
        }

        public void SetHomeView()
        {
            SetView(m_Bounds);
        }
        
        public void UpdateMoveSensitivity(float value)
        {
            MoveSensitivity = value;
            CalculateSpeed();
        }

        public override void OnNavigationOptionEnable()
        {
            StreamingModelController.AddObserver?.Invoke(Camera.main);
        }

        public override void OnNavigationOptionDisable()
        {
            
        }

        public override bool IsSupported()
        {
            return true;
        }

        public override GameObject GetNavigationGameObject()
        {
            return null;
        }
    }
}
