/* Modified version of https://github.com/HiddenMonk/Unity3DRuntimeTransformGizmo */
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CommandUndoRedo;
using UnityEngine.InputSystem;
using System;

namespace RuntimeGizmos
{
    //To be safe, if you are changing any transforms hierarchy, such as parenting an object to something,
    //you should call ClearTargets before doing so just to be sure nothing unexpected happens... as well as call UndoRedoManager.Clear()
    //For example, if you select an object that has children, move the children elsewhere, deselect the original object, then try to add those old children to the selection, I think it wont work.
    public class TransformGizmo : MonoBehaviour
    {
        private struct CustomRay
        {
            public Ray Ray { private set; get; }
            public int Id { private set; get; }
            public bool IsSelecting { private set; get; }
            
            public CustomRay(Ray ray, int id, bool isSelecting)
            {
                Ray = ray;
                Id = id;
                IsSelecting = isSelecting;
            }

            public void SetSelectingValue(bool value)
            {
                IsSelecting = value;
            }

            public void UpdateRay(Ray ray)
            {
                Ray = new Ray(ray.origin, ray.direction);
            }
        }
        
        public void SetTarget(Transform transformVal)
        {
            overrideTarget = transformVal;
            if (overrideTarget == null)
            {
                ClearTargets();
            }
            else
            {
                ClearAndAddTarget(overrideTarget);
            }
        }

        public static TransformGizmo CreateHandle(Camera targetCamera, Transform targetTransform)
        {
            TransformGizmo gizmo = targetCamera.gameObject.AddComponent<TransformGizmo>();
            Instance = gizmo;
            gizmo.myCamera = targetCamera;
            gizmo.SetTarget(targetTransform);
            return gizmo;
        }
        
        public TransformGizmo WithActions(InputAction inputMoveAction, InputAction selectAction)
        {
            _selectAction = selectAction;
            _inputMoveAction = inputMoveAction;
            return this;
        }

        public TransformGizmo WithLayer(LayerMask targetLayerMask)
        {
            selectionMask = targetLayerMask;
            return this;
        }
        
        public Action<Ray, int> InputMoveAction;
        public Action<bool, Ray, int> SelectAction;

        private CustomRay? customRay;

        public static TransformGizmo Instance;
        
        public bool snapOverride = false;
        public void SetSpace(TransformSpace transformSpace) => space = transformSpace;
        public void SetSpace(int transformSpaceIndex) => space = (TransformSpace)transformSpaceIndex;
        public TransformSpace space = TransformSpace.Global;
        public void SetType(TransformType type) => transformType = type;
        public void SetType(int typeIndex) => transformType = (TransformType)typeIndex;
        public void SetShowAxis(ShowAxis type) => showAxis = type;
        public void SetShowAxis(int typeIndex) => showAxis = (ShowAxis)typeIndex;
        public void SetLayoutMask(LayerMask mask) => selectionMask = mask;
        public TransformType transformType = TransformType.Move;
        public ShowAxis showAxis = ShowAxis.All;
        public TransformPivot pivot = TransformPivot.Pivot;
        public CenterType centerType = CenterType.All;
        public ScaleType scaleType = ScaleType.FromPoint;
        
        private InputAction _inputMoveAction;
        private InputAction _selectAction;

        //These are the same as the unity editor hotkeys
        /*private Key SetSpaceToggle = Key.X;
        private Key SetPivotModeToggle = Key.Z;
        private Key SetCenterTypeToggle = Key.C;
        private Key SetScaleTypeToggle = Key.S;
        private Key translationSnapping = Key.LeftCtrl;
        private Key ActionKey = Key.LeftShift; //Its set to shift instead of control so that while in the editor we dont accidentally undo editor changes =/
        private Key UndoAction = Key.Z;
        private Key RedoAction = Key.Y;*/
        
        private Color xColor = new Color(1, 0, 0, 0.8f);
        private Color yColor = new Color(0, 1, 0, 0.8f);
        private Color yDepthColor = new Color(0, 0.5f, 0, 0.4f);
        private Color zColor = new Color(0, 0, 1, 0.8f);
        private Color allColor = new Color(.7f, .7f, .7f, 0.8f);
        private Color selectedColor = new Color(1, 1, 0, 0.8f);
        private Color hoverColor = new Color(1, .75f, 0, 0.8f);
        private float planesOpacity = .5f;
        //public Color rectPivotColor = new Color(0, 0, 1, 0.8f);
        //public Color rectCornerColor = new Color(0, 0, 1, 0.8f);
        //public Color rectAnchorColor = new Color(.7f, .7f, .7f, 0.8f);
        //public Color rectLineColor = new Color(.7f, .7f, .7f, 0.8f);

        public float movementSnap = .25f;
        public float rotationSnap = 15f;
        public float scaleSnap = 1f;

        private float handleLength = .25f;
        private float handleWidth = .003f;
        private float planeSize = .035f;
        private float triangleSize = .03f;
        private float boxSize = .03f;
        private int circleDetail = 40;
        private float allMoveHandleLengthMultiplier = 1f;
        private float allRotateHandleLengthMultiplier = 1.4f;
        private float allScaleHandleLengthMultiplier = 1.6f;
        private float minSelectedDistanceCheck = .01f;
        private float moveSpeedMultiplier = 1f;
        private float scaleSpeedMultiplier = 1f;
        private float rotateSpeedMultiplier = 1f;
        private float allRotateSpeedMultiplier = 20f;

        //If circularRotationMethod is true, when rotating you will need to move your mouse around the object as if turning a wheel.
        //If circularRotationMethod is false, when rotating you can just click and drag in a line to rotate.
        public bool circularRotationMethod;

        //Mainly for if you want the pivot point to update correctly if selected objects are moving outside the transformgizmo.
        //Might be poor on performance if lots of objects are selected...
        public bool forceUpdatePivotPointOnChange = true;

        public int maxUndoStored = 100;

        [SerializeField]
        private LayerMask selectionMask = Physics.DefaultRaycastLayers;
        
        public Action OnHandlerSelected;
        public Action OnHandlerReleased;

        public Transform overrideTarget { get; private set; }
        public Camera myCamera { get; private set; }
        public bool isTransforming { get; private set; }
        public float totalScaleAmount { get; private set; }
        public Quaternion totalRotationAmount { get; private set; }
        public Axis translatingAxis { get { return nearAxis; } }
        public Axis translatingAxisPlane { get { return planeAxis; } }
        public bool hasTranslatingAxisPlane { get { return translatingAxisPlane != Axis.None && translatingAxisPlane != Axis.Any; } }
        public TransformType transformingType { get { return translatingType; } }

        public Vector3 pivotPoint { get; private set; }
        Vector3 totalCenterPivotPoint;

        public Transform mainTargetRoot
        {
            get { return (targetRootsOrdered.Count > 0) ? targetRootsOrdered[targetRootsOrdered.Count - 1] : null; }
        }

        AxisInfo axisInfo;
        Axis nearAxis = Axis.None;
        Axis planeAxis = Axis.None;
        TransformType translatingType;

        AxisVectors handleLines = new AxisVectors();
        AxisVectors handlePlanes = new AxisVectors();
        AxisVectors handleTriangles = new AxisVectors();
        AxisVectors handleSquares = new AxisVectors();
        AxisVectors circlesLines = new AxisVectors();

        //We use a HashSet and a List for targetRoots so that we get fast lookup with the hashset while also keeping track of the order with the list.
        List<Transform> targetRootsOrdered = new List<Transform>();
        Dictionary<Transform, TargetInfo> targetRoots = new Dictionary<Transform, TargetInfo>();
        HashSet<Renderer> highlightedRenderers = new HashSet<Renderer>();
        HashSet<Transform> children = new HashSet<Transform>();

        List<Transform> childrenBuffer = new List<Transform>();
        List<Renderer> renderersBuffer = new List<Renderer>();
        List<Material> materialsBuffer = new List<Material>();

        WaitForEndOfFrame waitForEndOFFrame = new WaitForEndOfFrame();
        Coroutine forceUpdatePivotCoroutine;

        static Material lineMaterial;
        static Material outlineMaterial;

        private Dictionary<string, MeshRenderer> quadRenderers = new Dictionary<string, MeshRenderer>();
        private Dictionary<string, MeshRenderer> triangleRenderers = new Dictionary<string, MeshRenderer>();
        
        bool selecting = false;
        private Vector3 _previousPointerPosition;

        void Awake()
        {
            SetMaterial();
        }

        private void Start()
        {
            if (_inputMoveAction != null)
            {
                _inputMoveAction.performed += InputMoveActionOnperformed;
            }
            
            if (_selectAction != null)
            {
                _selectAction.performed += SelectActionOnperformed;
            }
            
            InputMoveAction -= OnInputMove;
            SelectAction -= OnSelectEvent;
            
            InputMoveAction += OnInputMove;
            SelectAction += OnSelectEvent;
        }

        void OnEnable()
        {
            forceUpdatePivotCoroutine = StartCoroutine(ForceUpdatePivotPointAtEndOfFrame());
        }

        void OnDisable()
        {
            ClearTargets(); //Just so things gets cleaned up, such as removing any materials we placed on objects.
            StopCoroutine(forceUpdatePivotCoroutine);
        }

        void OnDestroy()
        {
            ClearAllHighlightedRenderers();
            foreach (var quadRenderer in quadRenderers.Values)
            {
                Destroy(quadRenderer.gameObject);
            }
            foreach (var triangleRenderer in triangleRenderers.Values)
            {
                Destroy(triangleRenderer.gameObject);
            }
            
            if (_inputMoveAction != null)
            {
                _inputMoveAction.performed -= InputMoveActionOnperformed;
            }
            
            if (_selectAction != null)
            {
                _selectAction.performed -= SelectActionOnperformed;
            }
            
            InputMoveAction -= OnInputMove;
            SelectAction -= OnSelectEvent;
        }

        void Update()
        {
            if (overrideTarget)
            {
                // use target selection override
                if (mainTargetRoot != overrideTarget)
                    ClearAndAddTarget(overrideTarget);
            }

            if (mainTargetRoot == null)
            {
                RemoveAllHandles();
            }
            else
            {
                DrawHandles();
                // HandleUndoRedo();
                // SetSpaceAndType();
            }
        }

        void LateUpdate()
        {
            if (mainTargetRoot == null)
                return;

            //We run this in lateupdate since coroutines run after update and we want our gizmos to have the updated target transform position after TransformSelected()
            SetAxisInfo();

            SetLines();
        }
        
        private void OnInputMove(Ray ray, int rayId)
        {
            if (nearAxis != Axis.None)
            {
                if(customRay.HasValue && customRay.Value.Id == rayId)
                {
                    SetNearAxis(ray);
                    if (nearAxis != Axis.None)
                    {
                        CustomRay updatedRay = customRay.Value;
                        updatedRay.UpdateRay(ray);
                        customRay = updatedRay;
                        _previousPointerPosition = ray.origin;
                    }
                    else
                    {
                        customRay = null;
                    }
                }
                return;
            }
            SetNearAxis(ray);

            if (nearAxis == Axis.None) return;
            customRay = new CustomRay(ray, rayId, false);
            _previousPointerPosition = ray.origin;
        }
        
        private void OnSelectEvent(bool beingPress, Ray ray, int rayID)
        {
            if (beingPress && customRay.HasValue && customRay.Value.Id == rayID && !customRay.Value.IsSelecting)
            {
                selecting = true;
                customRay.Value.SetSelectingValue(true);
            } else if (!beingPress && customRay.HasValue && customRay.Value.Id == rayID)
            {
                selecting = false;
                customRay.Value.SetSelectingValue(false);
            }
            
            if (selecting && mainTargetRoot != null)
            {
                if (nearAxis == Axis.None)
                {
                    SetNearAxis(ray);
                }
                if(nearAxis == Axis.None) return;
                StartCoroutine(TransformSelected(translatingType));
            }
        }
        
        private void SelectActionOnperformed(InputAction.CallbackContext inputActionContext)
        {
            if (inputActionContext.phase == InputActionPhase.Canceled)
            {
                selecting = false;
                return;
            }
            if(!inputActionContext.performed) return;
            selecting = inputActionContext.ReadValueAsButton();
            if (selecting && mainTargetRoot != null)
            {
                if (nearAxis == Axis.None)
                {
                    SetNearAxis(GetInputRay());
                }
                if(nearAxis == Axis.None) return;
                StartCoroutine(TransformSelected(translatingType));
            }
        }

        private void InputMoveActionOnperformed(InputAction.CallbackContext inputActionContext)
        {
            if (!inputActionContext.performed) return;
            SetNearAxis(GetInputRay());
            Vector2 currentPointerPosition = inputActionContext.ReadValue<Vector2>();
            _previousPointerPosition = currentPointerPosition;
        }

        void RemoveAllHandles()
        {
            List<Vector3> emptyList = new List<Vector3>();

            // Remove all quad handles
            DrawQuads("LineZ", emptyList, Color.clear);
            DrawQuads("LineX", emptyList, Color.clear);
            DrawQuads("LineY", emptyList, Color.clear);
            DrawQuads("PlaneZ", emptyList, Color.clear);
            DrawQuads("PlaneX", emptyList, Color.clear);
            DrawQuads("PlaneY", emptyList, Color.clear);
            DrawQuads("SquareX", emptyList, Color.clear);
            DrawQuads("SquareY", emptyList, Color.clear);
            DrawQuads("SquareZ", emptyList, Color.clear);
            DrawQuads("SquareAll", emptyList, Color.clear);
            DrawQuads("CircleAll", emptyList, Color.clear);
            DrawQuads("CircleX", emptyList, Color.clear);
            DrawQuads("CircleY", emptyList, Color.clear);
            DrawQuads("CircleZ", emptyList, Color.clear);

            // Remove all triangle handles
            DrawTriangles("TriangleX", emptyList, Color.clear);
            DrawTriangles("TriangleY", emptyList, Color.clear);
            DrawTriangles("TriangleZ", emptyList, Color.clear);
        }

        void DrawHandles()
        {
            if (mainTargetRoot == null)
                return;
            Color xColor = (nearAxis == Axis.X) ? (isTransforming) ? selectedColor : hoverColor : this.xColor;
            Color yColor = (nearAxis == Axis.Y) ? (isTransforming) ? selectedColor : hoverColor : this.yColor;
            Color yDepthColor = (nearAxis == Axis.Y) ? (isTransforming) ? selectedColor : hoverColor : this.yDepthColor;
            Color zColor = (nearAxis == Axis.Z) ? (isTransforming) ? selectedColor : hoverColor : this.zColor;
            Color allColor = (nearAxis == Axis.Any) ? (isTransforming) ? selectedColor : hoverColor : this.allColor;

            //Note: The order of drawing the axis decides what gets drawn over what.
            TransformType moveOrScaleType = (transformType == TransformType.Scale || (isTransforming && translatingType == TransformType.Scale)) ? TransformType.Scale : TransformType.Move;
            DrawQuads("LineZ", handleLines.z, GetColor(moveOrScaleType, this.zColor, zColor, hasTranslatingAxisPlane));
            DrawQuads("LineX", handleLines.x, GetColor(moveOrScaleType, this.xColor, xColor, hasTranslatingAxisPlane));
            DrawQuads("LineY", handleLines.y, GetColor(moveOrScaleType, this.yColor, yColor, hasTranslatingAxisPlane));

            DrawTriangles("TriangleX", handleTriangles.x, GetColor(TransformType.Move, this.xColor, xColor, hasTranslatingAxisPlane));
            DrawTriangles("TriangleY", handleTriangles.y, GetColor(TransformType.Move, this.yColor, yColor, hasTranslatingAxisPlane));
            DrawTriangles("TriangleZ", handleTriangles.z, GetColor(TransformType.Move, this.zColor, zColor, hasTranslatingAxisPlane));

            DrawQuads("PlaneZ", handlePlanes.z, GetColor(TransformType.Move, this.zColor, zColor, planesOpacity, !hasTranslatingAxisPlane));
            DrawQuads("PlaneX", handlePlanes.x, GetColor(TransformType.Move, this.xColor, xColor, planesOpacity, !hasTranslatingAxisPlane));
            DrawQuads("PlaneY", handlePlanes.y, GetColor(TransformType.Move, this.yColor, yColor, planesOpacity, !hasTranslatingAxisPlane));

            DrawQuads("SquareX", handleSquares.x, GetColor(TransformType.Scale, this.xColor, xColor));
            DrawQuads("SquareY", handleSquares.y, GetColor(TransformType.Scale, this.yColor, yColor));
            DrawQuads("SquareZ", handleSquares.z, GetColor(TransformType.Scale, this.zColor, zColor));
            DrawQuads("SquareAll", handleSquares.all, GetColor(TransformType.Scale, this.allColor, allColor));

            DrawQuads("CircleAll", circlesLines.all, GetColor(TransformType.Rotate, this.allColor, allColor));
            DrawQuads("CircleX", circlesLines.x, GetColor(TransformType.Rotate, this.xColor, xColor));

            if (showAxis == ShowAxis.Y)
                DrawQuads("CircleY", circlesLines.y, GetColor(TransformType.Rotate, this.yColor, yColor), circlesLines.depthTest, GetColor(TransformType.Rotate, this.yDepthColor, yDepthColor));
            else
                DrawQuads("CircleY", circlesLines.y, GetColor(TransformType.Rotate, this.yColor, yColor));

            DrawQuads("CircleZ", circlesLines.z, GetColor(TransformType.Rotate, this.zColor, zColor));
        }

        Color GetColor(TransformType type, Color normalColor, Color nearColor, bool forceUseNormal = false)
        {
            return GetColor(type, normalColor, nearColor, false, 1, forceUseNormal);
        }
        Color GetColor(TransformType type, Color normalColor, Color nearColor, float alpha, bool forceUseNormal = false)
        {
            return GetColor(type, normalColor, nearColor, true, alpha, forceUseNormal);
        }
        Color GetColor(TransformType type, Color normalColor, Color nearColor, bool setAlpha, float alpha, bool forceUseNormal = false)
        {
            Color color;
            if (!forceUseNormal && TranslatingTypeContains(type, false))
            {
                color = nearColor;
            }
            else
            {
                color = normalColor;
            }

            if (setAlpha)
            {
                color.a = alpha;
            }

            return color;
        }

        /*void HandleUndoRedo()
        {
            if (maxUndoStored != UndoRedoManager.maxUndoStored)
            { UndoRedoManager.maxUndoStored = maxUndoStored; }

            if (Keyboard.current[ActionKey].isPressed)
            {
                if (Keyboard.current[UndoAction].isPressed)
                {
                    UndoRedoManager.Undo();
                }
                else if (Keyboard.current[RedoAction].isPressed)
                {
                    UndoRedoManager.Redo();
                }
            }
        }*/

        //We only support scaling in local space.
        public TransformSpace GetProperTransformSpace()
        {
            return transformType == TransformType.Scale ? TransformSpace.Local : space;
        }

        public bool TransformTypeContains(TransformType type)
        {
            return TransformTypeContains(transformType, type);
        }
        public bool TranslatingTypeContains(TransformType type, bool checkIsTransforming = true)
        {
            TransformType transType = !checkIsTransforming || isTransforming ? translatingType : transformType;
            return TransformTypeContains(transType, type);
        }
        public bool TransformTypeContains(TransformType mainType, TransformType type)
        {
            return ExtTransformType.TransformTypeContains(mainType, type, GetProperTransformSpace());
        }

        public float GetHandleLength(TransformType type, Axis axis = Axis.None, bool multiplyDistanceMultiplier = true)
        {
            float length = handleLength;
            if (transformType == TransformType.All)
            {
                if (type == TransformType.Move)
                    length *= allMoveHandleLengthMultiplier;
                if (type == TransformType.Rotate)
                    length *= allRotateHandleLengthMultiplier;
                if (type == TransformType.Scale)
                    length *= allScaleHandleLengthMultiplier;
            }

            if (multiplyDistanceMultiplier)
                length *= GetDistanceMultiplier();

            if (type == TransformType.Scale && isTransforming && (translatingAxis == axis || translatingAxis == Axis.Any))
                length += totalScaleAmount;

            return length;
        }

        /*void SetSpaceAndType()
        {
            if (ActionKey != Key.None && Keyboard.current[ActionKey].isPressed)
                return;

            if (!isTransforming)
                translatingType = transformType;

            if (SetPivotModeToggle != Key.None && Keyboard.current[SetPivotModeToggle].isPressed)
            {
                if (pivot == TransformPivot.Pivot)
                    pivot = TransformPivot.Center;
                else if (pivot == TransformPivot.Center)
                    pivot = TransformPivot.Pivot;

                SetPivotPoint();
            }

            if (SetCenterTypeToggle != Key.None && Keyboard.current[SetCenterTypeToggle].isPressed)
            {
                if (centerType == CenterType.All)
                    centerType = CenterType.Solo;
                else if (centerType == CenterType.Solo)
                    centerType = CenterType.All;

                SetPivotPoint();
            }

            if (SetSpaceToggle != Key.None && Keyboard.current[SetSpaceToggle].isPressed)
            {
                if (space == TransformSpace.Global)
                    space = TransformSpace.Local;
                else if (space == TransformSpace.Local)
                    space = TransformSpace.Global;
            }

            if (SetScaleTypeToggle != Key.None && Keyboard.current[SetScaleTypeToggle].isPressed)
            {
                if (scaleType == ScaleType.FromPoint)
                    scaleType = ScaleType.FromPointOffset;
                else if (scaleType == ScaleType.FromPointOffset)
                    scaleType = ScaleType.FromPoint;
            }

            if (transformType == TransformType.Scale)
            {
                if (pivot == TransformPivot.Pivot)
                    scaleType = ScaleType.FromPoint; //FromPointOffset can be inaccurate and should only really be used in Center mode if desired.
            }
        }*/

        IEnumerator TransformSelected(TransformType transType)
        {
            isTransforming = true;
            OnHandlerSelected?.Invoke();
            totalScaleAmount = 0;
            totalRotationAmount = Quaternion.identity;

            Vector3 originalPivot = pivotPoint;

            Vector3 otherAxis1, otherAxis2;
            Vector3 axis = GetNearAxisDirection(out otherAxis1, out otherAxis2);
            Vector3 planeNormal = hasTranslatingAxisPlane ? axis : (transform.position - originalPivot).normalized;
            Vector3 projectedAxis = Vector3.ProjectOnPlane(axis, planeNormal).normalized;
            Vector3 previousInputPosition = Vector3.zero;

            Vector3 currentSnapMovementAmount = Vector3.zero;
            float currentSnapRotationAmount = 0;
            float currentSnapScaleAmount = 0;

            List<ICommand> transformCommands = new List<ICommand>();
            for (int i = 0; i < targetRootsOrdered.Count; i++)
            {
                transformCommands.Add(new TransformCommand(this, targetRootsOrdered[i]));
            }
            
            do
            {
                Ray inputRay = GetInputRay();
                
                Vector3 inputPosition = Vector3.zero;

                if (customRay.HasValue)
                {
                    inputPosition = Geometry.LinePlaneIntersect(customRay.Value.Ray.origin, customRay.Value.Ray.direction, originalPivot, planeNormal);
                }
                else
                {
                    inputPosition = Geometry.LinePlaneIntersect(inputRay.origin, inputRay.direction, originalPivot, planeNormal);
                }
                
                bool isSnapping = snapOverride;// || Keyboard.current[translationSnapping].isPressed;

                if (previousInputPosition != Vector3.zero && inputPosition != Vector3.zero)
                {
                    if (transType == TransformType.Move)
                    {
                        Vector3 movement = Vector3.zero;

                        if (hasTranslatingAxisPlane)
                        {
                            movement = inputPosition - previousInputPosition;
                        }
                        else
                        {
                            float moveAmount = ExtVector3.MagnitudeInDirection(inputPosition - previousInputPosition, projectedAxis) * moveSpeedMultiplier;
                            movement = axis * moveAmount;
                        }

                        if (isSnapping && movementSnap > 0)
                        {
                            currentSnapMovementAmount += movement;
                            movement = Vector3.zero;

                            if (hasTranslatingAxisPlane)
                            {
                                float amountInAxis1 = ExtVector3.MagnitudeInDirection(currentSnapMovementAmount, otherAxis1);
                                float amountInAxis2 = ExtVector3.MagnitudeInDirection(currentSnapMovementAmount, otherAxis2);

                                float remainder1;
                                float snapAmount1 = CalculateSnapAmount(movementSnap, amountInAxis1, out remainder1);
                                float remainder2;
                                float snapAmount2 = CalculateSnapAmount(movementSnap, amountInAxis2, out remainder2);

                                if (snapAmount1 != 0)
                                {
                                    Vector3 snapMove = (otherAxis1 * snapAmount1);
                                    movement += snapMove;
                                    currentSnapMovementAmount -= snapMove;
                                }
                                if (snapAmount2 != 0)
                                {
                                    Vector3 snapMove = (otherAxis2 * snapAmount2);
                                    movement += snapMove;
                                    currentSnapMovementAmount -= snapMove;
                                }
                            }
                            else
                            {
                                float remainder;
                                float snapAmount = CalculateSnapAmount(movementSnap, currentSnapMovementAmount.magnitude, out remainder);

                                if (snapAmount != 0)
                                {
                                    movement = currentSnapMovementAmount.normalized * snapAmount;
                                    currentSnapMovementAmount = currentSnapMovementAmount.normalized * remainder;
                                }
                            }
                        }

                        for (int i = 0; i < targetRootsOrdered.Count; i++)
                        {
                            Transform target = targetRootsOrdered[i];

                            target.Translate(movement, Space.World);
                        }

                        SetPivotPointOffset(movement);
                    }
                    else if (transType == TransformType.Scale)
                    {
                        Vector3 projected = (nearAxis == Axis.Any) ? transform.right : projectedAxis;
                        float scaleAmount = ExtVector3.MagnitudeInDirection(inputPosition - previousInputPosition, projected) * scaleSpeedMultiplier;

                        if (isSnapping && scaleSnap > 0)
                        {
                            currentSnapScaleAmount += scaleAmount;
                            scaleAmount = 0;

                            float remainder;
                            float snapAmount = CalculateSnapAmount(scaleSnap, currentSnapScaleAmount, out remainder);

                            if (snapAmount != 0)
                            {
                                scaleAmount = snapAmount;
                                currentSnapScaleAmount = remainder;
                            }
                        }

                        //WARNING - There is a bug in unity 5.4 and 5.5 that causes InverseTransformDirection to be affected by scale which will break negative scaling. Not tested, but updating to 5.4.2 should fix it - https://issuetracker.unity3d.com/issues/transformdirection-and-inversetransformdirection-operations-are-affected-by-scale
                        Vector3 localAxis = (GetProperTransformSpace() == TransformSpace.Local && nearAxis != Axis.Any) ? mainTargetRoot.InverseTransformDirection(axis) : axis;

                        Vector3 targetScaleAmount = Vector3.one;
                        if (nearAxis == Axis.Any)
                            targetScaleAmount = (ExtVector3.Abs(mainTargetRoot.localScale.normalized) * scaleAmount);
                        else
                            targetScaleAmount = localAxis * scaleAmount;

                        for (int i = 0; i < targetRootsOrdered.Count; i++)
                        {
                            Transform target = targetRootsOrdered[i];

                            Vector3 targetScale = target.localScale + targetScaleAmount;

                            if (pivot == TransformPivot.Pivot)
                            {
                                target.localScale = targetScale;
                            }
                            else if (pivot == TransformPivot.Center)
                            {
                                if (scaleType == ScaleType.FromPoint)
                                {
                                    target.SetScaleFrom(originalPivot, targetScale);
                                }
                                else if (scaleType == ScaleType.FromPointOffset)
                                {
                                    target.SetScaleFromOffset(originalPivot, targetScale);
                                }
                            }
                        }

                        totalScaleAmount += scaleAmount;
                    }
                    else if (transType == TransformType.Rotate)
                    {
                        float rotateAmount = 0;
                        Vector3 rotationAxis = axis;

                        if (nearAxis == Axis.Any)
                        {
                            Vector3 inputDelta = GetInputDelta();
                            Vector3 rotation = transform.TransformDirection(new Vector3(inputDelta.y * Time.unscaledDeltaTime, -inputDelta.x * Time.unscaledDeltaTime, 0));
                            Quaternion.Euler(rotation).ToAngleAxis(out rotateAmount, out rotationAxis);
                            rotateAmount *= allRotateSpeedMultiplier;
                        }
                        else
                        {
                            if (circularRotationMethod)
                            {
                                float angle = Vector3.SignedAngle(previousInputPosition - originalPivot, inputPosition - originalPivot, axis);
                                rotateAmount = angle * rotateSpeedMultiplier;
                            }
                            else
                            {
                                Vector3 projected = (nearAxis == Axis.Any || ExtVector3.IsParallel(axis, planeNormal)) ? planeNormal : Vector3.Cross(axis, planeNormal);
                                rotateAmount = (ExtVector3.MagnitudeInDirection(inputPosition - previousInputPosition, projected) * (rotateSpeedMultiplier * 100f)) / GetDistanceMultiplier();
                            }
                        }

                        if (isSnapping && rotationSnap > 0)
                        {
                            currentSnapRotationAmount += rotateAmount;
                            rotateAmount = 0;

                            float remainder;
                            float snapAmount = CalculateSnapAmount(rotationSnap, currentSnapRotationAmount, out remainder);

                            if (snapAmount != 0)
                            {
                                rotateAmount = snapAmount;
                                currentSnapRotationAmount = remainder;
                            }
                        }

                        for (int i = 0; i < targetRootsOrdered.Count; i++)
                        {
                            Transform target = targetRootsOrdered[i];

                            if (pivot == TransformPivot.Pivot)
                            {
                                target.Rotate(rotationAxis, rotateAmount, Space.World);
                            }
                            else if (pivot == TransformPivot.Center)
                            {
                                target.RotateAround(originalPivot, rotationAxis, rotateAmount);
                            }
                        }

                        var rotationAmount = rotationAxis * rotateAmount;

                        totalRotationAmount *= showAxis switch
                        {
                            ShowAxis.Y => Quaternion.Euler(Vector3.up * rotationAmount.y),
                            ShowAxis.X => Quaternion.Euler(Vector3.right * rotationAmount.x),
                            ShowAxis.Z => Quaternion.Euler(Vector3.forward * rotationAmount.z),
                            _ => Quaternion.Euler(rotationAmount)
                        };
                    }
                }

                previousInputPosition = inputPosition;

                yield return null;
            } while (selecting);
            for (int i = 0; i < transformCommands.Count; i++)
            {
                ((TransformCommand)transformCommands[i]).StoreNewTransformValues();
            }
            CommandGroup commandGroup = new CommandGroup();
            commandGroup.Set(transformCommands);
            UndoRedoManager.Insert(commandGroup);

            totalRotationAmount = Quaternion.identity;
            totalScaleAmount = 0;
            isTransforming = false;
            OnHandlerReleased?.Invoke();
            SetTranslatingAxis(transformType, Axis.None);
            
            SetPivotPoint();
        }

        float CalculateSnapAmount(float snapValue, float currentAmount, out float remainder)
        {
            remainder = 0;
            if (snapValue <= 0)
                return currentAmount;

            float currentAmountAbs = Mathf.Abs(currentAmount);
            if (currentAmountAbs > snapValue)
            {
                remainder = currentAmountAbs % snapValue;
                return snapValue * (Mathf.Sign(currentAmount) * Mathf.Floor(currentAmountAbs / snapValue));
            }

            return 0;
        }

        Vector3 GetNearAxisDirection(out Vector3 otherAxis1, out Vector3 otherAxis2)
        {
            otherAxis1 = otherAxis2 = Vector3.zero;

            if (nearAxis != Axis.None)
            {
                if (nearAxis == Axis.X)
                {
                    otherAxis1 = axisInfo.yDirection;
                    otherAxis2 = axisInfo.zDirection;
                    return axisInfo.xDirection;
                }
                if (nearAxis == Axis.Y)
                {
                    otherAxis1 = axisInfo.xDirection;
                    otherAxis2 = axisInfo.zDirection;
                    return axisInfo.yDirection;
                }
                if (nearAxis == Axis.Z)
                {
                    otherAxis1 = axisInfo.xDirection;
                    otherAxis2 = axisInfo.yDirection;
                    return axisInfo.zDirection;
                }
                if (nearAxis == Axis.Any)
                {
                    return Vector3.one;
                }
            }

            return Vector3.zero;
        }

        Vector3 GetInputDelta()
        {
            Vector3 inputDelta = Vector3.zero;
            if(_inputMoveAction == null && !customRay.HasValue) return inputDelta;

            if (_inputMoveAction == null && customRay.HasValue){
                
                // Use the rotation of the VR controller to calculate the delta
                Quaternion currentRotation = Quaternion.LookRotation(customRay.Value.Ray.direction.normalized);
                Quaternion previousRotation = Quaternion.LookRotation(_previousPointerPosition.normalized);
                Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);

                inputDelta = deltaRotation * Vector3.forward;
                
                return inputDelta;
            }
            
            inputDelta = _inputMoveAction.ReadValue<Vector2>() - new Vector2( _previousPointerPosition.x, _previousPointerPosition.y);
            return inputDelta;
        }

        Ray GetInputRay()
        {
            if (_inputMoveAction == null) return new Ray();
            Vector2 moveValue = _inputMoveAction.ReadValue<Vector2>();
            return myCamera.ScreenPointToRay(moveValue);
        }

        public void AddTarget(Transform target, bool addCommand = true)
        {
            if (target != null)
            {
                if (targetRoots.ContainsKey(target))
                    return;
                if (children.Contains(target))
                    return;

                if (addCommand)
                    UndoRedoManager.Insert(new AddTargetCommand(this, target, targetRootsOrdered));

                AddTargetRoot(target);
                // AddTargetHighlightedRenderers(target);

                SetPivotPoint();
            }
        }

        public void RemoveTarget(Transform target, bool addCommand = true)
        {
            if (target != null)
            {
                if (!targetRoots.ContainsKey(target))
                    return;

                if (addCommand)
                    UndoRedoManager.Insert(new RemoveTargetCommand(this, target));

                // RemoveTargetHighlightedRenderers(target);
                RemoveTargetRoot(target);

                SetPivotPoint();
            }
        }

        public void ClearTargets(bool addCommand = true)
        {
            if (addCommand)
                UndoRedoManager.Insert(new ClearTargetsCommand(this, targetRootsOrdered));

            // ClearAllHighlightedRenderers();
            targetRoots.Clear();
            targetRootsOrdered.Clear();
            children.Clear();
        }

        void ClearAndAddTarget(Transform target)
        {
            UndoRedoManager.Insert(new ClearAndAddTargetCommand(this, target, targetRootsOrdered));

            ClearTargets(false);
            AddTarget(target, false);
        }

        void GetTargetRenderers(Transform target, List<Renderer> renderers)
        {
            renderers.Clear();
            if (target != null)
            {
                target.GetComponentsInChildren<Renderer>(true, renderers);
            }
        }

        void ClearAllHighlightedRenderers()
        {
            foreach (var target in targetRoots)
            {
                RemoveTargetHighlightedRenderers(target.Key);
            }

            //In case any are still left, such as if they changed parents or what not when they were highlighted.
            renderersBuffer.Clear();
            renderersBuffer.AddRange(highlightedRenderers);
            RemoveHighlightedRenderers(renderersBuffer);
        }

        void RemoveTargetHighlightedRenderers(Transform target)
        {
            GetTargetRenderers(target, renderersBuffer);

            RemoveHighlightedRenderers(renderersBuffer);
        }

        void RemoveHighlightedRenderers(List<Renderer> renderers)
        {
            for (int i = 0; i < renderersBuffer.Count; i++)
            {
                Renderer render = renderersBuffer[i];
                if (render != null)
                {
                    materialsBuffer.Clear();
                    materialsBuffer.AddRange(render.sharedMaterials);

                    if (materialsBuffer.Contains(outlineMaterial))
                    {
                        materialsBuffer.Remove(outlineMaterial);
                        render.materials = materialsBuffer.ToArray();
                    }
                }

                highlightedRenderers.Remove(render);
            }

            renderersBuffer.Clear();
        }

        void AddTargetRoot(Transform targetRoot)
        {
            targetRoots.Add(targetRoot, new TargetInfo());
            targetRootsOrdered.Add(targetRoot);

            AddAllChildren(targetRoot);
        }
        void RemoveTargetRoot(Transform targetRoot)
        {
            if (targetRoots.Remove(targetRoot))
            {
                targetRootsOrdered.Remove(targetRoot);

                RemoveAllChildren(targetRoot);
            }
        }

        void AddAllChildren(Transform target)
        {
            childrenBuffer.Clear();
            target.GetComponentsInChildren<Transform>(true, childrenBuffer);
            childrenBuffer.Remove(target);

            for (int i = 0; i < childrenBuffer.Count; i++)
            {
                Transform child = childrenBuffer[i];
                children.Add(child);
                RemoveTargetRoot(child); //We do this in case we selected child first and then the parent.
            }

            childrenBuffer.Clear();
        }
        void RemoveAllChildren(Transform target)
        {
            childrenBuffer.Clear();
            target.GetComponentsInChildren<Transform>(true, childrenBuffer);
            childrenBuffer.Remove(target);

            for (int i = 0; i < childrenBuffer.Count; i++)
            {
                children.Remove(childrenBuffer[i]);
            }

            childrenBuffer.Clear();
        }

        public void SetPivotPoint()
        {
            if (mainTargetRoot != null)
            {
                if (pivot == TransformPivot.Pivot)
                {
                    pivotPoint = mainTargetRoot.position;
                }
                else if (pivot == TransformPivot.Center)
                {
                    totalCenterPivotPoint = Vector3.zero;

                    Dictionary<Transform, TargetInfo>.Enumerator targetsEnumerator = targetRoots.GetEnumerator(); //We avoid foreach to avoid garbage.
                    while (targetsEnumerator.MoveNext())
                    {
                        Transform target = targetsEnumerator.Current.Key;
                        TargetInfo info = targetsEnumerator.Current.Value;
                        info.centerPivotPoint = target.GetCenter(centerType);

                        totalCenterPivotPoint += info.centerPivotPoint;
                    }

                    totalCenterPivotPoint /= targetRoots.Count;

                    if (centerType == CenterType.Solo)
                    {
                        pivotPoint = targetRoots[mainTargetRoot].centerPivotPoint;
                    }
                    else if (centerType == CenterType.All)
                    {
                        pivotPoint = totalCenterPivotPoint;
                    }
                }
            }
        }
        void SetPivotPointOffset(Vector3 offset)
        {
            pivotPoint += offset;
            totalCenterPivotPoint += offset;
        }


        IEnumerator ForceUpdatePivotPointAtEndOfFrame()
        {
            while (this.enabled)
            {
                ForceUpdatePivotPointOnChange();
#if UNITY_WEBGL
                yield return null;
#else
                yield return waitForEndOFFrame;
#endif
            }
        }

        void ForceUpdatePivotPointOnChange()
        {
            if (forceUpdatePivotPointOnChange)
            {
                if (mainTargetRoot != null && !isTransforming)
                {
                    bool hasSet = false;
                    Dictionary<Transform, TargetInfo>.Enumerator targets = targetRoots.GetEnumerator();
                    while (targets.MoveNext())
                    {
                        if (!hasSet)
                        {
                            if (targets.Current.Value.previousPosition != Vector3.zero && targets.Current.Key.position != targets.Current.Value.previousPosition)
                            {
                                SetPivotPoint();
                                hasSet = true;
                            }
                        }

                        targets.Current.Value.previousPosition = targets.Current.Key.position;
                    }
                }
            }
        }

        public void SetTranslatingAxis(TransformType type, Axis axis, Axis planeAxis = Axis.None)
        {
            this.translatingType = type;
            this.nearAxis = axis;
            this.planeAxis = planeAxis;
        }

        public AxisInfo GetAxisInfo()
        {
            AxisInfo currentAxisInfo = axisInfo;

            if (isTransforming && GetProperTransformSpace() == TransformSpace.Global && translatingType == TransformType.Rotate)
            {
                currentAxisInfo.xDirection = totalRotationAmount * Vector3.right;
                currentAxisInfo.yDirection = totalRotationAmount * Vector3.up;
                currentAxisInfo.zDirection = totalRotationAmount * Vector3.forward;
            }

            return currentAxisInfo;
        }

        void SetNearAxis(Ray inputRay)
        {
            if (isTransforming)
                return;

            SetTranslatingAxis(transformType, Axis.None);

            if (mainTargetRoot == null)
                return;

            float distanceMultiplier = GetDistanceMultiplier();
            float handleMinSelectedDistanceCheck = (this.minSelectedDistanceCheck + handleWidth) * distanceMultiplier;

            if (nearAxis == Axis.None && (TransformTypeContains(TransformType.Move) || TransformTypeContains(TransformType.Scale)))
            {
                //Important to check scale lines before move lines since in TransformType.All the move planes would block the scales center scale all gizmo.
                if (nearAxis == Axis.None && TransformTypeContains(TransformType.Scale))
                {
                    float tipMinSelectedDistanceCheck = (this.minSelectedDistanceCheck + boxSize) * distanceMultiplier;
                    HandleNearestPlanes(TransformType.Scale, handleSquares, tipMinSelectedDistanceCheck, inputRay);
                }

                if (nearAxis == Axis.None && TransformTypeContains(TransformType.Move))
                {
                    //Important to check the planes first before the handle tip since it makes selecting the planes easier.
                    float planeMinSelectedDistanceCheck = (this.minSelectedDistanceCheck + planeSize) * distanceMultiplier;
                    HandleNearestPlanes(TransformType.Move, handlePlanes, planeMinSelectedDistanceCheck, inputRay);

                    if (nearAxis != Axis.None)
                    {
                        planeAxis = nearAxis;
                    }
                    else
                    {
                        float tipMinSelectedDistanceCheck = (this.minSelectedDistanceCheck + triangleSize) * distanceMultiplier;
                        HandleNearestLines(TransformType.Move, handleTriangles, tipMinSelectedDistanceCheck, inputRay);
                    }
                }

                if (nearAxis == Axis.None)
                {
                    //Since Move and Scale share the same handle line, we give Move the priority.
                    TransformType transType = transformType == TransformType.All ? TransformType.Move : transformType;
                    HandleNearestLines(transType, handleLines, handleMinSelectedDistanceCheck, inputRay);
                }
            }

            if (nearAxis == Axis.None && TransformTypeContains(TransformType.Rotate))
            {
                HandleNearestLines(TransformType.Rotate, circlesLines, handleMinSelectedDistanceCheck, inputRay);
            }
        }

        void HandleNearestLines(TransformType type, AxisVectors axisVectors, float minSelectedDistanceCheck, Ray inputRay)
        {
            float xClosestDistance = ClosestDistanceFromMouseToLines(axisVectors.x, inputRay);
            float yClosestDistance = ClosestDistanceFromMouseToLines(axisVectors.y, inputRay);
            float zClosestDistance = ClosestDistanceFromMouseToLines(axisVectors.z, inputRay);
            float allClosestDistance = ClosestDistanceFromMouseToLines(axisVectors.all, inputRay);

            HandleNearest(type, xClosestDistance, yClosestDistance, zClosestDistance, allClosestDistance, minSelectedDistanceCheck, inputRay);
        }

        void HandleNearestPlanes(TransformType type, AxisVectors axisVectors, float minSelectedDistanceCheck, Ray inputRay)
        {
            float xClosestDistance = ClosestDistanceFromMouseToPlanes(axisVectors.x, inputRay);
            float yClosestDistance = ClosestDistanceFromMouseToPlanes(axisVectors.y, inputRay);
            float zClosestDistance = ClosestDistanceFromMouseToPlanes(axisVectors.z, inputRay);
            float allClosestDistance = ClosestDistanceFromMouseToPlanes(axisVectors.all, inputRay);

            HandleNearest(type, xClosestDistance, yClosestDistance, zClosestDistance, allClosestDistance, minSelectedDistanceCheck, inputRay);
        }

        void HandleNearest(TransformType type, float xClosestDistance, float yClosestDistance, float zClosestDistance, float allClosestDistance, float minSelectedDistanceCheck, Ray inputRay)
        {
            if (type == TransformType.Scale && allClosestDistance <= minSelectedDistanceCheck)
                SetTranslatingAxis(type, Axis.Any);
            else if (xClosestDistance <= minSelectedDistanceCheck && xClosestDistance <= yClosestDistance && xClosestDistance <= zClosestDistance)
                SetTranslatingAxis(type, Axis.X);
            else if (yClosestDistance <= minSelectedDistanceCheck && yClosestDistance <= xClosestDistance && yClosestDistance <= zClosestDistance)
                SetTranslatingAxis(type, Axis.Y);
            else if (zClosestDistance <= minSelectedDistanceCheck && zClosestDistance <= xClosestDistance && zClosestDistance <= yClosestDistance)
                SetTranslatingAxis(type, Axis.Z);
            else if (type == TransformType.Rotate && mainTargetRoot != null)
            {
                Vector3 mousePlaneHit = Geometry.LinePlaneIntersect(inputRay.origin, inputRay.direction, pivotPoint, (transform.position - pivotPoint).normalized);
                if ((pivotPoint - mousePlaneHit).sqrMagnitude <= (GetHandleLength(TransformType.Rotate)).Squared())
                    SetTranslatingAxis(type, Axis.Y);
            }
        }

        float ClosestDistanceFromMouseToLines(List<Vector3> lines, Ray inputRay)
        {
            float closestDistance = float.MaxValue;
            for (int i = 0; i + 1 < lines.Count; i++)
            {
                IntersectPoints points = Geometry.ClosestPointsOnSegmentToLine(lines[i], lines[i + 1], inputRay.origin, inputRay.direction);
                float distance = Vector3.Distance(points.first, points.second);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }
            return closestDistance;
        }

        float ClosestDistanceFromMouseToPlanes(List<Vector3> planePoints, Ray inputRay)
        {
            float closestDistance = float.MaxValue;

            if (planePoints.Count >= 4)
            {
                for (int i = 0; i < planePoints.Count; i += 4)
                {
                    Plane plane = new Plane(planePoints[i], planePoints[i + 1], planePoints[i + 2]);

                    float distanceToPlane;
                    if (plane.Raycast(inputRay, out distanceToPlane))
                    {
                        Vector3 pointOnPlane = inputRay.origin + (inputRay.direction * distanceToPlane);
                        Vector3 planeCenter = (planePoints[0] + planePoints[1] + planePoints[2] + planePoints[3]) / 4f;

                        float distance = Vector3.Distance(planeCenter, pointOnPlane);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                        }
                    }
                }
            }

            return closestDistance;
        }

        //float DistanceFromMouseToPlane(List<Vector3> planeLines)
        //{
        //	if(planeLines.Count >= 4)
        //	{
        //		Ray mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);
        //		Plane plane = new Plane(planeLines[0], planeLines[1], planeLines[2]);

        //		float distanceToPlane;
        //		if(plane.Raycast(mouseRay, out distanceToPlane))
        //		{
        //			Vector3 pointOnPlane = mouseRay.origin + (mouseRay.direction * distanceToPlane);
        //			Vector3 planeCenter = (planeLines[0] + planeLines[1] + planeLines[2] + planeLines[3]) / 4f;

        //			return Vector3.Distance(planeCenter, pointOnPlane);
        //		}
        //	}

        //	return float.MaxValue;
        //}

        void SetAxisInfo()
        {
            if (mainTargetRoot != null)
            {
                axisInfo.Set(mainTargetRoot, pivotPoint, GetProperTransformSpace());
            }
        }

        //This helps keep the size consistent no matter how far we are from it.
        public float GetDistanceMultiplier()
        {
            if (mainTargetRoot == null)
                return 0f;

            if (myCamera.orthographic)
                return Mathf.Max(.01f, myCamera.orthographicSize * 2f);
            return Mathf.Max(.01f, Mathf.Abs(ExtVector3.MagnitudeInDirection(pivotPoint - transform.position, myCamera.transform.forward)));
        }

        void SetLines()
        {
            SetHandleLines();
            SetHandlePlanes();
            SetHandleTriangles();
            SetHandleSquares();
            SetCircles(GetAxisInfo(), circlesLines);
        }

        void SetHandleLines()
        {
            handleLines.Clear();

            if (TranslatingTypeContains(TransformType.Move) || TranslatingTypeContains(TransformType.Scale))
            {
                float lineWidth = handleWidth * GetDistanceMultiplier();

                float xLineLength = 0;
                float yLineLength = 0;
                float zLineLength = 0;
                if (TranslatingTypeContains(TransformType.Move))
                {
                    xLineLength = yLineLength = zLineLength = GetHandleLength(TransformType.Move);
                }
                else if (TranslatingTypeContains(TransformType.Scale))
                {
                    xLineLength = GetHandleLength(TransformType.Scale, Axis.X);
                    yLineLength = GetHandleLength(TransformType.Scale, Axis.Y);
                    zLineLength = GetHandleLength(TransformType.Scale, Axis.Z);
                }

                AddQuads(pivotPoint, axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, xLineLength, lineWidth, handleLines.x);
                AddQuads(pivotPoint, axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, yLineLength, lineWidth, handleLines.y);
                AddQuads(pivotPoint, axisInfo.zDirection, axisInfo.xDirection, axisInfo.yDirection, zLineLength, lineWidth, handleLines.z);
            }
        }
        int AxisDirectionMultiplier(Vector3 direction, Vector3 otherDirection)
        {
            return ExtVector3.IsInDirection(direction, otherDirection) ? 1 : -1;
        }

        void SetHandlePlanes()
        {
            handlePlanes.Clear();

            if (TranslatingTypeContains(TransformType.Move))
            {
                Vector3 pivotToCamera = myCamera.transform.position - pivotPoint;
                float cameraXSign = Mathf.Sign(Vector3.Dot(axisInfo.xDirection, pivotToCamera));
                float cameraYSign = Mathf.Sign(Vector3.Dot(axisInfo.yDirection, pivotToCamera));
                float cameraZSign = Mathf.Sign(Vector3.Dot(axisInfo.zDirection, pivotToCamera));

                float planeSize = this.planeSize;
                if (transformType == TransformType.All)
                { planeSize *= allMoveHandleLengthMultiplier; }
                planeSize *= GetDistanceMultiplier();

                Vector3 xDirection = (axisInfo.xDirection * planeSize) * cameraXSign;
                Vector3 yDirection = (axisInfo.yDirection * planeSize) * cameraYSign;
                Vector3 zDirection = (axisInfo.zDirection * planeSize) * cameraZSign;

                Vector3 xPlaneCenter = pivotPoint + (yDirection + zDirection);
                Vector3 yPlaneCenter = pivotPoint + (xDirection + zDirection);
                Vector3 zPlaneCenter = pivotPoint + (xDirection + yDirection);

                AddQuad(xPlaneCenter, axisInfo.yDirection, axisInfo.zDirection, planeSize, handlePlanes.x);
                AddQuad(yPlaneCenter, axisInfo.xDirection, axisInfo.zDirection, planeSize, handlePlanes.y);
                AddQuad(zPlaneCenter, axisInfo.xDirection, axisInfo.yDirection, planeSize, handlePlanes.z);
            }
        }

        void SetHandleTriangles()
        {
            handleTriangles.Clear();

            if (TranslatingTypeContains(TransformType.Move))
            {
                float triangleLength = triangleSize * GetDistanceMultiplier();
                AddTriangles(axisInfo.GetXAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, triangleLength, handleTriangles.x);
                AddTriangles(axisInfo.GetYAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, triangleLength, handleTriangles.y);
                AddTriangles(axisInfo.GetZAxisEnd(GetHandleLength(TransformType.Move)), axisInfo.zDirection, axisInfo.yDirection, axisInfo.xDirection, triangleLength, handleTriangles.z);
            }
        }

        void AddTriangles(Vector3 axisEnd, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
        {
            Vector3 endPoint = axisEnd + (axisDirection * (size * 2f));
            Square baseSquare = GetBaseSquare(axisEnd, axisOtherDirection1, axisOtherDirection2, size / 2f);

            resultsBuffer.Add(baseSquare.bottomLeft);
            resultsBuffer.Add(baseSquare.topLeft);
            resultsBuffer.Add(baseSquare.topRight);
            resultsBuffer.Add(baseSquare.topLeft);
            resultsBuffer.Add(baseSquare.bottomRight);
            resultsBuffer.Add(baseSquare.topRight);

            for (int i = 0; i < 4; i++)
            {
                resultsBuffer.Add(baseSquare[i]);
                resultsBuffer.Add(baseSquare[i + 1]);
                resultsBuffer.Add(endPoint);
            }
        }

        void SetHandleSquares()
        {
            handleSquares.Clear();

            if (TranslatingTypeContains(TransformType.Scale))
            {
                float boxSize = this.boxSize * GetDistanceMultiplier();
                AddSquares(axisInfo.GetXAxisEnd(GetHandleLength(TransformType.Scale, Axis.X)), axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, boxSize, handleSquares.x);
                AddSquares(axisInfo.GetYAxisEnd(GetHandleLength(TransformType.Scale, Axis.Y)), axisInfo.yDirection, axisInfo.xDirection, axisInfo.zDirection, boxSize, handleSquares.y);
                AddSquares(axisInfo.GetZAxisEnd(GetHandleLength(TransformType.Scale, Axis.Z)), axisInfo.zDirection, axisInfo.xDirection, axisInfo.yDirection, boxSize, handleSquares.z);
                AddSquares(pivotPoint - (axisInfo.xDirection * (boxSize * .5f)), axisInfo.xDirection, axisInfo.yDirection, axisInfo.zDirection, boxSize, handleSquares.all);
            }
        }

        void AddSquares(Vector3 axisStart, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size, List<Vector3> resultsBuffer)
        {
            AddQuads(axisStart, axisDirection, axisOtherDirection1, axisOtherDirection2, size, size * .5f, resultsBuffer);
        }
        void AddQuads(Vector3 axisStart, Vector3 axisDirection, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float length, float width, List<Vector3> resultsBuffer)
        {
            Vector3 axisEnd = axisStart + (axisDirection * length);
            AddQuads(axisStart, axisEnd, axisOtherDirection1, axisOtherDirection2, width, resultsBuffer);
        }

        void AddQuads(Vector3 axisStart, Vector3 axisEnd, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float width, List<Vector3> resultsBuffer, List<bool> resultsDepth = null, bool depthTest = true)
        {
            Square baseRectangle = GetBaseSquare(axisStart, axisOtherDirection1, axisOtherDirection2, width);
            Square baseRectangleEnd = GetBaseSquare(axisEnd, axisOtherDirection1, axisOtherDirection2, width);

            resultsBuffer.Add(baseRectangle.bottomLeft);
            resultsBuffer.Add(baseRectangle.topLeft);
            resultsBuffer.Add(baseRectangle.topRight);
            resultsBuffer.Add(baseRectangle.bottomRight);

            resultsBuffer.Add(baseRectangleEnd.bottomLeft);
            resultsBuffer.Add(baseRectangleEnd.topLeft);
            resultsBuffer.Add(baseRectangleEnd.topRight);
            resultsBuffer.Add(baseRectangleEnd.bottomRight);

            if (resultsDepth != null)
            {
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
                resultsDepth.Add(depthTest);
            }

            for (int i = 0; i < 4; i++)
            {
                resultsBuffer.Add(baseRectangle[i]);
                resultsBuffer.Add(baseRectangleEnd[i]);
                resultsBuffer.Add(baseRectangleEnd[i + 1]);
                resultsBuffer.Add(baseRectangle[i + 1]);

                if (resultsDepth != null)
                {
                    resultsDepth.Add(depthTest);
                    resultsDepth.Add(depthTest);
                    resultsDepth.Add(depthTest);
                    resultsDepth.Add(depthTest);
                }
            }
        }

        void AddQuad(Vector3 axisStart, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float width, List<Vector3> resultsBuffer)
        {
            Square baseRectangle = GetBaseSquare(axisStart, axisOtherDirection1, axisOtherDirection2, width);

            resultsBuffer.Add(baseRectangle.bottomLeft);
            resultsBuffer.Add(baseRectangle.topLeft);
            resultsBuffer.Add(baseRectangle.topRight);
            resultsBuffer.Add(baseRectangle.bottomRight);
        }

        Square GetBaseSquare(Vector3 axisEnd, Vector3 axisOtherDirection1, Vector3 axisOtherDirection2, float size)
        {
            Square square;
            Vector3 offsetUp = ((axisOtherDirection1 * size) + (axisOtherDirection2 * size));
            Vector3 offsetDown = ((axisOtherDirection1 * size) - (axisOtherDirection2 * size));
            //These might not really be the proper directions, as in the bottomLeft might not really be at the bottom left...
            square.bottomLeft = axisEnd + offsetDown;
            square.topLeft = axisEnd + offsetUp;
            square.bottomRight = axisEnd - offsetUp;
            square.topRight = axisEnd - offsetDown;
            return square;
        }

        void SetCircles(AxisInfo axisInfo, AxisVectors axisVectors)
        {
            axisVectors.Clear();

            if (TranslatingTypeContains(TransformType.Rotate))
            {
                float circleLength = GetHandleLength(TransformType.Rotate);
                switch (showAxis)
                {
                    case ShowAxis.All:
                        AddCircle(pivotPoint, axisInfo.xDirection, circleLength, axisVectors.x);
                        AddCircle(pivotPoint, axisInfo.yDirection, circleLength, axisVectors.y);
                        AddCircle(pivotPoint, axisInfo.zDirection, circleLength, axisVectors.z);
                        AddCircle(pivotPoint, (pivotPoint - transform.position).normalized, circleLength, axisVectors.all, null, false);
                        break;

                    case ShowAxis.X:
                        AddCircle(pivotPoint, axisInfo.xDirection, circleLength, axisVectors.x);
                        break;

                    case ShowAxis.Y:
                        AddCircle(pivotPoint, axisInfo.yDirection, circleLength, axisVectors.y, axisVectors.depthTest);
                        break;

                    case ShowAxis.Z:
                        AddCircle(pivotPoint, axisInfo.zDirection, circleLength, axisVectors.z);
                        break;
                }
            }
        }

        void AddCircle(Vector3 origin, Vector3 axisDirection, float size, List<Vector3> resultsBuffer, List<bool> resultDepth = null, bool depthTest = true)
        {
            Vector3 up = axisDirection.normalized * size;
            Vector3 forward = Vector3.Slerp(up, -up, .5f);
            Vector3 right = Vector3.Cross(up, forward).normalized * size;

            Matrix4x4 matrix = new Matrix4x4();

            matrix[0] = right.x;
            matrix[1] = right.y;
            matrix[2] = right.z;

            matrix[4] = up.x;
            matrix[5] = up.y;
            matrix[6] = up.z;

            matrix[8] = forward.x;
            matrix[9] = forward.y;
            matrix[10] = forward.z;

            Vector3 lastPoint = origin + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
            Vector3 nextPoint = Vector3.zero;
            float multiplier = 360f / circleDetail;

            Plane plane = new Plane((transform.position - pivotPoint).normalized, pivotPoint);

            float circleHandleWidth = handleWidth * GetDistanceMultiplier();

            for (int i = 0; i < circleDetail + 1; i++)
            {
                nextPoint.x = Mathf.Cos((i * multiplier) * Mathf.Deg2Rad);
                nextPoint.z = Mathf.Sin((i * multiplier) * Mathf.Deg2Rad);
                nextPoint.y = 0;

                nextPoint = origin + matrix.MultiplyPoint3x4(nextPoint);

                if (showAxis == ShowAxis.All)
                {
                    if (!depthTest || plane.GetSide(lastPoint))
                    {
                        Vector3 centerPoint = (lastPoint + nextPoint) * .5f;
                        Vector3 upDirection = (centerPoint - origin).normalized;
                        AddQuads(lastPoint, nextPoint, upDirection, axisDirection, circleHandleWidth, resultsBuffer);
                    }
                }
                else
                {
                    Vector3 centerPoint = (lastPoint + nextPoint) * .5f;
                    Vector3 upDirection = (centerPoint - origin).normalized;

                    AddQuads(lastPoint, nextPoint, upDirection, axisDirection, circleHandleWidth, resultsBuffer, resultDepth, !depthTest || plane.GetSide(lastPoint));
                }

                lastPoint = nextPoint;
            }
        }

        void DrawLines(List<Vector3> lines, Color color)
        {
            if (lines.Count == 0)
                return;

            GL.Begin(GL.LINES);
            GL.Color(color);

            for (int i = 0; i < lines.Count; i += 2)
            {
                GL.Vertex(lines[i]);
                GL.Vertex(lines[i + 1]);
            }

            GL.End();
        }

        void DrawTriangles(string name, List<Vector3> lines, Color color)
        {
            triangleRenderers.TryGetValue(name, out MeshRenderer mr);

            if (lines.Count == 0 || lines[0].magnitude == 0)
            {
                if (mr != null)
                {
                    mr.enabled = false;
                }
                return;
            }

            if (mr == null)
            {
                GameObject go = new GameObject(name);
                
                int layer = selectionMask.value > 0 && Mathf.IsPowerOfTwo(selectionMask.value)
                    ? (int)Mathf.Log(selectionMask.value, 2)
                    : LayerMask.NameToLayer("Default");
                
                go.layer = layer >= 0 && layer < 32 ? layer : LayerMask.NameToLayer("Default");
                mr = go.AddComponent<MeshRenderer>();
                go.AddComponent<MeshFilter>();
                mr.material = new Material(Shader.Find("TransformGizmo"));
                triangleRenderers[name] = mr;
            }

            mr.enabled = true;

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Mesh mesh = new Mesh();

            int triangleCount = lines.Count / 3;
            Vector3[] vertices = new Vector3[triangleCount * 3];
            int[] triangles = new int[triangleCount * 3];
            Color[] colors = new Color[triangleCount * 3];

            for (int i = 0; i < triangleCount; i++)
            {
                int baseIndex = i * 3;

                // Vertices
                vertices[baseIndex] = lines[baseIndex];
                vertices[baseIndex + 1] = lines[baseIndex + 1];
                vertices[baseIndex + 2] = lines[baseIndex + 2];

                // Triangles
                triangles[baseIndex] = baseIndex;
                triangles[baseIndex + 1] = baseIndex + 1;
                triangles[baseIndex + 2] = baseIndex + 2;

                // Colors
                colors[baseIndex] = color;
                colors[baseIndex + 1] = color;
                colors[baseIndex + 2] = color;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;

            mf.mesh = mesh;
        }

        void DrawQuads(string name, List<Vector3> lines, Color color, List<bool> depthTest = null, Color? depthColor = null)
        {
            quadRenderers.TryGetValue(name, out MeshRenderer mr);

            if (lines.Count == 0 || lines[0].magnitude == 0)
            {
                if (mr != null)
                {
                    mr.enabled = false;
                }
                return;
            }

            if (mr == null)
            {
                GameObject go = new GameObject(name);
                int layer = selectionMask.value > 0 && Mathf.IsPowerOfTwo(selectionMask.value)
                    ? (int)Mathf.Log(selectionMask.value, 2)
                    : LayerMask.NameToLayer("Default");
                
                go.layer = layer >= 0 && layer < 32 ? layer : LayerMask.NameToLayer("Default");
                mr = go.AddComponent<MeshRenderer>();
                go.AddComponent<MeshFilter>();
                mr.material = new Material(Shader.Find("TransformGizmo"));
                quadRenderers[name] = mr;
            }

            mr.enabled = true;

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Mesh mesh = new Mesh();

            int quadCount = lines.Count / 4;
            Vector3[] vertices = new Vector3[quadCount * 4];
            int[] triangles = new int[quadCount * 6];
            Color[] colors = new Color[quadCount * 4];

            for (int i = 0; i < quadCount; i++)
            {
                int baseIndex = i * 4;
                int baseTriIndex = i * 6;

                // Vertices
                vertices[baseIndex] = lines[baseIndex];
                vertices[baseIndex + 1] = lines[baseIndex + 1];
                vertices[baseIndex + 2] = lines[baseIndex + 2];
                vertices[baseIndex + 3] = lines[baseIndex + 3];

                // Triangles
                triangles[baseTriIndex] = baseIndex;
                triangles[baseTriIndex + 1] = baseIndex + 1;
                triangles[baseTriIndex + 2] = baseIndex + 2;
                triangles[baseTriIndex + 3] = baseIndex;
                triangles[baseTriIndex + 4] = baseIndex + 2;
                triangles[baseTriIndex + 5] = baseIndex + 3;

                // Colors
                Color quadColor = (depthTest != null && depthColor.HasValue) ?
                    (depthTest[baseIndex] ? color : depthColor.Value) : color;
                colors[baseIndex] = quadColor;
                colors[baseIndex + 1] = quadColor;
                colors[baseIndex + 2] = quadColor;
                colors[baseIndex + 3] = quadColor;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;

            mf.mesh = mesh;
        }

        void DrawFilledCircle(List<Vector3> lines, Color color)
        {
            if (lines.Count == 0)
                return;

            Vector3 center = Vector3.zero;
            for (int i = 0; i < lines.Count; i++)
            {
                center += lines[i];
            }
            center /= lines.Count;

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            for (int i = 0; i + 1 < lines.Count; i++)
            {
                GL.Vertex(lines[i]);
                GL.Vertex(lines[i + 1]);
                GL.Vertex(center);
            }

            GL.End();
        }

        void SetMaterial()
        {
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Custom/Lines"));
                outlineMaterial = new Material(Shader.Find("Custom/Outline"));
            }
        }
    }
}
