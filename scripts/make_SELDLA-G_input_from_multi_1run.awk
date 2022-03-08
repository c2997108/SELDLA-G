 BEGIN{
  ORS=""; print "#2nd_linkage_group\t1st_linkage_group\tcontig_orientation_at_2nd\tcontig_orientation_at_1st\toriginal_contig_name\tmarker_position_on_original_contig";
 }
 FILENAME==ARGV[1]{lenscaf[$1]=length($2)}
 FILENAME~".*_split_.*[.]txt[.]ld2[.]ph$"{ #info    scaffold6.1|size931094_2        16491   1       0       1       1 
    if(FNR==1){ ##info   #chr    pos     RBYw13  RBYw1   RBYw20  RBYw23
        nfileorder++;
        nitems[nfileorder]=NF;
        filenum=FILENAME;
        #fileorder[nfileorder]=filenum;
        sub(/.*_split_/,"",filenum);
        sub(/[.]txt[.]ld2[.]ph$/,"",filenum);
        for(i=4;i<=NF;i++){print "\t"filenum"#"$i};
    }else{
        #print $1,lenscaf[$1],nfileorder"\n";
        countph[nfileorder][$1]++;
        if(countph[nfileorder][$1]==1){
            ph[$1][1][nfileorder]=$0
        }
        ph[$1][lenscaf[$1]][nfileorder]=$0
    }
 }
 FILENAME==ARGV[length(ARGV)-1] && $0!~"^#"{ #1       linkage_scaffold_92     -       0       1778262 0       16.279069767441857
    if(header==0){header=1; print "\n"; ORS="\n"};
    scaf=$2;
    print "#"$1"\t"scaf"\t"$3"\t+\t"scaf;
    if($3=="+"||$3=="na"){
        PROCINFO["sorted_in"] = "@ind_num_asc"; 
    }else{
        PROCINFO["sorted_in"] = "@ind_num_desc";
    }
    #フェーズが空でなければ
    if(length(ph[scaf])>0){
        for(j in ph[scaf]){
            ORS="";
            print $1"\t"scaf"\t"$3"\t+\t"scaf"\t"j;
            for(m=1;m<=nfileorder;m++){
                if(length(ph[scaf][j][m])>0){
                    split(ph[scaf][j][m],arr,"\t");
                    for(k=4;k<=length(arr);k++){print "\t"arr[k]}; 
                }else{
                    for(k=4;k<=nitems[m];k++){print "\t-1"};
                }
            }
            print "\n";
            ORS="\n";
        }
    }
 }  
