using UnityEngine;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;

namespace Unity.Industry.Viewer.Navigation.OrbitCamera
{
    public class FreeOrbitCamera : StandardCamera
    {
        /// <summary>
        ///     Move on the forward axis of the camera. The operation is similar
        ///     to a zoom without changing FOV.
        /// </summary>
        /// <remarks>
        ///     This function does nothing if the new distance from lookAt is greater than
        ///     the maximum camera distance.
        /// </remarks>
        /// <param name="nbUnits">The number of units to move forward. A negative value
        ///     will move the camera away from the look at point.</param>
        public void MoveOnLookAtAxis(float nbUnits)
        {
            nbUnits *= GetDistanceFromLookAt() * m_Settings.moveOnAxisScaling;
            var originalDistanceFromLookAt = GetDistanceFromLookAt();

            var forward = m_DesiredRotation * Vector3.forward;

            var pos = m_DesiredPosition + forward * nbUnits;

            m_DesiredPosition = pos;

            if (originalDistanceFromLookAt - nbUnits < m_Settings.minDistanceFromLookAt)
            {
                m_DesiredLookAt = m_DesiredPosition + forward * m_Settings.minDistanceFromLookAt;
            }

            UpdateSphericalMovement(false);
        }
        
        /// <summary>
        ///     Drag the camera on the current frustum plane. If the
        ///     camera is looking forward, <see cref="Pan"/> will drag
        ///     the camera on its local up and right vectors.
        /// </summary>
        /// <param name="offset"></param>
        public void Pan(Vector3 offset)
        {
            offset = m_DesiredRotation * (offset * m_Settings.panScaling);
            MovePosition(offset);
            UpdateSphericalMovement(false);
        }
        
        /// <summary>
        ///     Move the current position of the camera by an offset.
        /// </summary>
        /// <param name="offset">The offset by which the camera should be moved.</param>
        public void MovePosition(Vector3 offset)
        {
            m_DesiredPosition += offset;
            m_DesiredLookAt += offset;
            UpdateSphericalMovement(false);
        }
        
        /// <summary>
        ///     Orbit around the look at point with up vector fixed to Y.
        /// </summary>
        /// <remarks>
        ///     The <see cref="angleOffset"/> is the same value that is provided in <see cref="Rotate"/>
        ///     because the orbit rotation and camera rotation matches when orbiting.
        /// </remarks>
        /// <param name="angleOffset"></param>
        public void OrbitAroundLookAt(Vector2 angleOffset)
        {
            angleOffset *= m_Settings.orbitScaling;
            m_DesiredRotationEuler += angleOffset;
            if (m_DesiredRotationEuler.x > 180)
                m_DesiredRotationEuler.x -= 360;
            m_DesiredRotationEuler.x = Mathf.Clamp(m_DesiredRotationEuler.x, -m_Settings.maxPitchAngle, m_Settings.maxPitchAngle);

            m_DesiredRotation =
                Quaternion.AngleAxis(m_DesiredRotationEuler.y, Vector3.up) *
                Quaternion.AngleAxis(m_DesiredRotationEuler.x, Vector3.right);
           
            var negDistance = new Vector3(0.0f, 0.0f, -GetDistanceFromLookAt());
            m_DesiredPosition = m_DesiredRotation * negDistance + m_DesiredLookAt;

            UpdateSphericalMovement(true);
        }
    }
}
