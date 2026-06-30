using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Threading.Tasks;
using System.Linq;
using Unity.Cloud.DataStreaming.Runtime;
using UnityEngine.InputSystem;

namespace Unity.Industry.Viewer.VR.FlyMode
{
    public class CustomTeleportHandler : MonoBehaviour
    {
        const float k_MaxDistance = 1000f;

        struct RayData
        {
            public Ray Ray;
            public float Distance;
        }

        private VRMovementController m_MovementController;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private XRRayInteractor _rayInteractor;
        private LineRenderer _lineRenderer;
        Vector3[] m_Positions = new Vector3[MAX_LINE_POINTS];
        int m_CurrentCapacity = MAX_LINE_POINTS;
        Vector3 m_HitNormal;

        private Gradient _invalidGradient => m_MovementController.InvalidGradient;
        private Gradient _validGradient => m_MovementController.ValidGradient;

        private GameObject m_ReticleInstance;

        public Vector3? TargetPosition { get; private set; }

        const int MAX_LINE_POINTS = 64;

        private int m_StepSize;
        
        private bool m_CancelRequested = false;

        // Key to zero-GC: Reuse this list every frame instead of creating a new one.
        private readonly List<RayData> m_ReusableRays = new List<RayData>(MAX_LINE_POINTS);

        private void Awake()
        {
            m_StepSize = Mathf.FloorToInt(Mathf.Sqrt(MAX_LINE_POINTS));
        }

        void OnEnable()
        {
            m_CancelRequested = false;
            _rayInteractor ??= GetComponent<XRRayInteractor>();
            _lineRenderer ??= GetComponent<LineRenderer>();
            if (m_MovementController != null)
            {
                m_MovementController.TeleportCancelActionReference.action.performed -= CancelActionOnPerformed;
                m_MovementController.TeleportCancelActionReference.action.performed += CancelActionOnPerformed;
            }
        }

        private bool m_FindingTarget;

        // Update is called once per frame
        void Update()
        {
            if (_rayInteractor == null) return;

            int pointsWritten = 0;

            _rayInteractor.GetLinePoints(ref m_Positions, out pointsWritten);

            if (pointsWritten > m_Positions.Length)
            {
                m_CurrentCapacity = m_Positions.Length;
                if (m_CurrentCapacity > MAX_LINE_POINTS)
                {
                    // Safety break to prevent a runaway allocation if the curve calculation goes wrong
                    Debug.LogError("Ray Interactor line points exceeded max allowed limit!");
                    return;
                }
            }

            if (pointsWritten > 0)
            {
                if (!m_FindingTarget)
                {
                    _ = FindTargetPosition(m_Positions, pointsWritten);
                }
                if (m_ReticleInstance != null)
                {
                    var stickInput = m_MovementController.TeleportDirectionInputReference.action.ReadValue<Vector2>();
                    RotateReticle(stickInput, m_MovementController.NavigationCamera.transform);
                }
            }
            else
            {
                _lineRenderer.positionCount = 0;
            }
        }

        private void OnDisable()
        {
            _lineRenderer.positionCount = 0;
            TargetPosition = null;
            if (m_ReticleInstance != null)
            {
                var pos = m_ReticleInstance.transform.position;
                var rot = m_ReticleInstance.transform.rotation;
                Destroy(m_ReticleInstance);
                // Only trigger teleport if not cancelled AND controller is still active
                if (!m_CancelRequested && m_MovementController != null && m_MovementController.gameObject.activeInHierarchy)
                {
                    m_MovementController.TeleportStart(pos, rot);
                }
                m_CancelRequested = false;
            }

            if (m_MovementController != null)
            {
                m_MovementController.TeleportCancelActionReference.action.performed -= CancelActionOnPerformed;
            }
        }
        
        // Rotates the reticle based on joystick input (Vector2).
        void RotateReticle(Vector2 joystickInput, Transform cameraTransform)
        {
            if (m_ReticleInstance == null || joystickInput == Vector2.zero)
                return;

            // Flatten camera forward to XZ plane
            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0;
            if (camForward.sqrMagnitude < 0.0001f)
                camForward = Vector3.forward; // fallback

            Quaternion baseRotation = Quaternion.LookRotation(camForward, Vector3.up);

            float angle = Mathf.Atan2(joystickInput.x, joystickInput.y) * Mathf.Rad2Deg;
            Quaternion offset = Quaternion.Euler(0f, angle, 0f);

            m_ReticleInstance.transform.rotation = baseRotation * offset;
        }

        private async Task FindTargetPosition(Vector3[] points, int count)
        {
            // Prevent overlapping invocations: Update fires this every frame, but the async raycast
            // can span multiple frames and races on shared m_Positions/TargetPosition/lineRenderer.
            m_FindingTarget = true;
            try
            {
                await FindTargetPositionImpl(points, count);
            }
            finally
            {
                m_FindingTarget = false;
            }
        }

        private async Task FindTargetPositionImpl(Vector3[] points, int count)
        {
            if (m_MovementController.StreamingModelController == null)
            {
                return;
            }

            // Reset state for this frame's calculation
            TargetPosition = null;

            bool hasHitUI = false;
            int hitIndex = -1;
            Vector3? hitPosition = null;

            List<RayData> coarseRays;

            // --- 1. COARSE PASS ---
            GenerateRays(points, count, m_StepSize, out coarseRays);

            var result = await PickRaysAsync(coarseRays);

            if (result.Index != -1)
            {
                int coarseRayIndex = result.Index;
                hitIndex = coarseRayIndex * m_StepSize; 
        
                // b) Use the hit point and normal found by the stream raycast system.
                hitPosition = result.PickerResult.Point;
                m_HitNormal = result.PickerResult.Normal;
            }

            // UI Check (This should ideally be PickRaysAsync for performance, but using Physics.Raycast for now)
            hasHitUI = CheckRaycastUI(points, ref hitIndex, ref hitPosition);

            SetTargetPosition(hitPosition, hitIndex, count, hasHitUI);
        }

        private void SetTargetPosition(Vector3? position, int hitSegmentStartIndex, int count, bool hasHitUI = false)
        {
            if (!isActiveAndEnabled)
                return;

            bool isValid;
            bool isVerticalSurface = false;
            TargetPosition = position;
            if (hasHitUI)
            {
                isValid = false;
            }
            else
            {
                // Check if destination is too much vertical
                isVerticalSurface = Vector3.Dot(m_HitNormal, Vector3.up) < 0.3f;
                isValid = TargetPosition.HasValue && !isVerticalSurface;
            }
            
            Vector3 finalPoint = TargetPosition.HasValue? TargetPosition.Value: m_Positions[count - 1];

            if (isValid && m_ReticleInstance == null && TargetPosition.HasValue)
            {
                m_ReticleInstance = Instantiate(m_MovementController.DirectionTeleportReticle);
                m_ReticleInstance.transform.position = TargetPosition.Value;
            }
            else if (isValid && m_ReticleInstance != null && TargetPosition.HasValue)
            {
                m_ReticleInstance.transform.position = TargetPosition.Value;
            }
            else if (!isValid && m_ReticleInstance != null)
            {
                Destroy(m_ReticleInstance);
            }

            _lineRenderer.colorGradient = isValid ? _validGradient : _invalidGradient;
            if (TargetPosition.HasValue) // Only clip the line if a valid hit was found
            {
                // 1. Calculate the number of points to draw:
                //    hitSegmentStartIndex is the start of the hit segment.
                //    We draw up to this point, PLUS the hit point itself.
                int finalCount = hitSegmentStartIndex + 1; 

                _lineRenderer.positionCount = finalCount;
        
                // 2. Draw the line up to the starting point of the hit segment.
                //    (We draw up to hitSegmentStartIndex - 1)
                for (int i = 0; i < hitSegmentStartIndex; i++)
                {
                    _lineRenderer.SetPosition(i, m_Positions[i]);
                }
        
                // 3. Manually set the LAST point of the line to the exact hit position.
                // This draws the final, clipped segment correctly.
                _lineRenderer.SetPosition(hitSegmentStartIndex, finalPoint);
            }
            else
            {
                // No hit (or invalid hit): Draw the full curve
                _lineRenderer.positionCount = count;
                _lineRenderer.SetPositions(m_Positions);
            }
    
            if (hasHitUI || isVerticalSurface)
            {
                TargetPosition = null;
            }
        }

        private void GenerateRays(Vector3[] points, int count, int step, out List<RayData> rays,
            int startIndex = 0, int endIndex = -1, bool clearList = true)
        {
            rays = m_ReusableRays;
            if (clearList) rays.Clear();

            // Set actual bounds
            int startPoint = startIndex;
            int endPoint = (endIndex == -1) ? count : Mathf.Min(endIndex, count);

            // Loop from start to end, stepping by 'step'.
            // We ensure we don't try to access points beyond the 'count' limit.
            for (int i = startPoint; i < endPoint; i += step)
            {
                int nextIndex = Mathf.Min(i + step, endPoint - 1);
                if (i >= nextIndex) break;

                Vector3 start = points[i];
                Vector3 end = points[nextIndex];
                Vector3 direction = end - start;
                float distance = direction.magnitude;

                // CRITICAL: Skip zero-length segments to prevent NaN/Assertion errors
                if (distance < float.Epsilon)
                {
                    continue;
                }

                rays.Add(new RayData
                {
                    Ray = new Ray(start, direction),
                    Distance = distance
                });
            }
        }

        private async Task<PathPickerResult> PickRaysAsync(List<RayData> rays)
        {
            try
            {
                var tasks = rays.Select(r => PickAsync(r.Ray, r.Distance));
                var results = await Task.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].HasIntersected)
                    {
                        // The Index returned here is the index into the 'm_ReusableRays' list.
                        return new PathPickerResult
                        {
                            Index = i,
                            PickerResult = results[i]
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            return new PathPickerResult { Index = -1, PickerResult = PickerResult.Invalid };
        }

        private async Task<PickerResult> PickAsync(Ray ray, float maxDistance = k_MaxDistance)
        {
            var raycastResult =
                new PickerResult(await m_MovementController.StreamingModelController.Stage.RaycastAsync(ray,
                    maxDistance, RaycastOptions.ExcludeHiddenInstances));
            return raycastResult;
        }

        private static bool CheckRaycastUI(Vector3[] linePoints, ref int hitIndex, ref Vector3? hitPosition)
        {
            for (int i = 0; i < linePoints.Length - 1; i++)
            {
                var ray = new Ray(linePoints[i], linePoints[i + 1] - linePoints[i]);
                var distance = Vector3.Distance(linePoints[i], linePoints[i + 1]);
                if (Physics.Raycast(ray, out RaycastHit hitInfo, distance, LayerMask.GetMask("UI")))
                {
                    hitIndex = i;
                    hitPosition = hitInfo.point;
                    return true;
                }
            }

            return false;
        }

        private void CancelActionOnPerformed(InputAction.CallbackContext obj)
        {
            m_CancelRequested = true;
            TargetPosition = null;
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
            }
            if (m_ReticleInstance != null)
            {
                Destroy(m_ReticleInstance);
            }
        }

        public void Init(VRMovementController movementController)
        {
            m_MovementController = movementController;
            m_MovementController.TeleportCancelActionReference.action.performed -= CancelActionOnPerformed;
            m_MovementController.TeleportCancelActionReference.action.performed += CancelActionOnPerformed;
        }
    }
}