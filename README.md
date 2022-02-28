# SELDLA-G

このツールは、de novoゲノム構築時に使用する連鎖解析ツールであるhttps://github.com/c2997108/SELDLA
で出力されたファイルを使って、Hi-Cのコンタクトマップ風に操作してエラーなどを除去しながら最終的なゲノム構築を行うツールです。

## インストール

https://github.com/c2997108/SELDLA-G/releases/download/v0.6.2/SELDLA-G_v0.6.2.zip
からダウンロードして、解凍する。SELDLA-Gをダブルクリックすれば実行できます。

## 動作環境

- Windows 10 or 11
- Nvidia GT710以降のGPUでは確認済み (AMDやINDELの新しいGPUなら大丈夫かも。未確認。）

一応Linuxでも動作したけど、Macのarm版は今のところVisual Studioから出力できないのでM1 Macは対応できそうにない。

## 開発環境のセットアップ

- .NET Core 3.1 Runtime (v3.1)
- Visual Studio 2022 (.NET 6)

## 使用方法

<img width="805" alt="image" src="https://user-images.githubusercontent.com/5350508/155942153-7c9ea304-7637-4c71-8136-f5d307e3d25b.png">

下記のキーを押して、読み込み、編集、保存を行う。

- O : ファイルを開く。ファイルはSELDLAで処理した結果ファイル。
- R : 基本的に本ツールのコンタクトマップは、現在マウスカーソルがいる場所のX, Y座標のマーカーの位置を起点に操作を行う。Rを押すと、上側の黄色マーカーの染色体内の並びを反転させる。
- T : 2つの黄色マーカー間の染色体を入れ替える
- Y : 上側の緑色のマーカーのコンティグ内の並びを反転させる。
- U : 2つの緑色マーカー間のコンティグを入れ替える
- N : 選択した範囲のコンティグを入れ替える。1回押すと開始箇所を指定し、2回押した時点で範囲を確定して反転させる。染色体を超えて範囲選択しても動くけど、色々とおかしなことになるので同一染色体内のコンティグを選択すること。
- M : 選択した範囲間のコンティグを入れ替える。1～4回押して入れ替える範囲を確定させる。
- D : 上側の緑色マーカーのコンティグを削除する。
- E : 選択した範囲のコンティグが属する染色体名を変更する。変更するコンティグは染色体の端にあるコンティグを含む状態で行うこと。染色体が入れ子になるような変更をすると変な挙動になる。
- S : 編集したファイルを保存する。ターミナルのほうにファイル名を入力する必要あり。再度「O」キーを押して保存したファイルから編集を再開できる。ヒートマップも保存する。
- P : コンティグのFASTAをタブ区切りテキストにしたファイルを読み込んで、最終的な伸長後のFASTAファイルなどを出力する。ターミナルのほうにタブ区切りのコンティグ配列ファイル名を入力する必要あり。
