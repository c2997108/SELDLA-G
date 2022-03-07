
 BEGIN{
  ORS=""; print "#2nd_linkage_group\t1st_linkage_group\tcontig_orientation_at_2nd\tcontig_orientation_at_1st\toriginal_contig_name\tmarker_position_on_original_contig";
 }
 FILENAME==ARGV[1]{ #info    scaffold6.1|size931094_2        16491   1       0       1       1 
  if(FNR==1){ ##info   #chr    pos     RBYw13  RBYw1   RBYw20  RBYw23
   for(i=4;i<=NF;i++){print "\t"$i}; print "\n";
  };
  a[$2][$3]=$0; ORS="\n";
 }
 FILENAME==ARGV[2] && $0!~"^#"{ph[$1][$3]=$0} #scaffold6.1|size931094_1        lowqual 83938   0       1       0       -1
 FILENAME==ARGV[3]{len[$1]=$2} #linkage_scaffold_1      51206515        20      51206515        51206516
 FILENAME==ARGV[4] { #linkage_scaffold_108    37018   265460  265118
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
 FILENAME==ARGV[5] && $0!~"^#"{n[$1]++; original[$1][n[$1]]=$2; orient[$1][n[$1]]=$3; originalstart[$1][n[$1]]=$4+1; originalend[$1][n[$1]]=$5}
 FILENAME==ARGV[6] && $0!~"^#"{ #1       linkage_scaffold_92     -       0       1778262 0       16.279069767441857
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
                        if(length(a[original[oldchr][i]])>0){
                            for(j in a[original[oldchr][i]]){
                                ORS="";
                                split(a[original[oldchr][i]][j],arr,"\t");
                                print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                                for(k=3;k<=length(arr);k++){print "\t"arr[k]}; 
                                print "\n";ORS="\n";
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
                        if(length(a[original[oldchr][i]])>0){
                            for(j in a[original[oldchr][i]]){
                                ORS="";
                                split(a[original[oldchr][i]][j],arr,"\t");
                                print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                                for(k=3;k<=length(arr);k++){print "\t"arr[k]}; 
                                print "\n";ORS="\n";
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
                    if(length(a[original[oldchr][i]])>0){
                        for(j in a[original[oldchr][i]]){
                            ORS="";
                            split(a[original[oldchr][i]][j],arr,"\t");
                            print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                            for(k=3;k<=length(arr);k++){print "\t"arr[k]}; 
                            print "\n";ORS="\n";
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
                    if(length(a[original[oldchr][i]])>0){
                        for(j in a[original[oldchr][i]]){
                            ORS="";
                            split(a[original[oldchr][i]][j],arr,"\t");
                            print $1"\t"oldchr"\t"$3"\t"orient[oldchr][i]"\t"original[oldchr][i];
                            for(k=3;k<=length(arr);k++){print "\t"arr[k]}; 
                            print "\n";ORS="\n";
                        }
                    }
                }
            }
        }
    }
  
 }