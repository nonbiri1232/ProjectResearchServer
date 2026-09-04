using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CardView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI atkText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image cardImage;

    public bool IsHandCard { get; set; } // 手札にいるか
    public bool IsFieldCard { get; set; } // 盤面にいるか
    public bool IsMyCard { get; set; } // 自分のカードか（敵のカードじゃないか）
    public string AbilityText { get; set; } // ホバーした時に表示する能力テキスト

    public CardData CurrentData { get; private set; }

    public void Setup(CardData data)
    {
        CurrentData = data;
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        costText.text = CurrentData.cost.ToString();
        atkText.text = CurrentData.atk.ToString();
        hpText.text = CurrentData.hp.ToString();
    }

    public void SetHighlight(bool isSelected)
    {
        Image bgImage = GetComponent<Image>(); // カードの背景画像
        if (isSelected)
        {
            // 水色に光らせて、無限にループして点滅（Yoyo）させる
            bgImage.DOColor(new Color(0.5f, 0.8f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            // 点滅をキャンセルして元の白に戻す
            bgImage.DOKill();
            bgImage.DOColor(Color.white, 0.2f);
        }
    }
}