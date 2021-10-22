using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CarouselController : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    private const float MAX_VELOCITY = 13;
    private const float MIN_VELOCITY = 0.8f;
    private const float TIME_TO_CENTER = 0.5f;
    private const float DECELERATION_TIME = 1.0f;

    [SerializeField] ScrollRect scrollRect;
    [SerializeField] RectTransform viewport;
    [SerializeField] HorizontalLayoutGroup layout;
    [SerializeField] AnimationCurve centeringCurve;

    private int leftMostIndex;
    private int rightMostIndex;
    private int centerIndex;
    private int selectedIndex;
    [SerializeField] private float currentPosition;
    private float previousPosition;
    private float initialPosition;
    private float targetPosition;
    private float moveTimer;
    private float dragVelocity;
    private float currentVelocity;
    private float thumbnailWidth;
    private float gap;
    private bool scrollControlsDragging;
    private bool isDragging;
    private bool isCentering;
    private RectTransform layoutRect;
    private RectTransform selectedItem;
    private List<RectTransform> items;
    private Vector2 lastDragPosition;

    public void Start()
    {
        layoutRect = layout.GetComponent<RectTransform>();
        items = new List<RectTransform>();
        int i = 0;
        foreach (RectTransform child in layoutRect)
        {
            if (!child.gameObject.activeInHierarchy) continue;

            int index = i;
            Button button = child.GetComponent<Button>();
            button?.onClick.AddListener(() => { OnSelectedItem(index); });
            i++;
            items.Add(child);
        }

        thumbnailWidth = items[0].GetComponent<RectTransform>().rect.width;
        gap = layout.spacing;
        leftMostIndex = 0;
        rightMostIndex = items.Count - 1;
        centerIndex = ((items.Count) / 2) - 1;
        targetPosition = (items.Count % 2 == 0) ? thumbnailWidth * 0.5f : 0;
        currentPosition = targetPosition;

        scrollControlsDragging = items.Count * (thumbnailWidth + gap) < viewport.rect.width;
        scrollRect.enabled = scrollControlsDragging;
    }

    void Update()
    {
        //LayoutRebuilder.MarkLayoutForRebuild(layoutRect); // USE THIS IF YOU ARE SCALING THE ELEMENTES TO MAINTING THE RIGHT GAP

        if (!scrollControlsDragging)
        {
            layoutRect.anchoredPosition = Vector2.right * currentPosition;
            if (currentPosition + items[rightMostIndex].anchoredPosition.x + items[rightMostIndex].rect.xMin > layoutRect.rect.width - gap)
            {
                SwitchRightMost();
            }
            if (currentPosition + items[leftMostIndex].anchoredPosition.x + items[leftMostIndex].rect.xMax < gap)
            {
                SwitchLeftMost();
            }
        }

        if (isCentering)
        {
            moveTimer += Time.deltaTime;
            if (scrollControlsDragging)
            {
                scrollRect.horizontalNormalizedPosition = Mathf.Lerp(initialPosition, targetPosition, centeringCurve.Evaluate(moveTimer / TIME_TO_CENTER));
                if (moveTimer > TIME_TO_CENTER)
                {
                    isCentering = false;
                    scrollRect.enabled = true;
                }
            }
            else
            {
                if (moveTimer < TIME_TO_CENTER)
                {
                    currentPosition = Mathf.Lerp(initialPosition, targetPosition, centeringCurve.Evaluate(moveTimer / TIME_TO_CENTER));
                }
                else
                {
                    isCentering = false;
                }
            }
        }
        else if (!scrollControlsDragging)
        {
            if (isDragging)
            {
                dragVelocity = (currentPosition - previousPosition) / Time.deltaTime;
                previousPosition = currentPosition;
            }
            else
            {
                moveTimer += Time.deltaTime;
                if (moveTimer < DECELERATION_TIME)
                {
                    currentVelocity = Mathf.Lerp(dragVelocity, 0, moveTimer / DECELERATION_TIME);
                    currentPosition += currentVelocity;
                    if (Mathf.Abs(currentVelocity) < MIN_VELOCITY)
                    {
                        moveTimer = DECELERATION_TIME;
                    }
                }
            }
        }
    }

    public void Select(int index)
    {
        OnSelectedItem(index);
    }

    public void ScrollToSelected()
    {
        OnSelectedItem(selectedIndex);
    }

    private void OnSelectedItem(int index)
    {
        Debug.Log($"on selected {index}");
        if (items.Count == 0) return;

        index = Mathf.Clamp(index, 0, items.Count - 1);

        if (selectedItem == items[index]) return;

        selectedIndex = index;
        selectedItem = items[index];

        isCentering = true;
        moveTimer = 0f;
        if (scrollControlsDragging)
        {
            initialPosition = scrollRect.horizontalNormalizedPosition;
            targetPosition = (float)index / (float)(items.Count - 1);
            scrollRect.enabled = false;
        }
        else
        {
            initialPosition = currentPosition;
            targetPosition = layoutRect.rect.width * 0.5f - selectedItem.anchoredPosition.x;// + 0.5f * thumbnailWidth;
        }
    }

    private void SwitchLeftMost()
    {
        items[leftMostIndex].transform.SetAsLastSibling();
        float offset = thumbnailWidth + gap;
        currentPosition += offset;
        initialPosition += offset;
        targetPosition += offset;
        layoutRect.anchoredPosition = Vector2.right * currentPosition;
        rightMostIndex = leftMostIndex;
        leftMostIndex = (leftMostIndex < items.Count - 1) ? leftMostIndex + 1 : 0;
        centerIndex = (centerIndex < items.Count - 1) ? centerIndex + 1 : 0;
    }

    private void SwitchRightMost()
    {
        items[rightMostIndex].transform.SetAsFirstSibling();
        float offset = thumbnailWidth + gap;
        currentPosition -= offset;
        initialPosition -= offset;
        targetPosition -= offset;
        layoutRect.anchoredPosition = Vector2.right * currentPosition;
        leftMostIndex = rightMostIndex;
        rightMostIndex = (rightMostIndex > 0) ? rightMostIndex - 1 : items.Count - 1;
        centerIndex = (centerIndex > 0) ? centerIndex - 1 : items.Count - 1;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (scrollControlsDragging || isCentering || eventData.button != PointerEventData.InputButton.Left)
            return;

        isDragging = true;
        lastDragPosition = Vector2.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out lastDragPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (scrollControlsDragging || !isDragging || eventData.button != PointerEventData.InputButton.Left)
            return;

        isDragging = false;
        moveTimer = 0;
        dragVelocity = Mathf.Clamp(dragVelocity * Time.deltaTime, -MAX_VELOCITY, MAX_VELOCITY);
        currentVelocity = dragVelocity;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (scrollControlsDragging || !isDragging)
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out Vector2 dragPosition))
            return;

        Vector2 delta = dragPosition - lastDragPosition;
        lastDragPosition = dragPosition;

        currentPosition += delta.x;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        previousPosition = currentPosition;
        dragVelocity = 0;
    }
}
