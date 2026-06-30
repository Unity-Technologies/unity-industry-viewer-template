using Unity.Mathematics;
using UnityEngine;
using Unity.Cloud.DataStreaming.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    // Like SphericalObserver but fixes two problems that appear when the camera is
    // inside a tile's bounds (e.g. VR walkthroughs, architectural close-ups):
    //
    // Problem 1 — Priority inversion: SphericalObserver collapses max *= distance to
    //   max *= nearPlane (~0.01m for VR), making close tiles ~5000x lower priority than
    //   identical tiles at 50m. NOTE: this fix requires returning ErrorSpecification.max,
    //   which is internal to the data-streaming package. This external copy cannot apply
    //   Fix 1 — interior tile priority boost is lost. Quality (min) is still correct.
    //
    // Problem 2 — 360° max-quality loading: SphericalObserver uses min = projectionFactor
    //   × SSE × distance for all tiles regardless of direction. Inside a building, front
    //   and back content tiles at similar distances demand identical LOD quality and together
    //   saturate the polygon budget. Fix: min for tiles the camera is outside (rho>0) uses
    //   a direction-aware sseForMin that ramps from SSE (theta=halfFov, viewport edge) to
    //   SSE×32768 (theta=π, directly behind). Tiles within the camera's FOV cone always
    //   get sseForMin=SSE so every visible tile loads at full quality regardless of camera
    //   mode (orbit, fly-through, or first-person). Only off-screen tiles are penalised,
    //   freeing polygon budget without degrading anything the user can actually see.
    //   Enclosing tiles (rho=0, camera inside) always use sseForMin=SSE.
    public class ProximityAwareObserver : ICameraObserver
    {
        const float k_FarBehindMultiplierBase = 8;

        Camera m_Camera;

        int m_CurrentTime;

        bool m_IsOrthographic;
        double m_ProjectionFactor;
        float m_NearPlane;
        float m_FarPlane;
        double m_HalfFovRadians;
        double3 m_Forward;
        double3 m_Position;
        float3 m_NearPlaneCenter;
        float m_TargetSseFarBehind;
        
        public ProximityAwareObserver(Camera camera)
        {
            m_Camera = camera;
            ScreenSpaceError = 4; // Default value recommended for VR first-person walkthroughs at room scale. Adjust as needed.
        }

        public ProximityAwareObserver(Camera camera, float screenSpaceError)
        {
            m_Camera = camera;
            ScreenSpaceError = screenSpaceError;
        }

        // ScreenSpaceError controls the detail-vs-distance tradeoff for the entire scene.
        // Think of it as "how much detail do I want to see at a given distance?"
        //
        // Lower value  → more detail loaded further away, more polygons, heavier on GPU/CPU.
        // Higher value → less detail at distance, fewer polygons, lighter and faster.
        //
        // Recommended starting points:
        //   4 (default) — VR first-person walkthrough at room scale. Good quality within ~15 m.
        //   2           — Desktop first-person or desktop orbit around a building. Quality within ~30 m.
        //   1           — Desktop orbit or aerial view from 50–100 m. Quality within ~100 m.
        //   6–8         — Mobile or performance-constrained devices. Accepts coarser geometry.
        //
        // Rule of thumb: halving ScreenSpaceError doubles the effective quality distance,
        // but also roughly doubles the polygon count — test against your triangle budget.
        public float ScreenSpaceError { get; set; }
        public float ScreenSpaceErrorExponent { private get; set; } = 5;
        public bool CanUpdate { get; set; } = true;

        public float GetErrorSpecification(ObserverInputData data)
        {
            var time = Time.frameCount;
            if (time != m_CurrentTime && CanUpdate)
            {
                ComputeObserverData();
                m_CurrentTime = time;
            }

            var nearPlaneCenterToBounds = data.Bounds.Center - m_NearPlaneCenter;
            nearPlaneCenterToBounds.x = nearPlaneCenterToBounds.x > 0 ? math.max(nearPlaneCenterToBounds.x - data.Bounds.Extents.x, 0) : math.min(nearPlaneCenterToBounds.x + data.Bounds.Extents.x, 0);
            nearPlaneCenterToBounds.y = nearPlaneCenterToBounds.y > 0 ? math.max(nearPlaneCenterToBounds.y - data.Bounds.Extents.y, 0) : math.min(nearPlaneCenterToBounds.y + data.Bounds.Extents.y, 0);
            nearPlaneCenterToBounds.z = nearPlaneCenterToBounds.z > 0 ? math.max(nearPlaneCenterToBounds.z - data.Bounds.Extents.z, 0) : math.min(nearPlaneCenterToBounds.z + data.Bounds.Extents.z, 0);

            var theta = 0.0;
            var rho = math.length(nearPlaneCenterToBounds);
            if (rho > math.EPSILON)
            {
                var cosTheta = math.dot(m_Forward, nearPlaneCenterToBounds) / rho;
                cosTheta = math.clamp(cosTheta, -1, 1);
                theta = math.acos(cosTheta);
            }
            else
            {
                // Camera is inside bounds: nearPlaneCenterToBounds is zero so there is no
                // direction information. Fall back to the vector from camera position to the
                // bounds centre. This theta is kept for symmetry with the package version but
                // only affects max (priority) in the full implementation — here it is unused.
                var toCenterVec = data.Bounds.Center - m_Position;
                var toCenterDist = math.length(toCenterVec);
                if (toCenterDist > math.EPSILON)
                {
                    var cosTheta = math.dot(m_Forward, toCenterVec / toCenterDist);
                    theta = math.acos(math.clamp(cosTheta, -1.0, 1.0));
                }
            }

            // Content tiles (rho>0): tiles within the camera's FOV cone (theta≤halfFov) get
            // sseForMin=SSE — full quality regardless of distance or camera mode. Tiles outside
            // the viewport ramp from SSE (at the FOV edge) to SSE×32768 (directly behind),
            // collapsing off-screen content to near-zero polygons and freeing the full triangle
            // budget for what is actually visible.
            // Enclosing tiles (rho=0): always sseForMin=SSE so they are always refined,
            // exposing their children (e.g. desks inside a room tile).
            var sseForMin = rho > math.EPSILON && theta > m_HalfFovRadians
                ? ScreenSpaceError + (theta - m_HalfFovRadians) / (math.PI - m_HalfFovRadians) * (m_TargetSseFarBehind - ScreenSpaceError)
                : (double)ScreenSpaceError;

            var closestPointOnBound = m_NearPlaneCenter + nearPlaneCenterToBounds;
            var distance = math.length(closestPointOnBound - m_Position);
            distance = math.max(m_NearPlane, distance);

            var min = m_ProjectionFactor * sseForMin;
            if (!m_IsOrthographic)
                min *= distance;

            return (float)min;
        }

        void ComputeObserverData()
        {
            m_IsOrthographic = m_Camera.orthographic;

            m_ProjectionFactor = m_Camera.orthographic
                ? 2.0 * m_Camera.orthographicSize / m_Camera.pixelHeight
                : 2.0 * math.tan(math.radians(m_Camera.fieldOfView / 2)) / m_Camera.pixelHeight;

            m_NearPlane = m_Camera.nearClipPlane;
            m_FarPlane = m_Camera.farClipPlane;
            m_HalfFovRadians = m_IsOrthographic ? 0.0 : math.radians(m_Camera.fieldOfView / 2.0);

            var forward = m_Camera.transform.forward;
            m_Forward = new double3(forward.x, forward.y, forward.z);

            var position = m_Camera.transform.position;
            m_Position = new double3(position.x, position.y, position.z);

            m_NearPlaneCenter = new float3(0, 0, m_Camera.nearClipPlane);
            m_NearPlaneCenter = m_Camera.transform.TransformPoint(m_NearPlaneCenter);
            m_TargetSseFarBehind = ScreenSpaceError * math.pow(k_FarBehindMultiplierBase, ScreenSpaceErrorExponent);
        }
    }
}
