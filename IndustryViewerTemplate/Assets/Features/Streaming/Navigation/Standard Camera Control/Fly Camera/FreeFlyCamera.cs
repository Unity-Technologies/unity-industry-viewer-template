using UnityEngine;
using Unity.Industry.Viewer.Navigation.StandardCameraControl.Shared;

namespace Unity.Industry.Viewer.Navigation.FlyCamera
{
    /// <summary>
    ///     A generic free fly camera with pan, move, rotation, orbit and automatic positioning features.
    /// </summary>
    public class FreeFlyCamera : StandardCamera
    {
        // Default values to hide missing call to SetupCameraSpeed (hotfix)
        /// <summary>
        ///     Move the camera in the specified local direction.
        /// </summary>
        /// <remarks>
        ///     After being called once, this method will continue to move the camera in
        ///     the specified direction. You need to call it with a zero vector to stop the camera from moving.
        /// </remarks>
        /// <param name="unitDir">A unit vector indicating the local direction in which the camera should move.</param>
        public override void MoveInLocalDirection(Vector3 unitDir)
        {
            m_MovingDirection = unitDir;
            UpdateSphericalMovement(false);
        }

        /// <summary>
        ///     Rotate the camera by adding an offset to the current rotation.
        /// </summary>
        /// <remarks>
        ///     The <see cref="angleOffset"/> is a rotation in azimuth coordinate where Y is up
        ///     axis (azimuth angle, clockwise) and X, right axis (altitude, clockwise)
        /// </remarks>
        /// <param name="angleOffset">A rotation around the y axis, then the x axis</param>
        public override void Rotate(Vector2 angleOffset)
        {
            m_DesiredRotationEuler += angleOffset;
            if (m_DesiredRotationEuler.x > 180)
                m_DesiredRotationEuler.x -= 360;
            m_DesiredRotationEuler.x = Mathf.Clamp(m_DesiredRotationEuler.x, -m_Settings.maxPitchAngle, m_Settings.maxPitchAngle);

            m_DesiredRotation =
                Quaternion.AngleAxis(m_DesiredRotationEuler.y, Vector3.up) *
                Quaternion.AngleAxis(m_DesiredRotationEuler.x, Vector3.right);

            m_DesiredLookAt = m_DesiredRotation * new Vector3(0.0f, 0.0f, (m_DesiredLookAt - m_DesiredPosition).magnitude) + m_DesiredPosition;

            UpdateSphericalMovement(false);
        }
    }
}
