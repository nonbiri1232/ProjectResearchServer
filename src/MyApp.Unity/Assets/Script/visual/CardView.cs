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
    public bool CanShowAbility { get; private set; }

    public CardData CurrentData { get; private set; }

    public void SetImage(Sprite image)
    {
        cardImage.sprite = image;
        UpdateVisuals();
    }
    public void Setup(CardData data)
    {
        CurrentData = data;
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (costText != null) {
            costText.text = CurrentData.cost.ToString();
            costText.ForceMeshUpdate();
        }
        if (atkText != null) {
            atkText.text = CurrentData.atk.ToString();
            atkText.ForceMeshUpdate();
        }
        if (hpText != null) {
            hpText.text = CurrentData.hp.ToString();
            hpText.ForceMeshUpdate();
        }
    }

    public void SetPresentation(string abilityText, bool canShowAbility, Sprite sprite, bool isFaceDown)
    {
        CanShowAbility = canShowAbility && !isFaceDown && !string.IsNullOrWhiteSpace(abilityText);
        AbilityText = CanShowAbility ? abilityText : string.Empty;
        if (cardImage != null)
        {
            if (sprite != null) cardImage.sprite = sprite;
            cardImage.gameObject.SetActive(!isFaceDown);
        }
        if (costText != null) costText.transform.parent.gameObject.SetActive(!isFaceDown);
        if (atkText != null) atkText.transform.parent.gameObject.SetActive(!isFaceDown);
        if (hpText != null) hpText.transform.parent.gameObject.SetActive(!isFaceDown);

        UpdateVisuals();
    }

    public void SetHighlight(bool isSelected)
    {
        Image bgImage = cardImage;
        if (bgImage == null) return;
        bgImage.DOKill();
        if (isSelected)
        {
            // 水色に光らせて、無限にループして点滅（Yoyo）させる
            bgImage.DOColor(new Color(0.5f, 0.8f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            // 点滅をキャンセルして元の白に戻す
            bgImage.DOColor(Color.white, 0.2f);
        }
    }
}
