# SELDLA-G

このツールは、de novoゲノム構築時に使用する連鎖解析ツールであるhttps://github.com/c2997108/SELDLA
で出力されたファイルを使って、Hi-Cのコンタクトマップ風に操作してエラーなどを除去しながら最終的なゲノム構築を行うツールです。

## インストール

https://github.com/c2997108/SELDLA-G/releases/download/v0.8.4/SELDLA-G_v0.8.4.zip
からダウンロードして、解凍する。SELDLA-Gをダブルクリックすれば実行できます。

## 動作環境

### Windows 10 or 11
- Nvidia GT710以降、AMD RX6000台のGPUではGPUが使えるのを確認済み。INDELの新しい統合GPUなら大丈夫かも、未確認。
- 連鎖解析モードはWindowsでGPUが使える環境を推奨。

### Mac
- GPUは使えないのではないかなと思うけど、CPUでは動作を確認できた。

### Linux
- 一応CentOS7でも起動して、NvidiaのドライバーがあればGPUも使えた。ただ、コンタクトマップ表示がマーカー数2000程度以上では表示されないみたい。

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
- P : 連鎖解析モードでは、SELDLAでコンティグのFASTAをミスアセンブル箇所で分断してタブ区切りテキストにしたファイル(`XXX_split_seq.txt`)、Hi-CモードではSALSAで分断したコンティグのFASTA(`assembly.cleaned.fasta`)を読み込んで、最終的な伸長後のFASTAファイルなどを出力する。ターミナルのほうにXXXseq.txtファイル、fastaファイル名を入力する必要あり。
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
- B : (Hi-C専用)色の強度を変更する
- H : 黒背景か白背景を切り替える
- S : 編集したファイルを保存する。ターミナルのほうにファイル名を入力する必要あり。連鎖解析モードなら再度「O」キー、Hi-Cモードなら「I」キーを押して保存したファイルから編集を再開できる。ヒートマップも保存する。
- Z : 一度だけ直前の操作を取り消す。
- C : メモリー上に一時保存する
- V : メモリー上に一時保存したデータを呼び出す

## 入力データの作成方法

### 1. SELDLA家系1つ

#### 1.1. シングルセルや交雑種全ゲノムでSELDLA連鎖解析を行うためにVCFを作り連鎖解析を行う例

まずはとにかくVCFを作れば良いので、mpileupやGATKなどで作れば良い。それらを手軽に作る手順として拙作のPortable Pipelineを使った手順を説明しておく。

Dockerをインストールする。
https://docs.docker.com/engine/install/
に従い、Linux （x86_64）にDockerをインストールする。

インストール後にroot以外でもdockerを実行できるようにdockerグループに追加しておく。
```
sudo usermod -a -G docker $USER 
```
以上を実行したら設定を反映させるために、一旦ログアウトしてから再度ログインする必要あり。

```
git clone https://github.com/c2997108/OpenPortablePipeline.git
```

で、スクリプトをダウンロードしておく。

##### 1.1.1. 交雑種全ゲノムの場合

```
#contigのFASTAファイル：contigs.fasta
#RADseqなどのペアエンドのFASTQファイルが入ったディレクトリ：input_fastqs

python /Path/To/PortablePipeline/scripts/pp.py WGS~genotyping-by-mpileup input_fastqs contigs.fasta
#もしくは
#python /Path/To/PortablePipeline/scripts/pp.py WGS~genotyping-by-GATK input_fastqs contigs.fasta
#こちらはコンティグ数が多いと時間がかかる
```

その後、SELDLA連鎖解析で使用する家系情報`family.txt`は、下記のように作る。もし親を読んでいない場合は、「親のID」として「-1」を指定する。

```
"組み換えが生じたほうの親のID"\t"組み換えが生じていないほうの親のID"\t"交雑種である子供1"\t"子供2"...
```

これと対応するように、SNPを読み込んだあと下記の条件のサイトが解析対象となる。

```
1(ヘテロSNPとなっている場所のみが対象となる)\t-1(読まれていない場所のみが対象となる)\t[0 or 1 or 2]\t[0 or 1 or 2]...
```

SELDLA連鎖解析のコマンドは下記の通り。

```
python /Path/To/PortablePipeline/scripts/pp.py linkage-analysis~SELDLA -b "-p 0.03 -b 0.03 --NonZeroSampleRate=0.05 --NonZeroPhaseRate=0.1 -r 4000 --RateOfNotNASNP=0.001 --RateOfNotNALD=0.01 --ldseqnum 3" -d crossbreed contigs.fasta all.vcf family.txt
```

##### 1.1.2. 精子シングルセルの場合

別途10xのウェブサイトからCell Ranger DNA `cellranger-dna-1.1.0.tar.gz`をダウンロードしておく。

```
python /Path/To/PortablePipeline/scripts/pp.py linkage-analysis~single-cell_CellRanger-VarTrix input_fastqs contigs.fasta cellranger-dna-1.1.0.tar.gz
```

その後のSELDLA連鎖解析ではシングルセルの場合、次のファイルを使用する。
- pseudochr.re.fa.removedup.matrix.clean.txt_clean.txt
- pseudochr.re.fa.removedup.matrix.clean.txt.vcf2.family

family.txtの中身としては、
```
dummy\t精子1のID\t精子2...
```
となっていて、
clean.txtの中身は
```
1\t[-1 (欠損値) or 0 (変異なし) or 1 (変異あり）]\t[-1 (欠損値) or 0 (変異なし) or 1 (変異あり）]...
```

SELDLA連鎖解析のコマンドは下記の通り。

```
python /Path/To/PortablePipeline/scripts/pp.py linkage-analysis~SELDLA -b "-p 0.03 -b 0.03 --NonZeroSampleRate=0.05 --NonZeroPhaseRate=0.1 -r 4000 --RateOfNotNASNP=0.001 --RateOfNotNALD=0.01 --ldseqnum 3 --precleaned=pseudochr.re.fa.removedup.matrix.clean.txt_clean.txt" -d haploid pseudochr.re.fa.removedup.matrix.clean.txt_clean.txt pseudochr.re.fa.removedup.matrix.clean.txt.vcf2.family
```

ブロックサイズを決める-rオプションは、シングルセルの場合は欠損値が多いため10個程度のSNPが集まる大きさとしたほうが良いけど、大きくしすぎると解像度が下がってブレークポイントを検出しづらくなるので、コンティグN50の1/4程度までの大きさが良いかも。

#### 1.2. SELDLAの結果からSELDLA-Gの入力ファイルを作成

```
wget https://raw.githubusercontent.com/c2997108/SELDLA-G/master/scripts/make_SELDLA-G_input_from_single_1run.awk
#ノイズになりうるマーカーが1つしか取れなかったような短いコンティグを除去したい場合はこちら
#wget https://raw.githubusercontent.com/c2997108/SELDLA-G/master/scripts/make_SELDLA-G_input_from_single_1run_rmlowqual.awk
awk -F '\t' -f make_SELDLA-G_input_from_single_1run.awk seldla_split_1.txt.ld2imp seldla_split_1.txt.ld2.ph seldla_chain.txt > seldla_chain.ld2imp.all.txt
```

下記のファイルをSELDLA-Gの入力ファイルとする。
- `seldla_chain.ld2imp.all.txt` : -p オプションのフェーズ情報のほう
- `seldla_split_seq.txt` : -s オプションの配列情報のほう

### 2. SELDLA複数家系

RAD-seqなどの通常の連鎖解析はこれになる。単一家系でも、父方と母方の連鎖地図を統合すると思うので。上述のWGS~genotyping-by-mpileupなどでVCFを作っておく。

その後、family.txtとして、下記のようなファイルを作る

```
"雄親のID"\t"雌親のID"\t"子供1"\t"子供2"...
"雌親のID"\t"雄親のID"\t"子供1"\t"子供2"...
```

これと対応するように、SNPを読み込んだあと下記の条件のサイトが解析対象となる。

```
1(ヘテロSNPとなっている場所のみが対象となる)\t[0 or 2](ホモSNPとなっている場所のみが対象となる)\t[0 or 1 or 2]\t[0 or 1 or 2]...
```

SELDLA連鎖解析のコマンドは下記の通り。

```
SELDLA --exmatch 0.7 --clmatch 0.85 --spmatch 0.8 -p 0.3 -b 0.1 --NonZeroSampleRate=0.2 --NonZeroPhaseRate=0.3 -r 4000 --RateOfNotNASNP=0.2 --RateOfNotNALD=0.4 --ldseqnum 2 --fasta contigs.fasta --vcf all.vcf --family family.txt --mode duploid --output seldla
```

2回実行後に下記を実行する。

```
wget https://raw.githubusercontent.com/c2997108/SELDLA-G/master/scripts/make_SELDLA-G_input_from_multi_1run.awk
awk -F '\t' -f make_SELDLA-G_input_from_multi_1run.awk seldla_split_seq.txt seldla_split_*.txt.ld2.ph seldla_chain.txt > seldla_chain.ph.all.txt
```

この場合、出力されるのはコンティグの両端のフェーズ情報のみ。（複数家系あるとSNPの存在した場所がずれるため、ブロックごとに単純にマージできないため）
下記のファイルをSELDLA-Gの入力ファイルとする。
- `seldla_chain.ph.all.txt` : -p オプションのフェーズ情報のほう
- `seldla_split_seq.txt` : -s オプションの配列情報のほう
 
### 3. HiC SALSA解析

#### 3.1. Omni-Cの場合の解析例

```
#SALSAで解析するときの例
ref=scaffolds.fasta
fq1=reads_1.fastq
fq2=reads_2.fastq

bwa index $ref
samtools faidx $ref
bwa mem -t 32 $ref $fq1 $fq2 |samtools view -Sb - > output.bam
bamToBed -i output.bam > alignment.bed
python /Path/To/SALSA/run_pipeline.py -a $ref -l $ref.fai -b alignment.bed -e DNASE -o SALSA_output 
#ここまでSALSAの解析例
```

SALSA_outputフォルダーの中の`scaffolds_FINAL.agp`、`alignment_iteration_1.bed`を指定すれば良い。




## 作者の備忘録

古いバージョンの時のやり方。ver. 2.3.0以降のSELDLAなら複数回実行する必要なし。

### 4. SELDLA2回実行家系1つ

1度目のSELDLAで十分に伸長できていない場合、その出力を使って再度実行すると良い。その場合、伸長するかどうかの閾値を一度目よりも下げておく方が良い。

#### 4.1. 精子シングルセルを2回実行する場合

```
i=seldla_newpos_include_unoriented_in_chr.vcf
(head -n 1 $i; tail -n+2 $i|sort -k1,1V -k2,2n) > $i.sorted.vcf
awk -F'\t' 'NR==1{OFS="\t"; $10="dummy\t"$10; print $0} NR>1{$10="1\t"$10; print $0}' $i.sorted.vcf > $i.sorted.cleaned.txt
j=seldla_include_unoriented_in_chr.fasta;
SELDLA --exmatch 0.60 --clmatch 0.75 --spmatch 0.65 -p 0.03 -b 0.03 --NonZeroSampleRate=0.05 --NonZeroPhaseRate=0.1 -r 40000 --RateOfNotNASNP=0.001 --RateOfNotNALD=0.01 --ldseqnum 2 --fasta $j --vcf $i.sorted.vcf --precleaned $i.sorted.cleaned.txt --family family.filt.txt --mode haploid --output seldla2nd
```

2回実行後に下記を実行する。

```
wget https://raw.githubusercontent.com/c2997108/SELDLA-G/master/scripts/make_SELDLA-G_input_from_single_2run.awk
samtools faidx seldla_include_unoriented_in_chr.fasta
awk -F '\t' -f make_SELDLA-G_input_from_single_2run.awk seldla_split_1.txt.ld2imp seldla_split_1.txt.ld2.ph seldla_include_unoriented_in_chr.fasta.fai seldla2nd_break.txt seldla_chain.txt seldla2nd_chain.txt > seldla2nd_chain.ld2imp.all.txt
```

下記のファイルをSELDLA-Gの入力ファイルとする。
- `seldla2nd_chain.ld2imp.all.txt` : -p オプションのフェーズ情報のほう
- `seldla_split_seq.txt` : -s オプションの配列情報のほう


### 5. SELDLA2回実行家系複数

RAD-seqなどの通常の連鎖解析の2回実行版。

1度目のSELDLA連鎖解析のコマンドは下記の通り。

```
SELDLA --exmatch 0.7 --clmatch 0.85 --spmatch 0.8 -p 0.3 -b 0.1 --NonZeroSampleRate=0.2 --NonZeroPhaseRate=0.3 -r 4000 --RateOfNotNASNP=0.2 --RateOfNotNALD=0.4 --ldseqnum 2 --fasta contigs.fasta --vcf all.vcf --family family.txt --mode duploid --output seldla
```

2度目は例えば

```
i=seldla_newpos_include_unoriented_in_chr.vcf
(head -n 1 $i; tail -n+2 $i|sort -k1,1V -k2,2n) > $i.sorted.vcf
awk -F'\t' 'NR==1{OFS="\t"; $10="dummy\t"$10; print $0} NR>1{$10="1\t"$10; print $0}' $i.sorted.vcf > $i.sorted.cleaned.txt
j=seldla_include_unoriented_in_chr.fasta
SELDLA --exmatch 0.60 --clmatch 0.75 --spmatch 0.65 -p 0.3 -b 0.1 --NonZeroSampleRate=0.2 --NonZeroPhaseRate=0.3 -r 20000 --RateOfNotNASNP=0.2 --RateOfNotNALD=0.4 --ldseqnum 2 --fasta $j --vcf $i.sorted.vcf --precleaned $i.sorted.cleaned.txt --family family.txt --mode duploid --output seldla2nd
```

2回実行後に下記を実行する。

```
wget https://raw.githubusercontent.com/c2997108/SELDLA-G/master/scripts/make_SELDLA-G_input_from_multi_2run.awk
samtools faidx seldla_include_unoriented_in_chr.fasta
awk -F '\t' -f make_SELDLA-G_input_from_multi_2run.awk seldla_split_seq.txt seldla_split_*.txt.ld2.ph seldla_include_unoriented_in_chr.fasta.fai seldla2nd_break.txt seldla_chain.txt seldla2nd_chain.txt > seldla2nd_chain.ph.all.txt
```

この場合、出力されるのはコンティグの両端のフェーズ情報のみ。（複数家系あるとSNPの存在した場所がずれるため、ブロックごとに単純にマージできないため）
下記のファイルをSELDLA-Gの入力ファイルとする。
- `seldla2nd_chain.ph.all.txt` : -p オプションのフェーズ情報のほう
- `seldla_split_seq.txt` : -s オプションの配列情報のほう
 
