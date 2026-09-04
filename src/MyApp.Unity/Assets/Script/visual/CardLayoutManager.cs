using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public enum FieldType
{
    Deck,
    Hand,
    Field,
    Garbage
}

public class CardLayoutManager : MonoBehaviour
{
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform drawField;
    public FieldType fieldType;
    
    [Header("Display Settings")]
    [Tooltip("デッキや相手の手札など、裏向きに表示する場合はチェック")]
    [SerializeField] private bool isFaceDown = false;

    [Header("Effects")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("Selection Settings")]
    public bool IsSelectionMode { get; private set; } = false; // マウスクラスからドラッグ禁止を判定するためのフラグ
    private float selectionScale = 1.5f; // 選択時の最大サイズ
    private Vector3[] selectionCardPos;

    private Vector3[] cardPos;
    private Vector3 leftTop;
    private float worldWidth;
    private float worldHeight;
    private float actualCardWidth = -1f;
    private float actualCardHeight = -1f;
    
    private float currentScale = 1.0f;

    private List<CardData> cards = new List<CardData>();
    private List<GameObject> fieldClone = new List<GameObject>();

    private void Awake()
    {
        Initialize();
    }

    public void CreateCard(CardData data)
    {
        GameObject cardObj = Instantiate(cardPrefab, leftTop, Quaternion.identity,drawField);

        CardView view = cardObj.GetComponent<CardView>();
        if(view != null)
        {
            view.Setup(data);
        }

        cards.Add(data);
        fieldClone.Add(cardObj);

        cardObj.transform.localScale = Vector3.zero;

        CalculateLayout(cards.Count);
        RefreshCard();


    }
    public void Initialize()
    {
        if (drawField == null || cardPrefab == null) return;

        // フィールドの四隅から描画範囲を計算
        Vector3[] corners = new Vector3[4];
        drawField.GetWorldCorners(corners);
        leftTop = corners[1];
        leftTop.z = 0;
        worldWidth = corners[2].x - corners[1].x;
        worldHeight = corners[1].y - corners[0].y;

        // プレハブの本来のサイズを取得
        Canvas childCanvas = cardPrefab.GetComponentInChildren<Canvas>();
        if (childCanvas != null)
        {
            RectTransform canvasRect = childCanvas.GetComponent<RectTransform>();
            actualCardWidth = canvasRect.rect.width;
            actualCardHeight = canvasRect.rect.height;
        }

        // 初期状態で位置を計算しておく
        CalculateLayout(Mathf.Max(1, cards.Count));
    }

    private void CalculateLayout(int targetCardCount)
    {
        if (actualCardWidth <= 0 || targetCardCount == 0) return;

        currentScale = 1.0f;
        int canCardNumX = 1;
        int canCardNumY = 1;

        // 手札とフィールドの場合は、枠に収まるようにスケールを半分にしていく
        if (fieldType == FieldType.Hand || fieldType == FieldType.Field)
        {
            while (currentScale > 0.05f) // 無限に小さくなるのを防ぐ
            {
                float w = actualCardWidth * currentScale;
                float h = actualCardHeight * currentScale;
                
                canCardNumX = Mathf.Max(1, (int)(worldWidth / w));
                canCardNumY = Mathf.Max(1, (int)(worldHeight / h));

                // 必要なカード数が収まるならループ終了
                if (canCardNumX * canCardNumY >= targetCardCount) break;
                
                currentScale /= 2f; // 収まらなければ半分にする
            }
        }

        cardPos = new Vector3[targetCardCount];
        float curW = actualCardWidth * currentScale;
        float curH = actualCardHeight * currentScale;
        
        float areaW = canCardNumX > 1 ? (worldWidth - curW) / (canCardNumX - 1) : 0;
        float areaH = canCardNumY > 1 ? (worldHeight - curH) / (canCardNumY - 1) : 0;

        for (int i = 0; i < targetCardCount; i++)
        {
            // デッキとガベージは中央に重ねて表示する
            if (fieldType == FieldType.Deck || fieldType == FieldType.Garbage)
            {
                float centerX = leftTop.x + (worldWidth / 2f);
                float centerY = leftTop.y - (worldHeight / 2f);
                // 重なった時のチラつき（Zファイティング）防止のため、Z軸を少しだけズラす
                cardPos[i] = new Vector3(centerX, centerY, leftTop.z - (i * 0.01f));
            }
            // 手札とフィールドはグリッド状に広げて表示する
            else
            {
                int xIndex = i % canCardNumX;
                int yIndex = i / canCardNumX;
                float posX = leftTop.x + (curW / 2f) + (areaW * xIndex);
                float posY = leftTop.y - (curH / 2f) - (areaH * yIndex);
                cardPos[i] = new Vector3(posX, posY, leftTop.z);
            }
        }
    }

    public void RefreshCard()
    {
        Vector3 targetRot = isFaceDown ? new Vector3(0, 180, 0) : Vector3.zero;
        Vector3 targetScale = Vector3.one * currentScale;

        for (int i = 0; i < fieldClone.Count; i++)
        {
            if (i >= cardPos.Length) break;

            GameObject card = fieldClone[i];
            Vector3 pos = cardPos[i];

            card.transform.DOKill();
            card.transform.DOMove(pos, 0.25f).SetEase(Ease.OutExpo);
            card.transform.DOScale(targetScale, 0.25f).SetEase(Ease.OutExpo);
            card.transform.DORotate(targetRot, 0.25f).SetEase(Ease.OutExpo);
        }
    }

    public void RemoveCard(CardData data, GameObject obj)
    {
        int index = fieldClone.IndexOf(obj);
        if (index >= 0)
        {
            cards.RemoveAt(index);
            fieldClone.RemoveAt(index);
        }

        // サイズを初期状態(等倍)に戻す
        obj.transform.localScale = Vector3.one;

        // 抜けた穴を埋めるために再計算＆リフレッシュ
        CalculateLayout(Mathf.Max(1, cards.Count));
        RefreshCard();
    }

    public void ReceiveCard(CardLayoutManager fromManager, CardData data, GameObject obj)
    {
        FieldType sourceType = fromManager.fieldType;
        
        // 元のマネージャーの管理から外す
        fromManager.RemoveCard(data, obj);

        // 自身の管理に追加
        cards.Add(data);
        fieldClone.Add(obj);

        // 増えた枚数でスケールや座標を再計算
        CalculateLayout(cards.Count);
        
        // 既存のカードを再配置
        RefreshCard();

        // 今回受け取ったカードだけは、派手な登場アニメーションをさせる
        int newIndex = cards.Count - 1;
        MoveCard(obj, sourceType, newIndex);
    }

    // アニメーション実行処理
    private void MoveCard(GameObject card, FieldType sourceType, int targetIndex)
    {
        if (targetIndex >= cardPos.Length) return;

        card.transform.DOKill();

        Vector3 targetPosVec = cardPos[targetIndex];
        Vector3 targetRot = isFaceDown ? new Vector3(0, 180, 0) : Vector3.zero;
        Vector3 targetScale = Vector3.one * currentScale;

        Sequence seq = DOTween.Sequence();

        switch (fieldType)
        {
            case FieldType.Deck:
            case FieldType.Garbage:
                seq.Append(card.transform.DOMove(targetPosVec, 0.5f).SetEase(Ease.InOutQuad));
                seq.Join(card.transform.DORotate(targetRot, 0.5f));
                seq.Join(card.transform.DOScale(targetScale, 0.5f));
                break;

            case FieldType.Field:
            case FieldType.Hand:
                if (sourceType == FieldType.Garbage || sourceType == FieldType.Deck)
                {
                    // デッキや墓地からはジャンプ着地
                    seq.Append(card.transform.DOJump(targetPosVec, 2.0f, 1, 0.5f).SetEase(Ease.OutQuad));
                    seq.Join(card.transform.DORotate(targetRot, 0.5f));
                    seq.Join(card.transform.DOScale(targetScale, 0.5f));
                }
                else
                {
                    // 手札からはスッと移動
                    seq.Append(card.transform.DOMove(targetPosVec, 0.5f).SetEase(Ease.OutCubic));
                    seq.Join(card.transform.DORotate(targetRot, 0.5f, RotateMode.FastBeyond360));
                    seq.Join(card.transform.DOScale(targetScale, 0.5f));
                }
                break;
        }
    }

    public void SpawnTokenCard(CardData data)
    {
        cards.Add(data);
        
        // 再計算と既存カードの整頓
        CalculateLayout(cards.Count);
        RefreshCard();

        int targetIndex = cards.Count - 1;
        if (targetIndex >= cardPos.Length) return;

        Vector3 targetPosVec = cardPos[targetIndex];
        Vector3 targetRot = isFaceDown ? new Vector3(0, 180, 0) : Vector3.zero;
        Vector3 targetScale = Vector3.one * currentScale;

        // 空中から生成する
        Vector3 startPos = targetPosVec + new Vector3(0, 1.5f, 0);
        GameObject card = Instantiate(cardPrefab, startPos, Quaternion.identity);
        fieldClone.Add(card);

        card.transform.localScale = Vector3.zero; // 最初は見えない

        Sequence seq = DOTween.Sequence();
        seq.Append(card.transform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack));
        seq.Join(card.transform.DOJump(targetPosVec, 1.0f, 1, 0.5f).SetEase(Ease.OutQuad));
        seq.Join(card.transform.DORotate(targetRot, 0.5f));
    }

    public GameObject FindCardObject(CardData data)
    {
        foreach(var obj in fieldClone)
        {
            CardView view = obj.GetComponent<CardView>();
            if(view.CurrentData.uniqueId == data.uniqueId)
            {
                return obj;
            }
        }
        return null;
    }

    public void ReceiveFailSafeCard(CardLayoutManager deckManager, CardData data)
    {
        // デッキから対象のカードを探す
        GameObject targetObj = deckManager.FindCardObject(data);
        if (targetObj == null) return;

        // デッキの管理から外す
        deckManager.RemoveCard(data, targetObj);

        // 自身の管理に加える
        cards.Add(data);
        fieldClone.Add(targetObj);

        CalculateLayout(cards.Count);
        RefreshCard(); 

        int targetIndex = cards.Count - 1;
        if (targetIndex >= cardPos.Length) return;

        Vector3 targetPosVec = cardPos[targetIndex];
        Vector3 targetRot = isFaceDown ? new Vector3(0, 180, 0) : Vector3.zero;
        Vector3 targetScale = Vector3.one * currentScale;

        // フェイルセーフ専用の派手なアニメーション
        Sequence seq = DOTween.Sequence();
        Vector3 centerPos = new Vector3(0, 0, -3f); // 画面中央手前

        seq.Append(targetObj.transform.DOMove(centerPos, 0.4f).SetEase(Ease.OutCubic));
        seq.Join(targetObj.transform.DOScale(Vector3.one * 1.5f, 0.4f)); // 巨大化
        seq.Append(targetObj.transform.DOShakeRotation(0.6f, new Vector3(0, 0, 30f), 10)); // 警告のブルブル
        seq.Append(targetObj.transform.DOMove(targetPosVec, 0.4f).SetEase(Ease.OutCubic)); // 定位置へ
        seq.Join(targetObj.transform.DOScale(targetScale, 0.4f)); // 周りに合わせたスケールへ戻る
        seq.Join(targetObj.transform.DORotate(targetRot, 0.4f, RotateMode.FastBeyond360));
    }

    public void BeginSelectionMode(List<int> targetUniqueIds)
    {
        IsSelectionMode = true;
        List<GameObject> targetObjects = new List<GameObject>();
        
        for (int i = 0; i < cards.Count; i++)
        {
            GameObject obj = fieldClone[i];
            Collider col = obj.GetComponent<Collider>();
            
            // 対象のカードの場合
            if (targetUniqueIds.Contains(cards[i].uniqueId))
            {
                targetObjects.Add(obj);
                if (col != null) col.enabled = true;
            }
            else
            {
                if (col != null) col.enabled = false;
                obj.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InQuint);
            }
        }

        // 選択されたカードだけを画面中央に綺麗に並べる計算を行う
        CalculateSelectionLayout(targetObjects.Count);

        Vector3 targetRot = isFaceDown ? new Vector3(0, 180, 0) : Vector3.zero;
        Vector3 targetScaleVec = Vector3.one * selectionScale;

        // 対象カードを定位置へ素早く滑らかにアニメーション
        for (int i = 0; i < targetObjects.Count; i++)
        {
            GameObject card = targetObjects[i];
            Vector3 pos = selectionCardPos[i];
            pos.z = leftTop.z - 2f; // 手前に出す

            // Ease.OutExpo は初速が速く、ピタッと滑らかに止まるDCGに最適なカーブです
            card.transform.DOMove(pos, 0.25f).SetEase(Ease.OutExpo);
            card.transform.DOScale(targetScaleVec, 0.25f).SetEase(Ease.OutExpo);
            card.transform.DORotate(targetRot, 0.25f).SetEase(Ease.OutExpo);
        }
    }
    private void CalculateSelectionLayout(int targetCardCount)
    {
        selectionCardPos = new Vector3[targetCardCount];
        if (actualCardWidth <= 0 || targetCardCount == 0) return;

        selectionScale = 1.5f; // 通常より少し大きめからスタート
        int canCardNumX = 1;
        int canCardNumY = 1;

        // 画面に収まるまで縮小
        while (selectionScale > 0.2f)
        {
            float w = actualCardWidth * selectionScale;
            float h = actualCardHeight * selectionScale;
            
            canCardNumX = Mathf.Max(1, (int)(worldWidth / w));
            canCardNumY = Mathf.Max(1, (int)(worldHeight / h));

            if (canCardNumX * canCardNumY >= targetCardCount) break;
            selectionScale -= 0.1f;
        }

        float curW = actualCardWidth * selectionScale;
        float curH = actualCardHeight * selectionScale;
        
        // 行と列を計算して、全体が中央に来るようにオフセットを計算
        int columns = Mathf.Min(targetCardCount, canCardNumX);
        int rows = Mathf.CeilToInt((float)targetCardCount / columns);

        float paddingX = curW * 0.1f; // カード間の隙間
        float paddingY = curH * 0.1f;

        float totalWidth = (curW * columns) + (paddingX * (columns - 1));
        float totalHeight = (curH * rows) + (paddingY * (rows - 1));

        float startX = leftTop.x + (worldWidth - totalWidth) / 2f + (curW / 2f);
        float startY = leftTop.y - (worldHeight - totalHeight) / 2f - (curH / 2f);

        for (int i = 0; i < targetCardCount; i++)
        {
            int col = i % columns;
            int row = i / columns;
            
            float posX = startX + (curW + paddingX) * col;
            float posY = startY - (curH + paddingY) * row;
            selectionCardPos[i] = new Vector3(posX, posY, 0);
        }
    }

    public void EndSelectionMode()
    {
        IsSelectionMode = false;
        
        // 全てのカードの当たり判定を元に戻す
        EnableAllColliders();

        // 既存の計算メソッドを呼んで元の位置・サイズに戻す
        CalculateLayout(Mathf.Max(1, cards.Count));
        RefreshCard();
    }

    public void DisableAllColliders()
    {
        foreach (GameObject obj in fieldClone)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    public void EnableAllColliders()
    {
        if (IsSelectionMode) return; // 自分がセレクトモード中なら無視する

        foreach (GameObject obj in fieldClone)
        {
            Collider col = obj.GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }
    }
}