using System;
using System.Collections.Generic;

namespace SELDLA_G
{
    class ContigPos : System.IComparable
    {
        public string contigname;
        public string chrname;
        public int start_bp;
        public int end_bp;
        public float start_cm;
        public float end_cm;
        public string orientation;
        public int CompareTo(object obj)
        {
            //nullより大きい
            if (obj == null) return 1;
            //違う型とは比較できない
            if (this.GetType() != obj.GetType()) throw new ArgumentException("別の型とは比較できません。", "obj");

            if(this.chrname != ((ContigPos)obj).chrname)
            {
                return this.chrname.CompareTo(((ContigPos)obj).chrname);
            }
            else

                return this.start_bp.CompareTo(((ContigPos)obj).start_bp);
            }
        }
    class MarkerPos
    {
        public int X;
        public string chrname;
        public string contigname;
        public int chrStart = -1;
        public int chrEnd = -1;
        public int contigStart = -1;
        public int contigEnd = -1;
    }

    internal class PhaseData
    {
        public string chr2nd;
        public string chrorig;
        public string chrorient;
        public string markerpos;
        public List<int> dataphase;
        public int chrorigStartIndex = -1;
        public int chrorigEndIndex = -1;
        public int contigsize;
        public int regionsize;
    }
}
