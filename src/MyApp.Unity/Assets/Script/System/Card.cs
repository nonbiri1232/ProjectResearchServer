using System;
using System.Collections.Generic;
using System.Xml;
public enum where
{
    None,
    hand,
    selfField,
    enemyField
}
public class Select
{
    public bool isSelectConstructor{get;protected set;}
    public where whereTarget{get;protected set;}
    public int numOfSelect{get;protected set;}
    public Select()
    {
        isSelectConstructor = false;
        whereTarget = where.None;
    }
    public Select(where tar,int num)
    {
        isSelectConstructor = true;
        whereTarget = tar;
        numOfSelect = num;
    }

}

public class Card
{
    public enum CardType{Object,Method,Scope}

    public Player player;
    public Crest cr;
    protected static int NowUniqueId = 0;
    public int uniqueId{get;private set;}
    public bool isCanAttack{get;set;} = true;
    public bool isFirstTurn{get;set;} = true;
    public bool Proxy{get;set;}
    public bool Daemon{get;set;}
    public bool SandBox{get;set;}
    public bool Segfault{get;set;}
    public bool Encrypted{get;set;}
    public bool Immediate{get;set;}
    public bool isProxy{get;set;}
    public bool isDaemon{get;set;}
    public bool isSandBox{get;set;}
    public bool isSegfault{get;set;}
    public bool isEncrypted{get;set;}
    public bool isImmediate{get;set;}
    public bool wasPlayedFromGarbage{get;set;}
    public int attackTimes{get;protected set;} = 1;
    public bool isAssert{get;protected set;}
    public int Assert{get;protected set;}
    public int isAttacked{get;set;}
    public Select select;
    public CardType Type {get;protected set;}
    public int ChangeCost = 0;
    public int Cost{get; set;}
    public int ChangeAttack = 0;
    public int Attack{get; set;}
    public int ChangeHp = 0;
    public int Hp{get;set;}
    private static readonly Random rand = new Random();
    public void OnPlay()
    {
        Daemon = isDaemon;
        Encrypted = isEncrypted;
        Immediate = isImmediate;
        Proxy = isProxy;
        SandBox = isSandBox;
        Segfault = isSegfault;
    }
    public virtual bool AddCost(Player Enemy,List<Card> target = null){return true;}
    public virtual bool ValidateTargets(Player move, Player enemy, List<Card> targets){return true;}
    public virtual void Constructor(Player Enemy,List<Card> target = null){}
    public virtual void Destructor(Player Enemy,List<Card> target = null){}
    public virtual bool IsFailSafe(){return false;}
    public virtual void FailSafe(Player Enemy,List<Card> target = null){}
    public virtual void OnTurnStart(Player Enemy,List<Card> target = null){}
    public virtual void OnTurnEnd(Player Enemy,List<Card> target = null){}
    public virtual void OnAttack(Player Enemy,Card target = null){}
    public virtual void StartPhase(Player Enemy){}
    public virtual void EndPhase(Player Enemy){}
    public virtual void OpponentEndPhase(Player Enemy){}
    public virtual void ScopeEffectOnAttack(Player pl,List<Card> target = null){}
    public virtual void ScopeEffectOnPlay(Player pl,Card target = null){}
    public virtual void CrestOnAttack(Player Enemy,List<Card> target = null){}
    public virtual void CrestOnPlay(Player Enemy,Card target = null){}

    private readonly List<Action<Player, List<Card>>> additionalDestructorEffects = new List<Action<Player, List<Card>>>();

    public Card()
    {
        uniqueId = NowUniqueId++;
    }
    public void AddDestructorEffect(Action<Player, List<Card>> effect)
    {
        if (effect != null)
        {
            additionalDestructorEffects.Add(effect);
        }
    }

    public void ExecuteDestructor(Player enemy, List<Card> target = null)
    {
        Destructor(enemy, target);

        // 発動中にリストが変更されても問題が起きないようコピーする
        Action<Player, List<Card>>[] effects = additionalDestructorEffects.ToArray();

        // 墓地から復活したときに感染効果を残さない
        additionalDestructorEffects.Clear();

        foreach (Action<Player, List<Card>> effect in effects)
        {
            effect(enemy, target);
        }
    }
    protected Card RandomSelect(List<Card> target)
    {
        if(target == null || target.Count == 0)return null;
        int size = target.Count;
        int rnd = rand.Next(0,size);
        return target[rnd];
    }

    public void SettingBasicCard(int i)
    {
        Cost = i;
        Attack = i;
        Hp = i;
        Type = CardType.Object;
        select = new Select();
    }

    public static string GetCardClassName(int cardId)
    {
        switch (cardId)
        {
            case 0: return "SledOverClock";
            case 1: return "IncrementProcess";
            case 2: return "ClockDownBot";
            case 3: return "ParallelCompilation";
            case 4:return "PoisonPoint";
            case 5: return "UnSafeArea";
            case 6: return "Master";
            case 7: return "Raid10";
            case 8: return "RmRf";
            case 9: return "Paging";
            case 10: return "BackGroundMiner";
            case 11: return "SystemFreeze";
            case 12: return "CarnelPanicZero";
            case 13: return "AllDelete";
            case 14: return "SafeModeOverdrive";
            case 15: return "ForcedCrashTest";
            case 16: return "IllegalResourceSale";
            case 17: return "ForcedDebugMode";
            case 18: return "RansomwareInfection";
            case 19: return "LeechProcess";
            case 20: return "DDoSArea";
            case 21: return "MultiEncryptionProtocol";
            case 22: return "TimedLogicBomb";
            case 23: return "TrojanHorse";
            case 24: return "MemoryDumpRestore";
            case 25: return "CoreDumpProcess";
            case 26: return "ZombieProcess";
            case 27: return "DeepArchive";
            case 28: return "RestoreMeister";
            case 29: return "FakeHoneypot";
            case 30: return "PingBot";
            case 31: return "Firewall";
            case 32: return "DebugProcess";
            case 33: return "BackupServer";
            case 34: return "GarbageShredder";
            case 35: return "Antivirus";
            case 36: return "Mainframe";
            case 37: return "DataFetch";
            case 38: return "ProcessKill";
            case 39: return "EmergencyEvasion";
            case 40: return "Override";
            case 41: return "CacheClear";
            case 42: return "Format";
            case 43: return "ApplyPatch";
            case 44: return "EmergencyPower";
            case 45: return "ForgedFile";
            default:
                return null;
        }
    }
    public static int GetCardId(string className)
    {
        switch (className)
        {
            case "SledOverClock": return 0;
            case "IncrementProcess": return 1;
            case "ClockDownBot": return 2;
            case "ParallelCompilation": return 3;
            case "PoisonPoint": return 4;
            case "UnSafeArea": return 5;
            case "Master": return 6;
            case "Raid10": return 7;
            case "RmRf": return 8;
            case "Paging": return 9;
            case "BackGroundMiner": return 10;
            case "SystemFreeze": return 11;
            case "CarnelPanicZero": return 12;
            case "AllDelete": return 13;
            case "SafeModeOverdrive": return 14;
            case "ForcedCrashTest": return 15;
            case "IllegalResourceSale": return 16;
            case "ForcedDebugMode": return 17;
            case "RansomwareInfection": return 18;
            case "LeechProcess": return 19;
            case "DDoSArea": return 20;
            case "MultiEncryptionProtocol": return 21;
            case "TimedLogicBomb": return 22;
            case "TrojanHorse": return 23;
            case "MemoryDumpRestore": return 24;
            case "CoreDumpProcess": return 25;
            case "ZombieProcess": return 26;
            case "DeepArchive": return 27;
            case "RestoreMeister": return 28;
            case "FakeHoneypot": return 29;
            case "PingBot": return 30;
            case "Firewall": return 31;
            case "DebugProcess": return 32;
            case "BackupServer": return 33;
            case "GarbageShredder": return 34;
            case "Antivirus": return 35;
            case "Mainframe": return 36;
            case "DataFetch": return 37;
            case "ProcessKill": return 38;
            case "EmergencyEvasion": return 39;
            case "Override": return 40;
            case "CacheClear": return 41;
            case "Format": return 42;
            case "ApplyPatch": return 43;
            case "EmergencyPower": return 44;
            case "ForgedFile": return 45;
            default:
                return -1;
        }
    }
    public static int GetCardId(Card c)
    {
        if(c == null)return -1;
        string className = c.GetType().Name;
        switch (className)
        {
            case "SledOverClock": return 0;
            case "IncrementProcess": return 1;
            case "ClockDownBot": return 2;
            case "ParallelCompilation": return 3;
            case "PoisonPoint": return 4;
            case "UnSafeArea": return 5;
            case "Master": return 6;
            case "Raid10": return 7;
            case "RmRf": return 8;
            case "Paging": return 9;
            case "BackGroundMiner": return 10;
            case "SystemFreeze": return 11;
            case "CarnelPanicZero": return 12;
            case "AllDelete": return 13;
            case "SafeModeOverdrive": return 14;
            case "ForcedCrashTest": return 15;
            case "IllegalResourceSale": return 16;
            case "ForcedDebugMode": return 17;
            case "RansomwareInfection": return 18;
            case "LeechProcess": return 19;
            case "DDoSArea": return 20;
            case "MultiEncryptionProtocol": return 21;
            case "TimedLogicBomb": return 22;
            case "TrojanHorse": return 23;
            case "MemoryDumpRestore": return 24;
            case "CoreDumpProcess": return 25;
            case "ZombieProcess": return 26;
            case "DeepArchive": return 27;
            case "RestoreMeister": return 28;
            case "FakeHoneypot": return 29;
            case "PingBot": return 30;
            case "Firewall": return 31;
            case "DebugProcess": return 32;
            case "BackupServer": return 33;
            case "GarbageShredder": return 34;
            case "Antivirus": return 35;
            case "Mainframe": return 36;
            case "DataFetch": return 37;
            case "ProcessKill": return 38;
            case "EmergencyEvasion": return 39;
            case "Override": return 40;
            case "CacheClear": return 41;
            case "Format": return 42;
            case "ApplyPatch": return 43;
            case "EmergencyPower": return 44;
            case "ForgedFile": return 45;
            default:
                return -1;
        }
    }
    public static Card CreateCardInstance(string className)
    {
        return CreateCardInstance(GetCardId(className));
    }
    public static Card CreateCardInstance(int cardId)
    {
        switch (cardId)
        {
            case 0: return new SledOverClock();
            case 1: return new IncrementProcess();
            case 2: return new ClockDownBot();
            case 3: return new ParallelCompilation();
            case 4:return new PoisonPoint();
            case 5: return new UnSafeArea();
            case 6: return new Master();
            case 7: return new Raid10();
            case 8: return new RmRf();
            case 9: return new Paging();
            case 10: return new BackGroundMiner();
            case 11: return new SystemFreeze();
            case 12: return new CarnelPanicZero();
            case 13: return new AllDelete();
            case 14: return new SafeModeOverdrive();
            case 15: return new ForcedCrashTest();
            case 16: return new IllegalResourceSale();
            case 17: return new ForcedDebugMode();
            case 18: return new RansomwareInfection();
            case 19: return new LeechProcess();
            case 20: return new DDoSArea();
            case 21: return new MultiEncryptionProtocol();
            case 22: return new TimedLogicBomb();
            case 23: return new TrojanHorse();
            case 24: return new MemoryDumpRestore();
            case 25: return new CoreDumpProcess();
            case 26: return new ZombieProcess();
            case 27: return new DeepArchive();
            case 28: return new RestoreMeister();
            case 29: return new FakeHoneypot();
            case 30: return new PingBot();
            case 31: return new Firewall();
            case 32: return new DebugProcess();
            case 33: return new BackupServer();
            case 34: return new GarbageShredder();
            case 35: return new Antivirus();
            case 36: return new Mainframe();
            case 37: return new DataFetch();
            case 38: return new ProcessKill();
            case 39: return new EmergencyEvasion();
            case 40: return new Override();
            case 41: return new CacheClear();
            case 42: return new Format();
            case 43: return new ApplyPatch();
            case 44: return new EmergencyPower();
            case 45: return new ForgedFile();
            default:
                return null;
        }
    }
    public static Card CreateCardInstance(CardData card)
    {
        Card c = Card.CreateCardInstance(card.id);
        c.Attack = card.atk;
        c.Hp = card.hp;
        c.Cost = card.cost;
        c.isCanAttack = card.canAttackNow;

        return c;
    }
    public static List<CardData> PackingCard(List<Card> cards)
    {
        List<CardData> cardDatas = new List<CardData>();
        foreach(var c in cards)
        {
            CardData cardData = new CardData();
            cardData.id = GetCardId(c);
            cardData.atk = c.Attack;
            cardData.hp = c.Hp;
            cardData.cost = c.Cost;
            cardData.canAttackNow = (c != null &&
               c.Type == Card.CardType.Object &&
               c.player == c.player.gm.turn &&
               c.player.gm.currentPhase == PhaseState.Main &&
               c.player.field.Contains(c) &&
               c.isCanAttack &&
               (!c.isFirstTurn || c.isImmediate) &&
               c.isAttacked < c.attackTimes);
            cardData.uniqueId = c.uniqueId;
            cardData.type = (c.Type == CardType.Object?1:c.Type == CardType.Method?2:3);

            cardDatas.Add(cardData);
        }
        return cardDatas;
    }
}
