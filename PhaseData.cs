using System;
using System.Collections.Generic;

using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;

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

            if (this.chrname != ((ContigPos)obj).chrname)
            {
                return this.chrname.CompareTo(((ContigPos)obj).chrname);
            }
            else
            { 
                return this.start_bp.CompareTo(((ContigPos)obj).start_bp);
            }
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


    //public static class CopyHelper
    //{
    //    /// <summary>
    //    /// オブジェクトの値コピーを容易にする拡張メソッド。
    //    /// </summary>
    //    public static T DeepCopy<T>(this T src)
    //    {
    //        var jsonSerializerOptions = new JsonSerializerOptions()
    //        {
    //            ReferenceHandler = ReferenceHandler.Preserve,
    //            WriteIndented = true
    //        };

    //        var jsonData = JsonSerializer.Serialize(src, jsonSerializerOptions);

    //        return JsonSerializer.Deserialize<T>(jsonData, jsonSerializerOptions);
    //    }
    //}

    [Serializable]
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
        public PhaseData DeepCopy()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                //BinaryFormatter bf = new BinaryFormatter();
                //bf.Serialize(ms, this);
                //ms.Position = 0;
                //return (PhaseData)bf.Deserialize(ms);

                return new PhaseData()
                {
                    chr2nd = this.chr2nd,
                    chrorig = this.chrorig,
                    chrorient = this.chrorient,
                    markerpos = this.markerpos,
                    dataphase = new List<int>(this.dataphase),
                    chrorigStartIndex = this.chrorigStartIndex,
                    chrorigEndIndex = this.chrorigEndIndex,
                    contigsize = this.contigsize,
                    regionsize = this.regionsize
                };


                //var jsonSerializerOptions = new JsonSerializerOptions()
                //{
                //    ReferenceHandler = ReferenceHandler.Preserve,
                //    WriteIndented = true
                //};

                //var jsonData = JsonSerializer.Serialize(this, jsonSerializerOptions);

                //return JsonSerializer.Deserialize<PhaseData>(jsonData, jsonSerializerOptions);

            }
        }
    }

    class CountBox
    {
        List<int> positions = new List<int>();
        public CountBox()
        {
        }
        public void addItem(int i)
        {
            positions.Add(i);
        }
        public int getNum()
        {
            return positions.Count;
        }
        public List<int> getFirstPositions(int num)
        {
            List<int> list = new List<int>();
            if(getNum() < num) { num = getNum(); }
            for (int i = 0; i < num; i++)
            {
                list.Add(positions[i]);
            }
            return list;
        }
        public List<int> getLastPositions(int num)
        {
            List<int> list = new List<int>();
            if (getNum() < num) { num = getNum(); }
            for (int i = getNum() - 1; i >= getNum() - num; i--)
            {
                list.Add(positions[i]);
            }
            return list;
        }
    }

    class MaxRelation
    {
        string sep = "##SELDLA##";
        List<KeyValuePair<string, float>> sortedForFor;
        List<KeyValuePair<string, float>> sortedForBac;
        List<KeyValuePair<string, float>> sortedBacFor;
        List<KeyValuePair<string, float>> sortedBacBac;
        public MaxRelation(Dictionary<string, float> ForFor, Dictionary<string, float> ForBac, Dictionary<string, float> BacFor, Dictionary<string, float> BacBac)
        {
            sortedForFor = ForFor.ToList();
            sortedForBac = ForBac.ToList();
            sortedBacFor = BacFor.ToList();
            sortedBacBac = BacBac.ToList();
            sortedForFor.Sort((x, y) => -x.Value.CompareTo(y.Value));
            sortedForBac.Sort((x, y) => -x.Value.CompareTo(y.Value));
            sortedBacFor.Sort((x, y) => -x.Value.CompareTo(y.Value));
            sortedBacBac.Sort((x, y) => -x.Value.CompareTo(y.Value));
        }

        public bool isEmpty()
        {
            if(sortedForFor.Count==0 && sortedForBac.Count==0 && sortedBacFor.Count==0 && sortedBacBac.Count == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public (string, int, float) getTopRelation()
        {
            if (sortedForFor.Count > 0
                && ((sortedForBac.Count > 0 && sortedForFor[0].Value >= sortedForBac[0].Value) || sortedForBac.Count == 0)
                && ((sortedBacFor.Count > 0 && sortedForFor[0].Value >= sortedBacFor[0].Value) || sortedBacFor.Count == 0)
                && ((sortedBacBac.Count > 0 && sortedForFor[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                string result = sortedForFor[0].Key;
                float value = sortedForFor[0].Value;
                var list = sortedForFor[0].Key.Split(sep);
                return (result, 0, value);
            }
            else if (sortedForBac.Count > 0
                && ((sortedBacFor.Count > 0 && sortedForBac[0].Value >= sortedBacFor[0].Value) || sortedBacFor.Count == 0)
                && ((sortedBacBac.Count > 0 && sortedForBac[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                string result = sortedForBac[0].Key;
                float value = sortedForBac[0].Value;
                var list = sortedForBac[0].Key.Split(sep);
                return (result, 1, value);
            }
            else if (sortedBacFor.Count > 0
                && ((sortedBacBac.Count > 0 && sortedBacFor[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                string result = sortedBacFor[0].Key;
                float value = sortedBacFor[0].Value;
                var list = sortedBacFor[0].Key.Split(sep);
                return (result, 2, value);
            }
            else
            {
                string result = sortedBacBac[0].Key;
                float value = sortedBacBac[0].Value;
                var list = sortedBacBac[0].Key.Split(sep);
                return (result, 3, value);
            }
        }
        public void deleteOnlyTop()
        {
            if (sortedForFor.Count > 0
                && ((sortedForBac.Count > 0 && sortedForFor[0].Value >= sortedForBac[0].Value) || sortedForBac.Count == 0)
                && ((sortedBacFor.Count > 0 && sortedForFor[0].Value >= sortedBacFor[0].Value) || sortedBacFor.Count == 0)
                && ((sortedBacBac.Count > 0 && sortedForFor[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                sortedForFor.RemoveAt(0);
            }
            else if (sortedForBac.Count > 0
                && ((sortedBacFor.Count > 0 && sortedForBac[0].Value >= sortedBacFor[0].Value) || sortedBacFor.Count == 0)
                && ((sortedBacBac.Count > 0 && sortedForBac[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                sortedForBac.RemoveAt(0);
            }
            else if (sortedBacFor.Count > 0
                && ((sortedBacBac.Count > 0 && sortedBacFor[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                sortedBacFor.RemoveAt(0);
            }
            else
            {
                sortedBacBac.RemoveAt(0);
            }
        }
        public (string, int, float) getTopRelationAndDelete()
        {
            if (sortedForFor.Count > 0
                && ((sortedForBac.Count > 0 && sortedForFor[0].Value >= sortedForBac[0].Value) || sortedForBac.Count == 0)
                && ((sortedBacFor.Count > 0 && sortedForFor[0].Value >= sortedBacFor[0].Value) || sortedBacFor.Count == 0)
                && ((sortedBacBac.Count > 0 && sortedForFor[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                string result = sortedForFor[0].Key;
                float value = sortedForFor[0].Value;
                var list = sortedForFor[0].Key.Split(sep);
                deleteFor(list[0]);
                deleteFor(list[1]);
                return (result, 0, value);
            }
            else if( sortedForBac.Count > 0
                && ((sortedBacFor.Count > 0 && sortedForBac[0].Value >= sortedBacFor[0].Value) || sortedBacFor.Count == 0)
                && ((sortedBacBac.Count > 0 && sortedForBac[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                string result = sortedForBac[0].Key;
                float value = sortedForBac[0].Value;
                var list = sortedForBac[0].Key.Split(sep);
                deleteFor(list[0]);
                deleteBac(list[1]);
                return (result, 1, value);
            }
            else if (sortedBacFor.Count > 0
                && ((sortedBacBac.Count > 0 && sortedBacFor[0].Value >= sortedBacBac[0].Value) || sortedBacBac.Count == 0))
            {
                string result = sortedBacFor[0].Key;
                float value = sortedBacFor[0].Value;
                var list = sortedBacFor[0].Key.Split(sep);
                deleteBac(list[0]);
                deleteFor(list[1]);
                return (result, 2, value);
            }
            else
            {
                string result = sortedBacBac[0].Key;
                float value = sortedBacBac[0].Value;
                var list = sortedBacBac[0].Key.Split(sep);
                deleteBac(list[0]);
                deleteBac(list[1]);
                return (result, 3, value);
            }
        }
        
        void deleteFor(string chr)
        {
            for(int i=sortedForFor.Count-1; i>=0; i--)
            {
                var list = sortedForFor[i].Key.Split(sep);
                if(list[0]==chr || list[1] == chr)
                {
                    sortedForFor.RemoveAt(i);
                }
            }
            for (int i = sortedForBac.Count - 1; i >= 0; i--)
            {
                var list = sortedForBac[i].Key.Split(sep);
                if (list[0] == chr)
                {
                    sortedForBac.RemoveAt(i);
                }
            }
            for (int i = sortedBacFor.Count - 1; i >= 0; i--)
            {
                var list = sortedBacFor[i].Key.Split(sep);
                if (list[1] == chr)
                {
                    sortedBacFor.RemoveAt(i);
                }
            }
        }
        void deleteBac(string chr)
        {
            for (int i = sortedBacBac.Count - 1; i >= 0; i--)
            {
                var list = sortedBacBac[i].Key.Split(sep);
                if (list[0] == chr || list[1] == chr)
                {
                    sortedBacBac.RemoveAt(i);
                }
            }
            for (int i = sortedBacFor.Count - 1; i >= 0; i--)
            {
                var list = sortedBacFor[i].Key.Split(sep);
                if (list[0] == chr)
                {
                    sortedBacFor.RemoveAt(i);
                }
            }
            for (int i = sortedForBac.Count - 1; i >= 0; i--)
            {
                var list = sortedForBac[i].Key.Split(sep);
                if (list[1] == chr)
                {
                    sortedForBac.RemoveAt(i);
                }
            }
        }
    }
}
