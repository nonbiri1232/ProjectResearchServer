/// <summary>
/// 勝敗条件に直結する進行度と、教師AI専用のカード評価を提供する。
/// EvaluateCardの値は教師AIの手選びだけに使い、学習報酬には利用しない。
/// </summary>
public static class BoardEvaluator
{
    /// <summary>
    /// 最大メモリ差だけを使った勝利進行度。
    /// 手札、デッキ、盤面枚数など戦略依存の指標は含めない。
    /// </summary>
    public static float Evaluate(
        Player player,
        Player enemy,
        GameManager game = null)
    {
        if (player == null || enemy == null) return 0f;
        return (player.maxMemory - enemy.maxMemory) / 20f;
    }

    public static float EvaluateCard(Card card)
    {
        if (card == null) return 0f;

        float value = card.Cost;
        if (card.Type == Card.CardType.Object)
        {
            value += card.Attack * 0.7f + card.Hp * 0.5f;
            if (card.isProxy) value += 1f;
            if (card.isImmediate) value += 0.75f;
            if (card.isEncrypted) value += 0.75f;
            if (card.isSandBox) value += 0.75f;
            if (card.isSegfault) value += 0.5f;
            if (card.isDaemon) value += 0.5f;
            value += (card.attackTimes - 1) * 1.5f;
        }
        else
        {
            value += 1f;
        }

        return value;
    }
}
