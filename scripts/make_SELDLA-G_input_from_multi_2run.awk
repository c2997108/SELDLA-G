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
 FILENAME==ARGV[length(ARGV)-4]{len[$1]=$2} #linkage_scaffold_1      51206515        20      51206515        51206516
 FILENAME==ARGV[length(ARGV)-3] { #linkage_scaffold_108    37018   265460  265118
     nsplit[$1]++;
     #1つ目の断片の範囲
     if(nsplit[$1]==1){
         posstart[$1"_1"]=1;
     }
     posend[$1"_"nsplit[$1]]=$4;
     #2つ目の断片の範囲
     posstart[$1"_"nsplit[$1]+1]=$4+1;
     posend[$1"_"nsplit[$1]+1]=len[$1];
     #断片名を記憶しておく
     if(nsplit[$1]==1){
        split2nd[$1"_1"]=1;
        beforesplitname[$1"_1"]=$1;
     }; 
     split2nd[$1"_"nsplit[$1]+1]=1;
     beforesplitname[$1"_"nsplit[$1]+1]=$1;
 }
 FILENAME==ARGV[length(ARGV)-2] && $0!~"^#"{n[$1]++; original[$1][n[$1]]=$2; orient[$1][n[$1]]=$3; originalstart[$1][n[$1]]=$4+1; originalend[$1][n[$1]]=$5}
 FILENAME==ARGV[length(ARGV)-1] && $0!~"^#"{ #1       linkage_scaffold_92     -       0       1778262 0       16.279069767441857
    if(header==0){header=1; print "\n"; ORS="\n"};
    #print "test: "$0;
    #1回目で使われたかどうか
    #分割しているかどうか
    if($2~"^old"){
        #1回目で使われていないコンティグ
        if(split2nd[$2]==1){
            #2回目で分割あり
            print "There is a contig that was first used and splited in SELDLA 2nd run. 2nd run should be used with looser threshold"; 
            exit 1
        }else{
            #2回目で分割なし
            oldscaf=substr($2,5);
            print "#"$1"\t"oldscaf"\t"$3"\t+\t"oldscaf;
            if($3=="+"||$3=="na"){
                PROCINFO["sorted_in"] = "@ind_num_asc"; 
            }else{
                PROCINFO["sorted_in"] = "@ind_num_desc";
            }
            #フェーズが空でなければ
            if(length(ph[oldscaf])>0){
                for(j in ph[oldscaf]){
                    ORS="";
                    print $1"\t"oldscaf"\t"$3"\t+\t"oldscaf"\t"j;
                    for(m=1;m<=nfileorder;m++){
                        if(length(ph[oldscaf][j][m])>0){
                            split(ph[oldscaf][j][m],arr,"\t");
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
    }else{
        #1回目で使われたコンティグ
        if(split2nd[$2]==1){
            #2回目で分割あり
            oldchr=substr(beforesplitname[$2],18);
            if($3=="+"||$3=="na"){
                PROCINFO["sorted_in"] = "@ind_num_asc";
                for(i in original[oldchr]){
                    if(((originalstart[oldchr][i]>=posstart[$2] && originalstart[oldchr][i]<=posend[$2]) || (originalend[oldchr][i]>=posstart[$2] && originalend[oldchr][i]<=posend[$2])) && flagused[original[oldchr][i]]==0){
                        flagused[original[oldchr][i]]=1;
                        print "#"$1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                        if(orient[oldchr][i]=="+"||orient[oldchr][i]=="na"){
                            PROCINFO["sorted_in"] = "@ind_num_asc";
                        }else{
                            PROCINFO["sorted_in"] = "@ind_num_desc";
                        }
                        #フェーズが空でなければ
                        if(length(ph[original[oldchr][i]])>0){
                            for(j in ph[original[oldchr][i]]){
                                ORS="";
                                print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i]"\t"j;
                                for(m=1;m<=nfileorder;m++){
                                    if(length(ph[original[oldchr][i]][j][m])>0){
                                        split(ph[original[oldchr][i]][j][m],arr,"\t");
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
                }
            }else{
                PROCINFO["sorted_in"] = "@ind_num_desc";
                for(i in original[oldchr]){
                    if(((originalstart[oldchr][i]>=posstart[$2] && originalstart[oldchr][i]<=posend[$2]) || (originalend[oldchr][i]>=posstart[$2] && originalend[oldchr][i]<=posend[$2])) && flagused[original[oldchr][i]]==0){
                        flagused[original[oldchr][i]]=1;
                        print "#"$1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                        if(orient[oldchr][i]=="+"||orient[oldchr][i]=="na"){
                            PROCINFO["sorted_in"] = "@ind_num_desc";
                        }else{
                            PROCINFO["sorted_in"] = "@ind_num_asc";
                        }
                        #フェーズが空でなければ
                        if(length(ph[original[oldchr][i]])>0){
                            for(j in ph[original[oldchr][i]]){
                                ORS="";
                                print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i]"\t"j;
                                for(m=1;m<=nfileorder;m++){
                                    if(length(ph[original[oldchr][i]][j][m])>0){
                                        split(ph[original[oldchr][i]][j][m],arr,"\t");
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
                }
            }

        
        }else{
            #2回目で分割なし
            oldchr=substr($2,18);
            if($3=="+"||$3=="na"){
                PROCINFO["sorted_in"] = "@ind_num_asc";
                for(i in original[oldchr]){
                    print "#"$1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                    if(orient[oldchr][i]=="+"||orient[oldchr][i]=="na"){
                        PROCINFO["sorted_in"] = "@ind_num_asc";
                    }else{
                        PROCINFO["sorted_in"] = "@ind_num_desc";
                    }
                    #フェーズが空でなければ
                    if(length(ph[original[oldchr][i]])>0){
                        for(j in ph[original[oldchr][i]]){
                            ORS="";
                            print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i]"\t"j;
                            for(m=1;m<=nfileorder;m++){
                                if(length(ph[original[oldchr][i]][j][m])>0){
                                    split(ph[original[oldchr][i]][j][m],arr,"\t");
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
            }else{
                PROCINFO["sorted_in"] = "@ind_num_desc";
                for(i in original[oldchr]){
                    print "#"$1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                    if(orient[oldchr][i]=="+"||orient[oldchr][i]=="na"){
                        PROCINFO["sorted_in"] = "@ind_num_desc";
                    }else{
                        PROCINFO["sorted_in"] = "@ind_num_asc";
                    }
                    #フェーズが空でなければ
                    if(length(ph[original[oldchr][i]])>0){
                        for(j in ph[original[oldchr][i]]){
                            ORS="";
                            print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i]"\t"j;
                            for(m=1;m<=nfileorder;m++){
                                if(length(ph[original[oldchr][i]][j][m])>0){
                                    split(ph[original[oldchr][i]][j][m],arr,"\t");
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
            }
        }

    }
 }  
