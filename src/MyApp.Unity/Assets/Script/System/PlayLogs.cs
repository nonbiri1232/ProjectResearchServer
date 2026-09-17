
using System;
using System.Collections.Generic;

public enum LogType
{
    TurnStart, 
    TurnEnd,
    PlayCard,
    Attack,
    Damage,
    Destroyed,
    SelfDestory,
    FailSafe,
    Marigan,
    DrawHand,
    DrawGabage,
    MemoryChanged
}

public class PlayLogEntry
{
    public int turnNumber;
    public bool isPlayer1;
    public LogType type;
    public CardData? sourceCard;
    public CardData[] targetCards;
    public int actionValue;

    public PlayLogEntry(int turnNumber, bool isPlayer1, LogType type, CardData? sourceCard = null, CardData[] targetCards = null, int actionValue = 0)
    {
        this.turnNumber = turnNumber;
        this.isPlayer1 = isPlayer1;
        this.type = type;
        this.sourceCard = sourceCard;
        this.targetCards = targetCards;
        this.actionValue = actionValue;
    }
}

public class PlayLog
{
    private List<PlayLogEntry> history = new List<PlayLogEntry>();

    public IReadOnlyList<PlayLogEntry> History => history;
    public event Action<PlayLogEntry> OnLogAdded;

    public void AddLog(int turnNumber, bool isPlayer1, LogType type, CardData? sourceCard = null, CardData[] targetCards = null, int actionValue = 0)
    {
        var entry = new PlayLogEntry(turnNumber, isPlayer1, type, sourceCard, targetCards, actionValue);
        history.Add(entry);

        OnLogAdded?.Invoke(entry);
    }
    public void AddLog(int turnNumber, bool isPlayer1, LogType type, CardData[] targetCards)
    {
        var entry = new PlayLogEntry(turnNumber, isPlayer1, type, null, targetCards, 0);
        history.Add(entry);

        OnLogAdded?.Invoke(entry);
    }


}