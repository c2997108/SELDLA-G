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
 FILENAME==ARGV[3] && $0!~"^#"{ #1       linkage_scaffold_92     -       0       1778262 0       16.279069767441857

    if($3=="+"||$3=="na"){
        PROCINFO["sorted_in"] = "@ind_num_asc"; 
    }else{
        PROCINFO["sorted_in"] = "@ind_num_desc";
    }
    scaf=$2;
    if(length(a[scaf])>0){
        for(j in a[scaf]){
            ORS="";
            split(a[scaf][j],arr,"\t");
            print $1"\t"scaf"\t"$3"\t+\t"scaf;
            for(k=3;k<=length(arr);k++){print "\t"arr[k]}; 
            print "\n";ORS="\n";
        }
    }else{ #lowqual‚ðo—Í
        if(length(ph[scaf])>0){
            for(j in ph[scaf]){
                ORS="";
                split(ph[scaf][j],arr,"\t");
                print $1"\t"scaf"\t"$3"\t+\t"scaf;
                for(k=3;k<=length(arr);k++){print "\t"arr[k]}; 
                print "\n";ORS="\n";
            }
        }
    }
  
 }