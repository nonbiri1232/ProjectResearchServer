using System;
using System.Collections.Generic;
using System.Linq;

public struct CardData{
    public int uniqueId;
    public int id;
    public int type; //1.Object 2.Method 3.Scope
    public bool isProxy;
    public int cost;
    public int atk;
    public int hp;
    public bool canAttackNow;

}
public enum GameState
{
    Processing,
    WaitingForInput,
    Finished
}

public enum ActionType
{
    Marigan,
    SelfGarbage,
    Play,
    Attack,
    End
}
public enum PhaseState
{
    Start,
    Main,
    End
}
public class PlayerAction
{
    public ActionType type;
    public Card sourceCard;
    public List<Card> targetCard;
    public bool isAddCost;
    
    public PlayerAction(){}
    public PlayerAction(ActionType ty)
    {
        type = ty;
    }
    
    public PlayerAction(ActionType ty,Card source)
    {
        type = ty;
       sourceCard = source;
    }
    
    public PlayerAction(ActionType ty,List<Card> target)
    {
        type = ty;
        targetCard = new List<Card>();
        targetCard.AddRange(target);
    }
    public PlayerAction(ActionType ty,Card source,List<Card> target)
    {
        type = ty;
        sourceCard = source;
        targetCard = new List<Card>();
        targetCard.AddRange(target);
    }
    
}

public struct BoardData
{
    public List<CardData> selfField;
    public List<CardData> selfHand;
    
    public List<CardData> selfGarbage;
    public List<CardData> enemyField;
    
    public List<CardData> enemyGarbage;
}

public class GameManager
{
    private Player winner;
    public event Action<Player> OnGameFinished;
    public GameState currentState;
    public Player turn;
    public Player notrun;
    private const int maxHand = 8;
    public Player player1{get;private set;}
    public Player player2{get;private set;}
    Crest cr;
    public int systemTurn = 0;
    public bool isPlayer1Turn = true;
    public Card currentScope = null;
    public PhaseState currentPhase;
    List<Player> Didmarigan = new List<Player>();
    public PlayLog playLog = new PlayLog();
    public int decisionTick = 0;

    public GameManager(Player first,Player second)
    {
        player1 = first;
        player2 = second;
        Didmarigan.Add(first);
        Didmarigan.Add(second);
        cr = new Crest(player1,player2);

        foreach(var c in player1.deck)c.cr = cr;
        foreach(var c in player2.deck)c.cr = cr;

        player1.Shuffle();
        player2.Shuffle();

        player1.Draw(4);
        player2.Draw(4);

        player1.gm = this;
        player2.gm = this;

        StartPhase(player1,player2);
    }

    public void StartPhase(Player move,Player wait)
    {
        turn = move;
        notrun = wait;
        cr.turn = turn;
        cr.noTurn = notrun;
        currentPhase = PhaseState.Start;
        systemTurn++;
        move.usableMemory = ++move.turn;
        move.usedMemory = 0;
        if(systemTurn != 1)
        {
            move.Draw();
        }
        if(turn.field.Count > 0)
        {
            foreach(var c in turn.field.ToList())
            {
                if(!turn.field.Contains(c))continue;
                c.StartPhase(wait);
                c.isAttacked = 0;
            }
        }
        if(turn.deck.Count > 0)
        {
            foreach(var c in turn.deck)
            {
                if (c.IsFailSafe())
                {
                    move.DoFailSafe(wait,c);
                    WriteLog(LogType.FailSafe,c);
                    break;
                }
            }
        }
        if (IsFinish(move,wait))
        {
            FinishGame();
            return;
        }
        if(turn.field.Count == 0 && systemTurn != 1)
        {
            MainPhase(move,wait);
        }
        WriteLog(LogType.TurnStart);
        currentState = GameState.WaitingForInput;
    }

    public void MainPhase(Player move,Player wait)
    {
        currentPhase = PhaseState.Main;    
    }

    public void EndPhase(Player move,Player wait)
    {
        currentPhase = PhaseState.End;
        if(turn.field.Count > 0)
        {
            foreach(var c in turn.field.ToList())
            {
                if (!turn.field.Contains(c))
                {
                    continue;
                }
                if(c.Type == Card.CardType.Object){
                    c.isFirstTurn = false;
                    c.isCanAttack = true;
                    c.EndPhase(wait);
                }
                else if(c.Type == Card.CardType.Method)
                {
                    List<Card> list = new List<Card>(){c};
                    c.EndPhase(wait);
                    c.player.DestoryField(wait,list);
                }
            }
        }
        if(turn.deck.Count > 0)
        {
            foreach(var c in turn.deck)
            {
                if(c.IsFailSafe())
                {
                    move.DoFailSafe(wait,c);
                    WriteLog(LogType.FailSafe,c);
                    break;
                }
            }
        }
        if (currentScope != null && currentScope.player == move)
        {
            currentScope.EndPhase(wait);
        }
        foreach(Card c in wait.field.ToList())
        {
            if(wait.field.Contains(c) && c.Type == Card.CardType.Object)
            {
                c.OpponentEndPhase(move);
            }
        }
        if (IsFinish(move,wait))
        {
            FinishGame();
            return;
        }
        StartPhase(wait,move);
    }
    public void Surrender(Player surrenderPlayer)
    {
        winner = player1==surrenderPlayer?player2:player1;
        currentState = GameState.Finished;
        OnGameFinished?.Invoke(winner);
    }

    public bool NeedsMarigan(Player p)
    {
        return systemTurn == 1 && Didmarigan.Contains(p);
    }

    public bool ExecuteAction(Player move,Player wait,PlayerAction action)
    {
        if(action == null)return false;
        bool isCorrect = false;
        if(currentState != GameState.WaitingForInput) return false;

        currentState = GameState.Processing;

        switch (currentPhase)
        {
            case PhaseState.Start:
                switch(action.type)
                {
                    case ActionType.SelfGarbage:
                        if (!CheckCorrectPlayer(move, wait) ||
                            (action.targetCard != null &&
                             (action.targetCard.Any(c => c == null || !move.field.Contains(c)) ||
                              action.targetCard.Distinct().Count() != action.targetCard.Count)))
                        {
                            currentState = GameState.WaitingForInput;
                            return false;
                        }
                        move.DestoryField(wait,action.targetCard,true);                    
                        WriteLog(LogType.SelfDestory,action.targetCard);
                        MainPhase(move,wait);
                        isCorrect = true;
                        break;
                    case ActionType.Marigan:
                        if(systemTurn == 1 && Didmarigan.Contains(move)){
                            isCorrect = move.Marigan(action.targetCard);
                            if(!isCorrect){
                                currentState = GameState.WaitingForInput;
                                return isCorrect;
                            }
                            WriteLog(LogType.Marigan,action.targetCard);
                            Didmarigan.Remove(move);
                            if(Didmarigan.Count == 0)
                            {    
                                MainPhase(move,wait);
                            }
                            isCorrect = true;
                        }
                        break;
                }
                break;
            case PhaseState.Main:
                switch (action.type)
                {
                    case ActionType.Attack:
                        if(!CheckCorrectPlayer(move,wait))
                        {
                            currentState = GameState.WaitingForInput;
                            return false;
                        }
                        isCorrect = Attack(move,wait,action);
                        if(isCorrect)
                            WriteLog(LogType.Attack,action.sourceCard,action.targetCard);
                        break;
                
                    case ActionType.Play:
                        if(!CheckCorrectPlayer(move,wait))
                        {
                            currentState = GameState.WaitingForInput;
                            return false;
                        }
                        isCorrect = Play(move,wait,action);
                        if(isCorrect)
                            WriteLog(LogType.PlayCard,action.sourceCard);
                        break;
                
                    case ActionType.End :
                        if(!CheckCorrectPlayer(move,wait))
                        {
                            currentState = GameState.WaitingForInput;
                            return false;
                        }
                        EndPhase(move,wait);
                        isCorrect = true;
                        break;
                }
                break;
        }
        if (IsFinish(move,wait))
        {
            FinishGame();
            decisionTick++;
            return true;
        }
        
        currentState = GameState.WaitingForInput;
        decisionTick++;
        return isCorrect;
        
    }
    private bool IsFinish(Player pl1,Player pl2)
    {
        if(pl1.deck.Count <= 0){
            winner = pl2;
            return true;    
        }
        if(pl2.deck.Count <= 0)
        {
            winner = pl1;
            return true;
        }
        if(pl1.maxMemory <= 0)
        {
            winner = pl2;
            return true;
        }
        if(pl2.maxMemory <= 0)
        {
            winner = pl1;
            return true;
        }
        return false;
    }
    //終了処理
    private void FinishGame()
    {
        if(currentState == GameState.Finished) return;
        currentState = GameState.Finished;
        OnGameFinished?.Invoke(winner);
    }
    public void FinishAsDraw()
    {
        if (currentState == GameState.Finished) return;
        winner = null;
        currentState = GameState.Finished;
        OnGameFinished?.Invoke(null);
    }

    //実体化の処理
    public bool Play(Player move,Player wait,PlayerAction action)
    {

        if(action==null||action.sourceCard == null)return false;
        if(action.sourceCard.player != turn)return false;
        if(turn.field.Contains(action.sourceCard))return false;
        bool ignoreAssert = move.field.Any(c => c is ForcedDebugMode); //変更箇所１//
        //プレイできるかを確認
        if (action.isAddCost)
        {
            if(move.fieldCost + action.sourceCard.Cost + 1 > move.maxMemory || move.usedMemory + action.sourceCard.Cost + 1 > move.usableMemory) return false;
            if(action.sourceCard.isAssert && !ignoreAssert && move.maxMemory > action.sourceCard.Assert) return false;//変更箇所２//
            if(!action.sourceCard.AddCost(wait)) return false;
        }else{
            if(move.fieldCost + action.sourceCard.Cost > move.maxMemory || move.usedMemory + action.sourceCard.Cost > move.usableMemory) return false;
            if(action.sourceCard.isAssert && !ignoreAssert && move.maxMemory > action.sourceCard.Assert) return false;//変更箇所３//
            if(!action.sourceCard.AddCost(wait)) return false;
        }
        if (!ValidateTargets(move,wait,action.sourceCard,action.targetCard))
        {
            return false;
        }
       
        //カードをプレイする。
        //追加コストを払うならコストを1上げて
        //攻撃と体力を+1/+1
        if (action.isAddCost)
        {
            action.sourceCard.isImmediate = true;
            action.sourceCard.Cost += 1;
            action.sourceCard.Attack += 1;
            action.sourceCard.Hp += 1;
            action.sourceCard.ChangeCost += 1;
            action.sourceCard.ChangeAttack += 1;
            action.sourceCard.ChangeHp += 1;
        }
        if (!move.PlayFeild(action.sourceCard))
        {
            if (action.isAddCost)
            {
                action.sourceCard.isImmediate = action.sourceCard.Immediate;
                action.sourceCard.Cost -= 1;
                action.sourceCard.Attack -= 1;
                action.sourceCard.Hp -= 1;
                action.sourceCard.ChangeCost -= 1;
                action.sourceCard.ChangeAttack -= 1;
                action.sourceCard.ChangeHp -= 1;
            }
            return false;
        }
        if(currentScope != null)
        {
            currentScope.ScopeEffectOnPlay(wait,action.sourceCard);
        }
        cr.OnPlay(action.sourceCard);
        action.sourceCard.Constructor(wait,action.targetCard);
        action.sourceCard.OnPlay();
        return true;
    }
    private bool ValidateTargets(
        Player move,
        Player wait,
        Card source,
        List<Card> targets)
    {
        if (!source.select.isSelectConstructor)
        {
            return targets == null || targets.Count == 0;
        }

        if(targets == null || targets.Count == 0)return true;

        if (targets.Count > source.select.numOfSelect ||
            targets.Any(c => c == null)||
            targets.Distinct().Count() != targets.Count)
        {
            return false;
        }

        bool isValidArea;
        switch (source.select.whereTarget)
        {
            case where.hand:
                isValidArea = targets.All(c =>
                    c.player == move && move.hand.Contains(c));
                break;

            case where.selfField:
                isValidArea = targets.All(c =>
                    c.player == move && move.field.Contains(c));
                break;

            case where.enemyField:
                isValidArea = targets.All(c =>
                    c.player == wait && wait.field.Contains(c));
                break;
            default:
                return false;
        }

        return isValidArea && source.ValidateTargets(move, wait, targets);
    }
    //攻撃行動
    public bool Attack(Player move,Player wait,PlayerAction action)
    {
        var source = action.sourceCard;
        if(!move.field.Contains(source))return false;
        if(source.Type != Card.CardType.Object)
        {
            return false;
        }
        if (!source.isCanAttack)
        {
            return false;
        }
        //出たばかりのターンか？
        if (source.isFirstTurn && !source.isImmediate)
        {
            return false;
        }
        //このターンすでに攻撃しているか
        if (source.isAttacked >= source.attackTimes)
        {
            return false;
        }
        //直接攻撃できるか
        bool enemyHasObjects = wait.field.Any(c=>c.Type == Card.CardType.Object);
        if(action.targetCard == null && !enemyHasObjects && !source.isFirstTurn)
        {
            move.DirectAttack(wait,action.sourceCard);
            source.isEncrypted = false;
            source.isAttacked++;
            return true;

        }
        if(action.targetCard == null && !source.isFirstTurn)
        {
            return false;
        }
        if(action.targetCard == null || action.targetCard.Count == 0)return false;
        List<Card> checkProxy = wait.field.FindAll(c=>c.Type == Card.CardType.Object);
        if (action.targetCard == null || action.targetCard.Count != 1)
        {
            return false;
        }
        //攻撃のターゲットは一枚しか取れない
        var target = action.targetCard[0];
        if(!wait.field.Contains(target) || target.Type != Card.CardType.Object)return false;
        
        //プロキシがいるかを確認
        if(target.isProxy == false)
        {
            checkProxy.Remove(target);
            if(!target.isProxy){
                foreach(var c in checkProxy)
                {
                    if (c.isProxy)
                    {           
                        return false;
                    }
                }
            }
        }
        
        //ターゲットが暗号化されているか
        if (target.isEncrypted)
        {
            return false;
        }

        int sourceAtk = source.Attack;
        int targetAtk = target.Attack;

        //能力の処理
        if(currentScope != null)
        {
            currentScope.ScopeEffectOnAttack(wait,action.targetCard);
        }
        cr.OnAttack(source,target);
        source.OnAttack(wait,target);
        if(target.Hp <= 0 || source.Hp <= 0)
        {
            if(target.Hp <= 0)
            {
                move.DestoryField(wait, new List<Card> { target });
            }
            if(source.Hp <= 0)
            {
                List<Card> sourceL = new List<Card>{action.sourceCard};
                wait.DestoryField(move,sourceL);
            }
            source.isEncrypted = false;
            source.isAttacked++;
            return true;
        }
        //HPの増減処理
        if(!target.isSandBox){
            target.Hp -= sourceAtk;
            target.ChangeHp -= sourceAtk;
        }
        if(!source.isSandBox){
            source.Hp -= targetAtk;
            source.ChangeHp -= targetAtk;
        }
        target.isSandBox = false;
        source.isSandBox = false;
        //オブジェクトの解放処理
        if(target.Hp <= 0 || source.isSegfault)
        {
            move.DestoryField(wait, new List<Card> { target });
        }
        if(source.Hp <= 0 || target.isSegfault)
        {
            List<Card> sourceL = new List<Card>{action.sourceCard};
            wait.DestoryField(move,sourceL);
        }
        source.isEncrypted = false;
        source.isAttacked++;
        return true;
    }
    public BoardData GetBoardData(Player player)
    {
        Player enemyPlayer = (player == player1) ? player2 : player1;

        BoardData board = new BoardData();
        
        board.selfField = Card.PackingCard(player.field);
        board.selfHand = Card.PackingCard(player.hand);
        board.selfGarbage = Card.PackingCard(player.garbage);

        board.enemyField = Card.PackingCard(enemyPlayer.field);
        board.enemyGarbage = Card.PackingCard(enemyPlayer.garbage);

        return board;
    }
    private bool CheckCorrectPlayer(Player move,Player wait)
    {
        if(move != turn)return false;
        if(wait != notrun)return false;
        if(move != player1&& move != player2)return false;
        if(wait != player1&& wait != player2)return false;
        if(wait == move)return false;
        return true;
    }
    public void WriteLog(LogType type,Card? ccard=null,List<Card>? ccards=null,int actionValue = 0)
    {
        int currentTurn = (systemTurn + 1) / 2;
        bool isP1 = (turn == player1);
        CardData? sourceData = null;
        CardData[] targetData = null;
        if(ccard !=null)
            sourceData = Card.PackingCard(ccard);
        if(ccards != null)
            targetData = Card.PackingCard(ccards).ToArray();

        // 内部に保存
        playLog.AddLog(currentTurn, isP1, type, sourceData, targetData, actionValue);
    }
    public void WriteLog(LogType type,List<Card> ccards)
    {
        int currentTurn = (systemTurn + 1) / 2;
        bool isP1 = (turn == player1);

        CardData[] targetData = Card.PackingCard(ccards).ToArray();

        // 内部に保存
        playLog.AddLog(currentTurn, isP1, type, targetData);
    }
    public Card FindCardByUniqueId(int uniqueId)
    {
        if (uniqueId < 0) return null;

        Player[] players = new Player[] { this.turn, this.notrun };

        foreach (Player p in players)
        {
            if (p == null) continue;

            // フィールド
            foreach (Card c in p.field) { if (c != null && c.uniqueId == uniqueId) return c; }
            // 手札
            foreach (Card c in p.hand) { if (c != null && c.uniqueId == uniqueId) return c; }
            // 墓地
            foreach (Card c in p.garbage) { if (c != null && c.uniqueId == uniqueId) return c; }
            // デッキ
            foreach (Card c in p.deck) { if (c != null && c.uniqueId == uniqueId) return c; }
        }

        if (this.currentScope != null && this.currentScope.uniqueId == uniqueId)
        {
            return this.currentScope;
        }

        return null; // 見つからなかった場合
    }
}
