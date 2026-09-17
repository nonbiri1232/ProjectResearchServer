# 教師AI → Self-play（合計1000万ステップ）

現在の `MemoryEfficiencyExplore_3M` と報酬、5デッキ、PPOネットワーク、
探索設定を揃え、学習序盤に固定ルールベース教師を使う効果を比較する。

## Phase 1: 教師AIウォームアップ（200万ステップ）

PowerShellで以下を実行し、Trainerが待機したらUnityの `AILearning` シーンを再生する。

```powershell
cd C:\UnityProjects\ProjectResearch
& "C:\UnityProjects\.venvs\ProjectResearch-mlagents\Scripts\Activate.ps1"
mlagents-learn .\ml-agents-configs\teacher_curriculum_warmup_2m_config.yaml --run-id=TeacherWarmup_2M
```

完了後、Unityの再生を停止する。

## Phase 2: Self-play（800万ステップ）

`--resume` ではなく `--initialize-from` を使う。教師AIで得た重みを読み込み、
新しいRunとしてステップを0へ戻してSelf-playを800万ステップ行う。

```powershell
mlagents-learn .\ml-agents-configs\teacher_curriculum_selfplay_8m_config.yaml --run-id=TeacherCurriculum_10M --initialize-from=TeacherWarmup_2M
```

Trainerが待機したら同じ `AILearning` シーンを再生する。

## 出力

- Phase 1: `results/TeacherWarmup_2M/`
- 最終モデル: `results/TeacherCurriculum_10M/CardGame.onnx`
- Phase 2の表示ステップは800万だが、学習経験の合計は200万＋800万＝1000万。
- Phase 2のELOは新しいRun内の相対値なので、既存RunのELOと直接比較しない。
