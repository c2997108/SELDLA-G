[日本語ページ](https://github.com/c2997108/SELDLA-G/blob/master/README_jp.md)

# SELDLA-G

This tool is originaly a linkage analysis tool used in de novo genome construction with the output of [SELDLA](https://github.com/c2997108/SELDLA) to finish genome construction while removing errors manually like a contact map of Hi-C. It can also be used for Hi-C contact map editing.

## Install

You can download the binary from the following link and unzip.
https://github.com/c2997108/SELDLA-G/releases

## Requirements

- Windows 10 or 11

## Inputs

- [SALSA](https://github.com/marbl/SALSA) output files

## How to use

#### Example 1. Omni-C

The example of SALSA Analysis. The following steps might be done on Linux.

```
ref=scaffolds.fasta
fq1=reads_1.fastq
fq2=reads_2.fastq

bwa index $ref
samtools faidx $ref
bwa mem -t 32 $ref $fq1 $fq2 |samtools view -Sb - > output.bam
bamToBed -i output.bam > alignment.bed
python /Path/To/SALSA/run_pipeline.py -a $ref -l $ref.fai -b alignment.bed -e DNASE -o SALSA_output 
```

Then copy SALSA_output folder to Windows and open the PowerShell or Terminal on Windows to run SELDLA-G as follows.

```
.\SELDLA-G.exe hic -a /path/to/SALSA_output/scaffolds_FINAL.agp -b /path/to/SALSA_output/alignment_iteration_1.bed -f /path/to/SALSA_output/assembly.cleaned.fasta
#-w: window size (bp) [100,000]
#-l: the limit of contig length (bp) [1,000]
```

If you can open SALSA_output, you can see the contact map like the followings.

<img width="618" alt="image" src="https://user-images.githubusercontent.com/5350508/156413938-06fed85e-5c3f-42c6-b348-007be7cfcd54.png">

Press the following keys to load, edit, and save.

- O : Open file. You can restart with the files of `scaffolds_FINAL.agp` and `alignment_iteration_1.bed`. Please type in the terminal.
- I : Load the saved files and display it again. `XXX.agp`, `XXX.matrix`. Please type in the terminal.
- S : Save the edited files. It is necessary to enter the file name in the terminal. You can resume editing from the saved file by pressing the "I" key again. The heat map is also saved as a PNG file.
- P : Output a chromosome FASTA file. The FASTA of contigs split by SALSA (`assembly.cleaned.fasta`) is needed. Please type in the terminal.
- R : Basically, this tool's contact map is operated from the position of the X, Y markers of the current mouse cursor position. Pressing R reverses the intrastromal alignment of the upper blue marker.
- T : Swap the chromosomes between the two blue markers
- Y : Reverses the order of the upper green marker in the contig. Selecting a range across chromosomes will work, but it will cause various problems, so select contigs within the same chromosome.
- U : Swap the contig between two green markers.
- F : To swap the chromosome orientation in the selected range, press once to select the upper blue as the starting point, and press twice to confirm and invert the range.
- G : To swap the chromosomes between the selected ranges, press 1 to 4 times to confirm the range to be swapped.
- N : Swap the orientation of the contigs in the selected range; press once to select the upper green as the starting point, and press twice to confirm and invert the range. Selecting a range across chromosomes will work, but it will cause all sorts of problems, so be sure to select contigs within the same chromosome.
- M : Swap the contigs between the selected ranges, and press 1 to 4 times to confirm the range to be swapped. Selecting a range of contigs beyond a chromosome will work, but it will cause various problems, so be sure to select contigs within the same chromosome.
- D : Delete the contig of the upper green marker.
- W : Change the name of the chromosome to which the selected range of chromosomes belong. If the chromosomes are changed in such a way that they are nested, it will cause strange behavior.
- E : Change the chromosome name to which the selected range of contigs belongs. The contig to be changed must include the contig at the end of the chromosome. If the chromosomes are nested, it will cause strange behavior.
- Esc : Pressing the escape key cancels marker selection using the F, G, N, M, W, and E keys.
- B : change color intensity
- H : Toggle between black or white background
- Z : Cancel the previous operation only once.
- C : Save temporarily in memory.
- V : Recall data temporarily stored in memory
- 1 : Automatically reorder chromosomes that are in contact with each other in close proximity
