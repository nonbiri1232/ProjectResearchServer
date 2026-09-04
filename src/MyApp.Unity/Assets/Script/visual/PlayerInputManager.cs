using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;
public enum InputState
{
    Normal,         // 通常状態（ホバーのみ）
    DraggingHand,   // 手札をドラッグ中（プレイ準備）
    DraggingField,  // 場のカードをドラッグ中（攻撃準備）
    SelectingTarget, // 対象を選択中
    SelectingMarigan //マリガン選択中
}

public class PlayerInputManager : MonoBehaviour
{
    [Header("Managers")]
    public BattleManager battleManager; // DebugBattleManagerをアタッチする
    public BattleUIManager uiManager;
    public CardLayoutManager p1HandLayout;
    public CardLayoutManager p1MariganLayout;

    [Header("UI Areas (Play)")]
    public RectTransform normalPlayArea; 
    public RectTransform addCostPlayArea; 
    public GameObject playAreaUI; 

    [Header("Attack Line Settings")]
    public LineRenderer attackLine; 
    public int lineResolution = 20; 

    private InputState currentState = InputState.Normal;
    private GameObject draggingCard = null;
    private CardView draggingCardView = null;
    
    private Vector3 originalPos; 
    private float zDistance;

    private int requiredTargetCount;
    private List<CardData> selectedTargets = new List<CardData>();
    private bool isPlayWithAddCost = false;

    private void Start()
    {
        if (attackLine != null) attackLine.enabled = false;
        if (playAreaUI != null) playAreaUI.SetActive(false);
    }

    private void Update()
    {
        switch (currentState)
        {
            case InputState.Normal: HandleNormalState(); break;
            case InputState.DraggingHand: HandleDraggingHand(); break;
            case InputState.DraggingField: HandleDraggingField(); break;
            case InputState.SelectingTarget: HandleSelectingTarget(); break;
            case InputState.SelectingMarigan: HandleSelectingMarigan(); break;
        }
    }
    public void StartMariganSelection()
    {
        currentState = InputState.SelectingMarigan;
        selectedTargets.Clear();
    }
    public void ConfirmMarigan()
    {
        if (currentState != InputState.SelectingMarigan) return;
        battleManager.SubmitMarigan(selectedTargets);
        selectedTargets.Clear();
        currentState = InputState.Normal;
    }
    private void HandleSelectingMarigan()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Card"))
            {
                CardView view = hit.collider.GetComponent<CardView>();
                if (view != null && uiManager != null) uiManager.ShowPopUp(view.AbilityText); // ホバー表示
                
                // クリック時
                if (Input.GetMouseButtonDown(0))
                {
                    // マリガン領域にあるカードだけを選択可能にする
                    if (p1MariganLayout != null && p1MariganLayout.FindCardObject(view.CurrentData) != null)
                    {
                        if (selectedTargets.Contains(view.CurrentData))
                        {
                            selectedTargets.Remove(view.CurrentData);
                            view.SetHighlight(false);
                        }
                        else
                        {
                            selectedTargets.Add(view.CurrentData);
                            view.SetHighlight(true);
                        }
                    }
                }
            }
            else { if (uiManager != null) uiManager.HidePopUp(); }
        }
        else { if (uiManager != null) uiManager.HidePopUp(); }
    }

    private void HandleNormalState()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Card"))
            {
                CardView view = hit.collider.GetComponent<CardView>();
                if (view != null && uiManager != null) uiManager.ShowPopUp(view.AbilityText); 

                if (Input.GetMouseButtonDown(0) && battleManager.CanAct()) // ★CanAct()チェックを追加
                {
                    if (uiManager != null) uiManager.HidePopUp(); 
                    BeginDrag(hit.collider.gameObject);
                }
            }
            else
            {
                if (uiManager != null) uiManager.HidePopUp();
            }
        }
        else
        {
            if (uiManager != null) uiManager.HidePopUp();
        }
    }

    private void BeginDrag(GameObject cardObj)
    {
        cardObj.transform.DOKill();

        draggingCard = cardObj;
        draggingCardView = cardObj.GetComponent<CardView>();
        originalPos = cardObj.transform.position;
        zDistance = Camera.main.WorldToScreenPoint(originalPos).z;

        if (draggingCardView.IsHandCard)
        {
            currentState = InputState.DraggingHand;
            if (playAreaUI != null) playAreaUI.SetActive(true);
        }
        else if (draggingCardView.IsFieldCard && draggingCardView.CurrentData.canAttackNow)
        {
            currentState = InputState.DraggingField;
            if (attackLine != null) attackLine.enabled = true;
        }
        else
        {
            draggingCard = null;
            draggingCardView = null;
        }
    }

    private void HandleDraggingHand()
    {
        UpdateCardPositionToMouse();

        if (Input.GetMouseButtonUp(0))
        {
            if (playAreaUI != null) playAreaUI.SetActive(false);

            if (IsMouseOverRect(addCostPlayArea))
            {
                AttemptPlay(true);
            }
            else if (IsMouseOverRect(normalPlayArea))
            {
                AttemptPlay(false);
            }
            else if (Input.mousePosition.y > Screen.height * 0.4f)
            {
                AttemptPlay(false);
            }
            else
            {
                CancelDrag(); // 枠外なら元の位置に戻る
            }
        }
    }

    private bool IsMouseOverRect(RectTransform rect)
    {
        if (rect == null) return false;
        Camera cam = null;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        
        if (canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
        {
            cam = canvas.worldCamera;
            if (cam == null) cam = Camera.main;
        }
        
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
    }

    private void HandleDraggingField()
    {
        DrawAttackCurve(draggingCard.transform.position, Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
        {
            if (attackLine != null) attackLine.enabled = false;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Card"))
                {
                    CardView targetView = hit.collider.GetComponent<CardView>();
                    if (targetView != null && !targetView.IsMyCard)
                    {
                        // ★送信を有効化
                        battleManager.SubmitAttack(draggingCardView.CurrentData, targetView.CurrentData);
                    }
                }
                else if (hit.collider.CompareTag("EnemyPlayer"))
                {
                    // ★ダイレクトアタック
                    battleManager.SubmitAttack(draggingCardView.CurrentData, null);
                }
            }
            CancelDrag();
        }
    }

    private void HandleSelectingTarget()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.CompareTag("Card"))
            {
                CardView targetView = hit.collider.GetComponent<CardView>();
                if (targetView != null)
                {
                    if (selectedTargets.Contains(targetView.CurrentData))
                    {
                        selectedTargets.Remove(targetView.CurrentData);
                        targetView.SetHighlight(false);
                    }
                    else
                    {
                        selectedTargets.Add(targetView.CurrentData);
                        targetView.SetHighlight(true);

                        if (selectedTargets.Count >= requiredTargetCount)
                        {
                            ConfirmPlayWithTargets();
                        }
                    }
                }
            }
            else
            {
                CancelTargetSelection();
            }
        }
    }

    private void UpdateCardPositionToMouse()
    {
        Vector3 mouseScreenPos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDistance);
        draggingCard.transform.position = Camera.main.ScreenToWorldPoint(mouseScreenPos);
    }

    private void CancelDrag()
    {
        if (draggingCard != null)
        {
            draggingCard.transform.DOMove(originalPos, 0.25f).SetEase(Ease.OutCubic);
        }
        draggingCard = null;
        draggingCardView = null;
        currentState = InputState.Normal;
    }

    private void AttemptPlay(bool isAddCost)
    {
        isPlayWithAddCost = isAddCost;
        int targetCount = battleManager.RequiresTargetCount(draggingCardView.CurrentData);

        if (targetCount > 0)
        {
            requiredTargetCount = targetCount;
            selectedTargets.Clear();
            currentState = InputState.SelectingTarget;
            
            // ★ここではデバッグ用に一時的にカードを見えなくする
            draggingCard.SetActive(false); 
        }
        else
        {
            // ★対象不要なら即プレイ送信
            battleManager.SubmitPlay(draggingCardView.CurrentData, isPlayWithAddCost, null);
            draggingCard = null;
            draggingCardView = null;
            currentState = InputState.Normal;
        }
    }

    private void ConfirmPlayWithTargets()
    {
        // ★ターゲット付きでプレイ送信
        battleManager.SubmitPlay(draggingCardView.CurrentData, isPlayWithAddCost, selectedTargets);

        foreach (var data in selectedTargets) { /* Highlight解除処理 */ }

        draggingCard.SetActive(true);
        draggingCard = null;
        draggingCardView = null;
        currentState = InputState.Normal;
    }

    private void CancelTargetSelection()
    {
        p1HandLayout.EndSelectionMode();
        foreach (var data in selectedTargets) { /* Highlight解除処理 */ }
        draggingCard.SetActive(true);
        CancelDrag();
    }

    private void DrawAttackCurve(Vector3 startWorldPos, Vector3 mouseScreenPos)
    {
        mouseScreenPos.z = zDistance;
        Vector3 endWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        Vector3 controlPos = (startWorldPos + endWorldPos) / 2f;
        controlPos.y += 2.0f;

        attackLine.positionCount = lineResolution + 1;
        for (int i = 0; i <= lineResolution; i++)
        {
            float t = i / (float)lineResolution;
            Vector3 point = CalculateBezierPoint(t, startWorldPos, controlPos, endWorldPos);
            attackLine.SetPosition(i, point);
        }
    }

    private Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        Vector3 p = uu * p0; 
        p += 2 * u * t * p1; 
        p += tt * p2;        
        return p;
    }
}