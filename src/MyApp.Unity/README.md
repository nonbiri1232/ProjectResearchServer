# ALLO SEIZE - Trading Card Game

ハッキングとプログラミングをテーマにした本格デジタルカードゲーム

## 🛠️ 開発環境
**※開発を始める前に必ず確認！**
* **Unity Version:** `6000.4.0f1` (※必ずこのバージョンをUnity Hubからインストールして合わせてください)
* **C# / 開発エディタ:** Visual Studio または VS Code (お好みで)

## 🚀 環境構築・起動方法 (Setup)
1. UnityHubからこのプロジェクトをクローンする。
2. バージョンが指定のものになっているか確認して開く。
3. `Assets/Scenes/Game` を開き、Playボタンを押してエラーが出ないか確認する。

---

## 🤝 開発ルール

### 1. Gitの運用ルール
* いきなり `main` ブランチにプッシュしないこと！
* 作業を始める時は、必ず `main` から自分の作業用ブランチを切る。（例: `git switch -c feature/card-effects` など）
* 作業が終わったら、GitHub上でPull Request (PR) を作成し、お互いに確認してから `main` にマージする。

### 2. Unity特有の注意点（超重要🚨）
* **シーン（Scene）ファイルの同時編集は絶対NG！**
  * 複数人で同じシーンを同時に編集すると、Gitで取り返しのつかないバグが発生します。
  * `GameVisual` のUIなどをいじる時は、「今からメインシーン触るね！」と[LINE/Discordなど]で必ず声をかけること。
  * システムのテストをする時は、自分用のテストシーン（`Test_〇〇`）を作ってそこで動作確認をするのがオススメ。

### 3. プログラムの設計方針
* ゲームのルール（`GameManager`, `Player`, `Card`）と、画面の描画（`GameVisual`）は完全に分けて（疎結合で）設計しています。
* 新しいカードを追加する場合は、`Assets/Scripts/AllCard.cs` にクラスを追加するだけでOK！UI側のコードをいじる必要はありません。
* AIの作成では`GameManager.ExecuteAction`に対して行動リクエストを出してください。

---

## クラス構成
本作のソースコードにおける各コアクラスの主要なインスタンス変数とメソッドの仕様です。共同開発での機能拡張やデバッグの際の変数確認に活用してください。

### 🃏 Card クラス
すべてのカードの基底となるクラス。固有の能力を持つカードを作成する際は、このクラスを継承して各バーチャルメソッドをオーバーライドします。

#### 🔹 インスタンス変数
* **基本ステータス**
  * `player` (`Player`): このカードの所有プレイヤー。
  * `cr` (`Crest`): 適用されている常在効果（Crest）への参照。
  * `Cost` (`int`): カードの現コスト。
  * `Attack` (`int`): カードの現攻撃力（Objectのみ）。
  * `Hp` (`int`): カードの現体力（Objectのみ）。
  * `Type` (`CardType`): カードの種類（`Object`, `Method`, `Scope`）。
  * `select` (`Select`): カードが要求するターゲット選択の定義。
* **状態・制限フラグ**
  * `isFirstTurn` (`bool`): 召喚酔いフラグ（出たばかりのターンは `true`）。
  * `isCanAttack` (`bool`): 攻撃可能フラグ（FailSafe等の効果で操作可能）。
  * `attackTimes` (`int`): 1ターンに攻撃できる最大回数。
  * `isAttacked` (`int`): このターンに攻撃した回数。
* **一時ステータス修正値**
  * `ChangeCost` / `ChangeAttack` / `ChangeHp` (`int`): バフ・デバフ等による一時的な変動値。(バフ、デバフによるカードのステータスの増減があった場合はこれも同量の値を増減させてください)
* **特殊能力フラグ（本来の性質 / 現在の状態）**
  * `Proxy` / `isProxy` : 挑発（相手の攻撃を強制誘導する）。
  * `Daemon` / `isDaemon` : 破壊時にメモリが減少しない特殊ユニット。
  * `SandBox` / `isSandBox` : ダメージを受けない無敵状態。
  * `Segfault` / `isSegfault` : 自身を破壊した対象、または自身が戦闘した対象を道連れにする。
  * `Encrypted` / `isEncrypted` : 相手の攻撃対象に選ばれない暗号化状態。
  * `Immediate` / `isImmediate` : 出たターンにすぐ攻撃できる即効状態。

#### 🔹 主要メソッド
* `OnPlay()`: カードがプレイされた際、各特殊能力フラグの初期値を現在の状態に同期させます。(これらをオーバーライドしてカード能力を実装してください)
* `Constructor(Player Enemy, List<Card> target)`: **[要オーバーライド]** カードが場に出た時（ファンファーレ効果）の処理。
* `Destructor(Player Enemy, List<Card> target)`: **[要オーバーライド]** カードが破壊された時（ラストワード効果）の処理。
* `IsFailSafe()`: **[要オーバーライド]** デッキ内でフェイルセーフが発動する条件を満たしているか判定（`true`/`false`）。
* `FailSafe(Player Enemy, List<Card> target)`: **[要オーバーライド]** フェイルセーフ発動時の固有効果の処理。
* `StartPhase(Player Enemy)` / `EndPhase(Player Enemy)`: ターン開始時・終了時に誘発する効果。
* `OnAttack(Player Enemy, Card target)`: 自身が攻撃を仕掛けた時に誘発する効果。
* `ScopeEffectOnPlay(Player pl, Card target)` / `ScopeEffectOnAttack(Player pl, List<Card> target)`: Scopeカードが持つ環境持続効果。
* `CrestOnPlay(Player Enemy, Card target)` / `CrestOnAttack(Player Enemy, List<Card> target)`: カードが持つ常在効果。

---

### 👤 Player クラス
プレイヤーの各種リソース（メモリ）や、手札・デッキ・フィールド・墓地などのカード領域を管理します。

#### 🔹 インスタンス変数
* `turn` (`int`): 先攻・後攻やターン数の識別用。
* `maxMemory` (`int`): 最大メモリ（初期値20）。直接攻撃を受けると減少、決めると増加します。
* `usableMemory` (`int`): このターンに現在使用可能な残りメモリ。
* `fieldCost` (`int`): 自分の場に存在するカードの合計コスト。
* `usedMemory` (`int`): 使用済みのメモリ量。
* **カード領域（リスト）**
  * `hand` (`List<Card>`): 手札。上限は8枚。
  * `deck` (`List<Card>`): 山札（デッキ）。
  * `field` (`List<Card>`): フィールド（戦場）。
  * `garbage` (`List<Card>`): 墓地。
* `gm` (`GameManager`): 進行管理を行っているGameManagerへの参照。

#### 🔹 主要メソッド
* `DirectAttack(Player enemy, Card attacker)`: 相手プレイヤーへの直接攻撃。自身の `maxMemory` を攻撃力分増やし、相手の `maxMemory` を同値分減少させます。
* `Shuffle()`: デッキ（`deck`）の順序をランダムにシャッフルします。
* `DrawG()`: 墓地（`garbage`）からランダムに1枚カードを選び、手札に加えます。手札が8枚以上の場合は墓地に残ります。
* `DestoryField(Player enemy, List<Card> target, bool isStartPhase)`: フィールドのカードを破壊する一括処理。カードの種類やフラグに応じて `garbage` への移動、最大メモリの変動、ステータスの初期化リセット、`Destructor()` の呼び出しを安全に行います。(カードの解放には原則にこれを用いてください)
* `DoFailSafe(Player enemy, Card c)`: 条件を満たしたカード `c` のコストを0にしてデッキから直接フィールドへ展開し、フェイルセーフ効果とコンストラクタを起動します。

---

### ⚙️ GameManager クラス
ゲームの全体進行（フェイズの遷移、勝敗判定、プレイヤーからのアクション処理）を統括するコアエンジンです。

#### 🔹 主要メソッド
* `ExecuteAction(Player move, Player wait, PlayerAction action)`: UIやAIから送られてきたアクションを受領し、現在のゲーム状態やフェイズ（`Start`, `Main` 等）に応じて適切な処理（Play、Attack、End、SelfGarbage）へ振り分けます。
* `Play(Player move, Player wait, PlayerAction action)`: 手札からカードをフィールドに展開します。コスト支払い能力のチェック、手札からの削除、フィールドへの追加、各種 `Constructor` の発動を一連の流れで行います。
* `Attack(Player move, Player wait, PlayerAction action)`: フィールドのオブジェクトによる攻撃を処理します。召喚酔いチェック、攻撃回数制限、暗号化（`isEncrypted`）の解除、プロキシ（`isProxy`）の存在チェック、戦闘ダメージ計算、死亡判定（`DestoryField`）を厳密に行います。
* `StartPhase(Player move, Player wait)` / `EndPhase(Player move, Player wait)`: 各フェイズ開始・終了時のシステム処理。スタートフェイズ時にはデッキ内の全カードの `IsFailSafe()` をスキャンします。

---

### 🛡️ Crest クラス
フィールドに存在するオブジェクトが展開する「常在型バフ・デバフ（紋章効果）」の割り込み処理を管理します。

#### 🔹 インスタンス変数
* `turn` / `noTurn` (`Player`): 現在のターンプレイヤーと待機プレイヤー。
* `effectOnPlay` (`List<Card>`): カードがプレイされた時に割り込んで効果を発揮する常在カードのリスト。
* `effectOnAttack` (`List<Card>`): 攻撃が発生した時に割り込んで効果を発揮する常在カードのリスト。

#### 🔹 主要メソッド
* `OnPlay(Card play)`: 登録されているすべての常在カードの `CrestOnPlay` を実行します。
* `OnAttack(Card source, Card target)`: 登録されているすべての常在カードの `CrestOnAttack` を実行します。

---

### ✉️ PlayerAction クラス
プレイヤーが画面上で行った操作の情報を格納し、GameManagerへ伝達するための「注文書（データパッケージ）」です。

#### 🔹 インスタンス変数
* `type` (`ActionType`): アクションの種類（`Play`, `Attack`, `SelfGarbage`, `End`）。
* `sourceCard` (`Card`): アクションの主体となるカード（プレイする手札、攻撃する場のカードなど）。
* `targetCard` (`List<Card>`): アクションの対象となったカードのリスト（効果の対象、攻撃対象など）。
* `isAddCost` (`bool`): プレイ時、追加コストを支払う選択をしたかどうかのフラグ。

* その他わからないことがあれば聞いてください。
