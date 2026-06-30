using System;
using System.Collections;
using UnityEngine;
using Unity.Cloud.HighPrecision.Runtime;

namespace Unity.Industry.Viewer.Streaming
{
    // Shared base for navigation options that place the streamed model onto a detected AR
    // surface (MobileARController, CameraPassThroughController). It collects the transform
    // manipulation and surface-placement logic that is identical between those controllers.
    // Platform-specific concerns (AR session setup, input, occlusion, world maps) stay in the
    // derived classes, so this base intentionally has no AR Foundation dependency.
    public abstract class ARPlacementNavigationOption : NavigationOption
    {
        public Action OnAssetPlaceOnSurfaceComplete;

        protected Vector3 m_OriginalPosition;
        protected Quaternion m_OriginalRotation;

        public void Scale(float value)
        {
            TransformController.Instance.transform.localScale = Vector3.one * value;
        }

        public void ResetPosition()
        {
            TransformController.Instance.transform.position = m_OriginalPosition;
        }

        public void ResetRotation()
        {
            TransformController.Instance.transform.rotation = m_OriginalRotation;
        }

        public void RotateZBy(float value)
        {
            TransformController.Instance.transform.Rotate(0f, 0f, value, Space.Self);
        }

        public void RotateYBy(float value)
        {
            TransformController.Instance.transform.Rotate(0f, value, 0f,Space.Self);
        }

        public void RotateXBy(float value)
        {
            TransformController.Instance.transform.Rotate(value, 0f, 0f,Space.Self);
        }

        public void RotateZ(float newValue)
        {
            TransformController.Instance.transform.rotation = Quaternion.Euler(TransformController.Instance.transform.eulerAngles.x,
                TransformController.Instance.transform.eulerAngles.y,
                newValue);
        }

        public void RotateY(float newValue)
        {
            TransformController.Instance.transform.rotation = Quaternion.Euler(TransformController.Instance.transform.eulerAngles.x,
                newValue,
                TransformController.Instance.transform.eulerAngles.z);
        }

        public void RotateX(float newValue)
        {
            TransformController.Instance.transform.rotation = Quaternion.Euler(newValue,
                TransformController.Instance.transform.eulerAngles.y,
                TransformController.Instance.transform.eulerAngles.z);
        }

        public void MoveZPosition(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.z = value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveYPosition(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.y = value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveXPosition(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.x = value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveZPositionBy(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.z += value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveYPositionBy(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.y += value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void MoveXPositionBy(float value)
        {
            var originalPos = TransformController.Instance.transform.position;
            originalPos.x += value;
            TransformController.Instance.transform.position = originalPos;
        }

        public void PlaceOnSurface()
        {
            TransformController.Instance.transform.position = new Vector3(
                TransformController.Instance.transform.position.x, m_OriginalPosition.y,
                TransformController.Instance.transform.position.z);
            StartCoroutine(WaitForBoundsUpdate());

            return;

            IEnumerator WaitForBoundsUpdate()
            {
                yield return null;
                StreamingModelController streamingModelController = FindAnyObjectByType<StreamingModelController>(FindObjectsInactive.Include);
                DoubleBounds tmpBounds = streamingModelController.GetWorldBounds();
                var height = (float)tmpBounds.Extents.y;
                var lowestPoint = (float)(tmpBounds.Center.y - height);
                var offsetY = m_OriginalPosition.y - lowestPoint;
                TransformController.Instance.transform.position = new Vector3(
                    TransformController.Instance.transform.position.x,
                    TransformController.Instance.transform.position.y + offsetY,
                    TransformController.Instance.transform.position.z);
                OnAssetPlaceOnSurfaceComplete?.Invoke();
            }
        }

        public override GameObject GetNavigationGameObject()
        {
            return null;
        }

        public override void SetDefaultView() { }

        public override void FocusToPoint(DoubleBounds bounds) { }

        public override void TranslateTo(Vector3 position, Quaternion rotation) { }

        public override void FollowPresenter(GameObject presenter) { }
    }
}
