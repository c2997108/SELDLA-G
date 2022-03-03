# SELDLA-G

このツールは、de novoゲノム構築時に使用する連鎖解析ツールであるhttps://github.com/c2997108/SELDLA
で出力されたファイルを使って、Hi-Cのコンタクトマップ風に操作してエラーなどを除去しながら最終的なゲノム構築を行うツールです。

## インストール

https://github.com/c2997108/SELDLA-G/releases/download/v0.8.2/SELDLA-G_v0.8.2.zip
からダウンロードして、解凍する。SELDLA-Gをダブルクリックすれば実行できます。

## 動作環境

### Windows 10 or 11
- Nvidia GT710以降のGPUではGPUが使えるのを確認済み。AMDやINDELの新しいGPUなら大丈夫かも。未確認。
- 連鎖解析モードはWindowsでGPUが使える環境を推奨。

### Mac
- GPUは使えないのではないかなと思うけど、CPUでは動作を確認できた。

### Linux
- 一応CentOS7でも起動して、NvidiaのドライバーがあればGPUも使ってコンタクトマップ表示までは正常だけど、キー入力が変。

## 開発環境のセットアップ

- .NET Core 3.1 Runtime (v3.1)
- Visual Studio 2022 (.NET 6)

## 使用方法

### 連鎖解析モード

```
.\SELDLA-G.exe linkage
```

### Hi-Cモード

```
.\SELDLA-G.exe hic
```

<img width="618" alt="image" src="https://user-images.githubusercontent.com/5350508/156413938-06fed85e-5c3f-42c6-b348-007be7cfcd54.png">

下記のキーを押して、読み込み、編集、保存を行う。

- O : ファイルを開く。連鎖解析モードでは、ファイルはSELDLAで処理した結果ファイル(`XXX_chain.ld2imp.all.txt`)、もしくはSELDLA-Gで途中保存したファイル(`XXX.phase.txt`)。Hi-Cモードでは、SALSAの`scaffolds_FINAL.agp`, `alignment_iteration_1.bed`を指定する。
- I : (Hi-C専用)保存したファイルを読み込んで再度表示する。`XXX.agp`, `XXX.matrix`
- P : 連鎖解析モードでは、SELDLAでコンティグのFASTAをミスアセンブル箇所で分断してタブ区切りテキストにしたファイル(`XXX_split_seq.txt`)、Hi-CモードではSALSAで分断したコンティグのFASTA(`assembly.cleaned.fasta`)を読み込んで、最終的な伸長後のFASTAファイルなどを出力する。ターミナルのほうにFASTA.TABファイル、FASTAファイル名を入力する必要あり。
- R : 基本的に本ツールのコンタクトマップは、現在マウスカーソルがいる場所のX, Y座標のマーカーの位置を起点に操作を行う。Rを押すと、上側の青色マーカーの染色体内の並びを反転させる。
- T : 2つの青色マーカー間の染色体を入れ替える
- Y : 上側の緑色のマーカーのコンティグ内の並びを反転させる。染色体を超えて範囲選択しても動くけど、色々とおかしなことになるので同一染色体内のコンティグを選択すること。
- U : 2つの緑色マーカー間のコンティグを入れ替える染色体を超えて範囲選択しても動くけど、色々とおかしなことになるので同一染色体内のコンティグを選択すること。
- F : 選択した範囲の染色体の向きを入れ替える。1回押すと上側の青色が開始箇所として選択され、2回押した時点で範囲を確定して反転させる。
- G : 選択した範囲間の染色体を入れ替える。1～4回押して入れ替える範囲を確定させる。
- N : 選択した範囲のコンティグの向きを入れ替える。1回押すと上側の緑色が開始箇所として選択され、2回押した時点で範囲を確定して反転させる。染色体を超えて範囲選択しても動くけど、色々とおかしなことになるので同一染色体内のコンティグを選択すること。
- M : 選択した範囲間のコンティグを入れ替える。1～4回押して入れ替える範囲を確定させる。染色体を超えて範囲選択しても動くけど、色々とおかしなことになるので同一染色体内のコンティグを選択すること。
- D : 上側の緑色マーカーのコンティグを削除する。
- W : 選択した範囲の染色体が属する染色体名を変更する。染色体が入れ子になるような変更をすると変な挙動になる。
- E : 選択した範囲のコンティグが属する染色体名を変更する。変更するコンティグは染色体の端にあるコンティグを含む状態で行うこと。染色体が入れ子になるような変更をすると変な挙動になる。
- Esc : エスケープキーを押すと、F, G, N, M, W, Eキーでのマーカー選択をキャンセル出来る。
- S : 編集したファイルを保存する。ターミナルのほうにファイル名を入力する必要あり。連鎖解析モードなら再度「O」キー、Hi-Cモードなら「I」キーを押して保存したファイルから編集を再開できる。ヒートマップも保存する。
- Z : 一度だけ直前の操作を取り消す。
