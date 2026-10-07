using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class RaycastObject : MonoBehaviour
{
    public static RaycastObject instance;

    public float clickRadius;
    private Canvas canvas;
    public TextMeshProUGUI highlightText;
    public Vector2 mouseTextOffset;

    [HideInInspector]
    public TangibleBodyPart bodyPartScript;
    [HideInInspector]
    [System.NonSerialized]
    public RaycastHit hit;
    [HideInInspector]
    public bool raycastBlocked;

    private Camera cam;
    private RectTransform highlightTextRt;
    private TangibleBodyPart prevbodyPartScript;
    private RaycastHit[] hits;
    private Vector2 firstMousePos = new Vector2();
    private GameObject objectSelected;

    private bool touchStarted;
    private Vector2 firstTouchPos;
    private int touchId = -1;
    private bool touchStartedOverUI;
    private bool touchSelectionOwned;
    private bool touchCameraOwned;
    private Vector2 lastTouchPosition;
    private bool touchPanOwned;
    private int touchPanTouchId1 = -1;
    private int touchPanTouchId2 = -1;
    private bool touchPanTouch1StartedOverUI;
    private bool touchPanTouch2StartedOverUI;
    private Vector2 lastTouchCentroid;
    private bool ignoreTouchInteractionUntilAllReleased;
    private BoxSelection touchBoxSelection;
    private BrushSelection touchBrushSelection;
    private LassoSelection touchLassoSelection;

    private int layer_mask1;
    private int layer_mask2;
    private int layer_mask3;
    private int layer_mask4;
    private int layer_mask5;
    private LayerMask finalmask;

    private void Awake()
    {
        instance = this;
        cam = Camera.main;
        highlightTextRt = highlightText.GetComponent<RectTransform>();
        canvas = highlightText.GetComponentInParent<Canvas>();
    }

    void Start()
    {
        layer_mask1 = LayerMask.GetMask("Body");
        layer_mask2 = LayerMask.GetMask("Outline");
        layer_mask3 = LayerMask.GetMask("HighlightedOutline");
        layer_mask4 = LayerMask.GetMask("Cube");
        layer_mask5 = LayerMask.GetMask("Gizmo");
        finalmask = layer_mask1 | layer_mask2 | layer_mask3 | layer_mask4 | layer_mask5;
    }

    void Update()
    {
        if (raycastBlocked || EventSystem.current.IsPointerOverGameObject())
        {
            highlightText.text = "";
        }
        else
        {
            if (ActionControl.crossSectionsEnabled)
                HighlightWithPlane();
            else if (!ActionControl.creatingLocalNote)
                Highlight();

            Select();
            ShowContextualMenu();
        }

        HandleTouchTap();
    }

    private void LateUpdate()
    {
        highlightTextRt.position = Mouse.current.position.ReadValue() + mouseTextOffset * canvas.scaleFactor;
    }

    private TangibleBodyPart GetFirstAfterPlane(RaycastHit[] hits, RaycastHit firsthit)
    {
        if (hits == null || hits.Length == 0)
            return null;

        TangibleBodyPart bodyPartScript = firsthit.collider.GetComponent<TangibleBodyPart>();

        try
        {
            RaycastHit firstAfterPlane = firsthit;

            if (CrossSections.Instance.xPlane && firsthit.point.x > CrossSections.Instance.sagitalPlane.transform.position.x)
            {
                var collisions = hits.Where(it => it.point.x < CrossSections.Instance.sagitalPlane.transform.position.x).OrderByDescending(it => it.point.x);
                var last = hits.Where(it => it.point.x >= CrossSections.Instance.sagitalPlane.transform.position.x && !it.transform.CompareTag("Untagged") && !CrossSections.Instance.IsEnabledByTag(it.transform.tag)).OrderByDescending(it => it.point.x);
                if (last.Count() > 0)
                    firstAfterPlane = last.First();
                else
                    firstAfterPlane = collisions.First();
            }
            else if (CrossSections.Instance.ixPlane && firsthit.point.x < CrossSections.Instance.sagitalPlane.transform.position.x)
            {
                var collisions = hits.Where(it => it.point.x > CrossSections.Instance.sagitalPlane.transform.position.x).OrderBy(it => it.point.x);
                var last = hits.Where(it => it.point.x <= CrossSections.Instance.sagitalPlane.transform.position.x && !it.transform.CompareTag("Untagged") && !CrossSections.Instance.IsEnabledByTag(it.transform.tag)).OrderBy(it => it.point.x);
                if (last.Count() > 0)
                    firstAfterPlane = last.First();
                else
                    firstAfterPlane = collisions.First();
            }
            else if (CrossSections.Instance.zPlane && firsthit.point.y > CrossSections.Instance.transversalPlane.transform.position.y)
            {
                var collisions = hits.Where(it => it.point.y < CrossSections.Instance.transversalPlane.transform.position.y).OrderByDescending(it => it.point.y);
                var last = hits.Where(it => it.point.y >= CrossSections.Instance.transversalPlane.transform.position.y && !it.transform.CompareTag("Untagged") && !CrossSections.Instance.IsEnabledByTag(it.transform.tag)).OrderByDescending(it => it.point.y);
                if (last.Count() > 0)
                    firstAfterPlane = last.First();
                else
                    firstAfterPlane = collisions.First();
            }
            else if (CrossSections.Instance.izPlane && firsthit.point.y < CrossSections.Instance.transversalPlane.transform.position.y)
            {
                var collisions = hits.Where(it => it.point.y > CrossSections.Instance.transversalPlane.transform.position.y).OrderBy(it => it.point.y);
                var last = hits.Where(it => it.point.y <= CrossSections.Instance.transversalPlane.transform.position.y && !it.transform.CompareTag("Untagged") && !CrossSections.Instance.IsEnabledByTag(it.transform.tag)).OrderBy(it => it.point.y);

                if (last.Count() > 0)
                    firstAfterPlane = last.First();
                else
                    firstAfterPlane = collisions.First();
            }
            else if (CrossSections.Instance.yPlane && firsthit.point.z < CrossSections.Instance.frontalPlane.transform.position.z)
            {
                var collisions = hits.Where(it => it.point.z > CrossSections.Instance.frontalPlane.transform.position.z).OrderBy(it => it.point.z);
                var last = hits.Where(it => it.point.z <= CrossSections.Instance.frontalPlane.transform.position.z && !it.transform.CompareTag("Untagged") && !CrossSections.Instance.IsEnabledByTag(it.transform.tag)).OrderBy(it => it.point.z);

                if (last.Count() > 0)
                    firstAfterPlane = last.First();
                else
                    firstAfterPlane = collisions.First();
            }
            else if (CrossSections.Instance.iyPlane && firsthit.point.z > CrossSections.Instance.frontalPlane.transform.position.z)
            {
                var collisions = hits.Where(it => it.point.z < CrossSections.Instance.frontalPlane.transform.position.z).OrderByDescending(it => it.point.z);
                var last = hits.Where(it => it.point.z >= CrossSections.Instance.frontalPlane.transform.position.z && !it.transform.CompareTag("Untagged") && !CrossSections.Instance.IsEnabledByTag(it.transform.tag)).OrderByDescending(it => it.point.z);

                if (last.Count() > 0)
                    firstAfterPlane = last.First();
                else
                    firstAfterPlane = collisions.First();
            }

            bodyPartScript = firstAfterPlane.collider.GetComponent<TangibleBodyPart>();
            return bodyPartScript;
        }
        catch
        {
            return null;
        }
    }

    private void ClickedNull()
    {
        if (ActionControl.creatingLocalNote)
            return;

        if (!ActionControl.boxSelection && !ActionControl.brushSelection && !ActionControl.multipleSelection)
            ActionControl.Instance.CollapseAll();
        if (SelectedObjectsManagement.Instance.selectedObjects.Count > 0)
        {
            SelectedObjectsManagement.Instance.DeselectAllObjects(false);
            ActionControl.Instance.AddCommand(new SelectCommand(new List<GameObject>()), false);
        }
        CameraController.instance.SetTarget(null);
        NamesManagement.Instance.SetDesc(NamesManagement.NO_SELECTION);
        SelectedObjectsManagement.Instance.lastParentSelected = null;
        ActionControl.Instance.UpdateButtons();
        TranslateObject.Instance.SetGizmoCenter();
    }

    private void Select()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            firstMousePos = Mouse.current.position.ReadValue();
        }
        else if ((Mouse.current.leftButton.wasReleasedThisFrame)
            && Vector3.Distance(firstMousePos, Mouse.current.position.ReadValue()) < 10f)
        {
            if (objectSelected == null)
                ClickedNull();
            else if (LayerMask.LayerToName(objectSelected.layer).Equals("Cube"))
            {
                GizmoFace faceClicked = GizmoBehaviour.instance.GetHitFace(hit);
                GizmoBehaviour.instance.SetCameraRotation(faceClicked);
                if (ActionControl.crossSectionsEnabled)
                    CrossPlanesGizmo.Instance.SetPlane(faceClicked);
            }
            else if (!ActionControl.creatingLocalNote)
            {
                Label labelScript = objectSelected.GetComponent<Label>();

                if (bodyPartScript != null)
                    bodyPartScript.ObjectClicked();

                if (labelScript != null)
                    labelScript.Click();
            }
            objectSelected = null;
        }
    }

    private void HandleTouchTap()
    {
        if (Touchscreen.current == null)
        {
            if (touchPanOwned || ignoreTouchInteractionUntilAllReleased)
            {
                ClearTouchGestureState();
                ignoreTouchInteractionUntilAllReleased = false;
            }
            return;
        }

        Touchscreen touchscreen = Touchscreen.current;
        int activeTouchCount = GetActiveTouchCount(touchscreen);

        if (ignoreTouchInteractionUntilAllReleased)
        {
            if (activeTouchCount == 0)
            {
                ClearTouchGestureState();
                ignoreTouchInteractionUntilAllReleased = false;
            }
            return;
        }

        TouchControl touch = touchscreen.primaryTouch;

        if (touch.press.wasPressedThisFrame)
        {
            touchStarted = true;
            firstTouchPos = touch.position.ReadValue();
            touchId = touch.touchId.ReadValue();
            touchStartedOverUI = EventSystem.current != null &&
                                 EventSystem.current.IsPointerOverGameObject(touchId);
            touchSelectionOwned = false;
            touchCameraOwned = false;
            touchPanOwned = false;
            touchPanTouchId1 = -1;
            touchPanTouchId2 = -1;
            touchPanTouch1StartedOverUI = false;
            touchPanTouch2StartedOverUI = false;
            lastTouchCentroid = Vector2.zero;
            lastTouchPosition = firstTouchPos;
            touchBoxSelection = null;
            touchBrushSelection = null;
            touchLassoSelection = null;
        }

        if (touchPanOwned)
        {
            UpdateTouchPan(touchscreen, activeTouchCount);
            return;
        }

        if (activeTouchCount >= 2)
        {
            if (touchSelectionOwned)
            {
                // A claimed selection gesture keeps ownership; extra touches are ignored.
            }
            else if (activeTouchCount == 2 && TryBeginTouchPan(touchscreen))
            {
                return;
            }
            else
            {
                IgnoreTouchInteractionUntilAllReleased(activeTouchCount);
                return;
            }
        }

        if (!touchStarted)
            return;

        Vector2 touchPosition = touch.position.ReadValue();
        bool selectionToolAvailable = false;

        if (!touchSelectionOwned && !touchCameraOwned &&
            (touch.press.isPressed || touch.press.wasReleasedThisFrame) &&
            TryStartTouchSelection(touchPosition, out selectionToolAvailable))
        {
            touchSelectionOwned = true;
            ProcessTouchSelection(true, false, false, firstTouchPos);
            ProcessTouchSelection(false, true, false, touchPosition);
        }
        else if (touchSelectionOwned && touch.press.isPressed)
        {
            ProcessTouchSelection(false, true, false, touchPosition);
        }

        if (!touchSelectionOwned && !touchCameraOwned && !selectionToolAvailable && !touchStartedOverUI &&
            (touch.press.isPressed || touch.press.wasReleasedThisFrame) &&
            Vector2.Distance(firstTouchPos, touchPosition) >= 10f)
        {
            if (CameraController.instance != null &&
                CameraController.instance.TryRotateByTouchDelta(touchPosition - lastTouchPosition))
                touchCameraOwned = true;
        }
        else if (touchCameraOwned && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
        {
            if (CameraController.instance != null)
                CameraController.instance.TryRotateByTouchDelta(touchPosition - lastTouchPosition);
        }

        lastTouchPosition = touchPosition;

        if (!touch.press.wasReleasedThisFrame)
            return;

        touchStarted = false;

        if (touchSelectionOwned)
        {
            ProcessTouchSelection(false, false, true, touchPosition);
            ClearTouchSelectionOwnership();
            return;
        }

        if (touchCameraOwned)
        {
            ClearTouchSelectionOwnership();
            return;
        }

        if (Vector2.Distance(firstTouchPos, touchPosition) >= 10f)
        {
            ClearTouchSelectionOwnership();
            return;
        }

        bool pointerOverUI = EventSystem.current != null &&
                             EventSystem.current.IsPointerOverGameObject(touchId);

        if (pointerOverUI)
        {
            ClearTouchSelectionOwnership();
            return;
        }

        SelectAtPosition(touchPosition);

        ClearTouchSelectionOwnership();
    }

    private int GetActiveTouchCount(Touchscreen touchscreen)
    {
        int activeTouchCount = 0;
        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            if (touchscreen.touches[i].isInProgress)
                activeTouchCount++;
        }

        return activeTouchCount;
    }

    private bool TryBeginTouchPan(Touchscreen touchscreen)
    {
        if (!touchStarted || touchSelectionOwned ||
            !TryGetTwoActiveTouches(touchscreen, out TouchControl firstTouch, out TouchControl secondTouch))
            return false;

        int primaryTouchId = touchId;
        int firstTouchId = firstTouch.touchId.ReadValue();
        int secondTouchId = secondTouch.touchId.ReadValue();

        TouchControl trackedTouch;
        TouchControl newTouch;
        if (firstTouchId == primaryTouchId)
        {
            trackedTouch = firstTouch;
            newTouch = secondTouch;
        }
        else if (secondTouchId == primaryTouchId)
        {
            trackedTouch = secondTouch;
            newTouch = firstTouch;
        }
        else
        {
            return false;
        }

        if (newTouch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.Began && !newTouch.press.wasPressedThisFrame)
            return false;

        int newTouchId = newTouch.touchId.ReadValue();
        bool newTouchStartedOverUI = EventSystem.current != null &&
                                     EventSystem.current.IsPointerOverGameObject(newTouchId);

        touchPanTouchId1 = trackedTouch.touchId.ReadValue();
        touchPanTouchId2 = newTouchId;
        touchPanTouch1StartedOverUI = touchStartedOverUI;
        touchPanTouch2StartedOverUI = newTouchStartedOverUI;

        if (touchPanTouch1StartedOverUI || touchPanTouch2StartedOverUI)
        {
            ClearTouchPanState();
            return false;
        }

        Vector2 firstPosition = trackedTouch.position.ReadValue();
        Vector2 secondPosition = newTouch.position.ReadValue();
        lastTouchCentroid = (firstPosition + secondPosition) * 0.5f;

        touchPanOwned = true;
        touchCameraOwned = false;
        touchStarted = false;
        touchSelectionOwned = false;
        touchId = -1;
        ClearTouchSelectionTool();
        return true;
    }

    private bool TryGetTwoActiveTouches(Touchscreen touchscreen, out TouchControl firstTouch, out TouchControl secondTouch)
    {
        firstTouch = null;
        secondTouch = null;
        int activeTouchCount = 0;

        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            TouchControl touch = touchscreen.touches[i];
            if (!touch.isInProgress)
                continue;

            activeTouchCount++;
            if (activeTouchCount == 1)
                firstTouch = touch;
            else if (activeTouchCount == 2)
                secondTouch = touch;
        }

        return activeTouchCount == 2;
    }

    private void UpdateTouchPan(Touchscreen touchscreen, int activeTouchCount)
    {
        if (activeTouchCount != 2 ||
            !TryGetActiveTouchById(touchscreen, touchPanTouchId1, out TouchControl firstTouch) ||
            !TryGetActiveTouchById(touchscreen, touchPanTouchId2, out TouchControl secondTouch))
        {
            IgnoreTouchInteractionUntilAllReleased(activeTouchCount);
            return;
        }

        Vector2 currentCentroid = (firstTouch.position.ReadValue() + secondTouch.position.ReadValue()) * 0.5f;
        if (CameraController.instance != null)
            CameraController.instance.TryPanByTouchPositions(lastTouchCentroid, currentCentroid);
        lastTouchCentroid = currentCentroid;
    }

    private bool TryGetActiveTouchById(Touchscreen touchscreen, int requestedTouchId, out TouchControl result)
    {
        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            TouchControl touch = touchscreen.touches[i];
            if (touch.isInProgress && touch.touchId.ReadValue() == requestedTouchId)
            {
                result = touch;
                return true;
            }
        }

        result = null;
        return false;
    }

    private void IgnoreTouchInteractionUntilAllReleased(int activeTouchCount)
    {
        ClearTouchGestureState();
        ignoreTouchInteractionUntilAllReleased = activeTouchCount > 0;
    }

    private void ClearTouchGestureState()
    {
        touchStarted = false;
        firstTouchPos = Vector2.zero;
        touchId = -1;
        touchStartedOverUI = false;
        touchSelectionOwned = false;
        touchCameraOwned = false;
        ClearTouchPanState();
        lastTouchPosition = Vector2.zero;
        ClearTouchSelectionTool();
    }

    private void ClearTouchPanState()
    {
        touchPanOwned = false;
        touchPanTouchId1 = -1;
        touchPanTouchId2 = -1;
        touchPanTouch1StartedOverUI = false;
        touchPanTouch2StartedOverUI = false;
        lastTouchCentroid = Vector2.zero;
    }

    private bool TryStartTouchSelection(Vector2 touchPosition, out bool selectionToolAvailable)
    {
        selectionToolAvailable = false;

        if (!ActionControl.selectionGestureArmed || touchStartedOverUI || ActionControl.crossSectionsEnabled)
            return false;

        float dragThreshold;
        int activeToolCount = 0;

        if (ActionControl.boxSelection)
        {
            activeToolCount++;
            touchBoxSelection = ActionControl.Instance.boxSelectionScript;
            dragThreshold = touchBoxSelection != null ? touchBoxSelection.minDistanceToSelect : float.NaN;
        }
        else
        {
            dragThreshold = float.NaN;
        }

        if (ActionControl.brushSelection)
        {
            activeToolCount++;
            touchBrushSelection = BrushSelection.instance;
            dragThreshold = touchBrushSelection != null ? touchBrushSelection.minDistanceToSelect : float.NaN;
        }

        if (ActionControl.lassoSelection)
        {
            activeToolCount++;
            touchLassoSelection = LassoSelection.instance;
            dragThreshold = touchLassoSelection != null ? 0f : float.NaN;
        }

        if (activeToolCount != 1 || float.IsNaN(dragThreshold))
        {
            ClearTouchSelectionTool();
            return false;
        }

        selectionToolAvailable = true;
        return Vector2.Distance(firstTouchPos, touchPosition) > dragThreshold;
    }

    private void ProcessTouchSelection(bool wasPressed, bool isPressed, bool wasReleased, Vector2 position)
    {
        bool pointerOverUI = wasPressed
            ? touchStartedOverUI
            : EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);

        if (touchBoxSelection != null)
            touchBoxSelection.ProcessTouchInput(wasPressed, isPressed, wasReleased, position, pointerOverUI);
        else if (touchBrushSelection != null)
            touchBrushSelection.ProcessTouchInput(wasPressed, isPressed, wasReleased, position, pointerOverUI);
        else if (touchLassoSelection != null)
            touchLassoSelection.ProcessTouchInput(wasPressed, isPressed, wasReleased, position, pointerOverUI);
    }

    private void ClearTouchSelectionOwnership()
    {
        touchSelectionOwned = false;
        touchCameraOwned = false;
        touchStarted = false;
        touchStartedOverUI = false;
        ClearTouchSelectionTool();
        touchId = -1;
        lastTouchPosition = Vector2.zero;
        ClearTouchPanState();
    }

    private void ClearTouchSelectionTool()
    {
        touchBoxSelection = null;
        touchBrushSelection = null;
        touchLassoSelection = null;
    }

    private void SelectAtPosition(Vector2 screenPosition)
    {
        Ray ray = cam.ScreenPointToRay(screenPosition);

        if (ActionControl.crossSectionsEnabled)
        {
            Physics.Raycast(ray, out hit, 100, finalmask);

            ray.origin = ray.GetPoint(100);
            ray.direction = -ray.direction;

            hits = Physics.RaycastAll(ray, 100, finalmask);

            if (hits.Length == 0)
                return;

            TangibleBodyPart selectedBodyPart = GetFirstAfterPlane(hits, hit);

            if (selectedBodyPart == null)
                return;

            objectSelected = selectedBodyPart.gameObject;
            bodyPartScript = selectedBodyPart;
        }
        else
        {
            if (!Physics.Raycast(ray, out hit, 100, finalmask))
                return;

            objectSelected = hit.transform.gameObject;
            bodyPartScript = objectSelected.GetComponent<TangibleBodyPart>();
        }

        if (LayerMask.LayerToName(objectSelected.layer).Equals("Cube"))
        {
            GizmoFace faceClicked = GizmoBehaviour.instance.GetHitFace(hit);
            GizmoBehaviour.instance.SetCameraRotation(faceClicked);

            if (ActionControl.crossSectionsEnabled)
                CrossPlanesGizmo.Instance.SetPlane(faceClicked);

            return;
        }

        if (!ActionControl.creatingLocalNote)
        {
            Label labelScript = objectSelected.GetComponent<Label>();

            if (bodyPartScript != null)
                bodyPartScript.ObjectClicked();

            if (labelScript != null)
                labelScript.Click();
        }
    }

    private void Highlight()
    {
        var mousePos = Mouse.current.position.ReadValue();
        var worldMousePos = cam.ScreenToWorldPoint(mousePos);

        Ray centarRay = cam.ScreenPointToRay(mousePos);
        bool raycastHit = Physics.Raycast(centarRay, out hit, 100, finalmask);

        bool sphereHit = false;

        if (!raycastHit)
        {
            Vector3 A = worldMousePos - cam.transform.forward * 10f;
            Vector3 B = worldMousePos + cam.transform.forward * 10f;
            Vector3 direction = B - A;
            sphereHit = Physics.SphereCast(A, clickRadius / 14 * cam.orthographicSize, direction, out hit, 30, finalmask);
        }

        if (raycastHit || sphereHit)
        {
            objectSelected = hit.transform.gameObject;
            bodyPartScript = objectSelected.GetComponent<TangibleBodyPart>();

            if (prevbodyPartScript != null && bodyPartScript != prevbodyPartScript)
                prevbodyPartScript.MouseExit();

            prevbodyPartScript = bodyPartScript;

            if (bodyPartScript != null)
            {
                bodyPartScript.MouseEnter();
                if (ActionControl.nameOnMouse)
                    highlightText.text = bodyPartScript.nameScript.name;
                else
                    highlightText.text = "";
            }
            else
                highlightText.text = "";
        }
        else
        {
            if (bodyPartScript != null)
            {
                bodyPartScript.MouseExit();
                highlightText.text = "";
                bodyPartScript = null;
            }
            objectSelected = null;
        }
    }

    private void HighlightWithPlane()
    {
        try
        {
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Physics.Raycast(ray, out hit, 100, finalmask);
            ray.origin = ray.GetPoint(100);
            ray.direction = -ray.direction;
            hits = Physics.RaycastAll(ray, 100, finalmask);

            if (hits.Length > 0)
            {
                objectSelected = GetFirstAfterPlane(hits, hit).gameObject;
                bodyPartScript = objectSelected.GetComponent<TangibleBodyPart>();

                if (prevbodyPartScript != null && bodyPartScript != prevbodyPartScript)
                    prevbodyPartScript.MouseExit();

                prevbodyPartScript = bodyPartScript;

                if (bodyPartScript != null)
                {
                    bodyPartScript.MouseEnter();
                    if (ActionControl.nameOnMouse)
                        highlightText.text = bodyPartScript.nameScript.name;
                    else
                        highlightText.text = "";
                }
                else
                    highlightText.text = "";
            }
            else
            {
                if (bodyPartScript != null)
                {
                    bodyPartScript.MouseExit();
                    highlightText.text = "";
                    bodyPartScript = null;
                }
                objectSelected = null;
            }
        }
        catch
        {
            return;
        }
    }

    private void ShowContextualMenu()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame && (bodyPartScript != null || SelectedObjectsManagement.Instance.selectedObjects.Count > 0))
        {
            firstMousePos = Mouse.current.position.ReadValue();
            if (bodyPartScript != null)
                ContextualMenu.Instance.contextObject = bodyPartScript.gameObject;
            else
                ContextualMenu.Instance.contextObject = null;
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame && Vector3.Distance(firstMousePos, Mouse.current.position.ReadValue()) < 10f)
        {
            ContextualMenu.Instance.Show();
        }
    }
}
