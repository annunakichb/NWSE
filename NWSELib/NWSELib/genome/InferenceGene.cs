﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.genome
{

    /// <summary>
    /// 推断基因
    /// </summary>
    public class InferenceGene : NodeGene
    {
        /// <summary>
        /// 推断各维的节点ID或者名称,以及时间项
        /// </summary>
        public List<(int, int)> dimensions = new List<(int, int)>();

        public override T clone<T>()
        {
            InferenceGene gene = new InferenceGene().copy<InferenceGene>(this);
            gene.dimensions.AddRange(this.dimensions);
            return (T)(Object)gene;
        }

        private int comp_dimension((int,int) t1,(int,int) t2)
        {
            if (t1.Item2 > t2.Item2) return 1;
            else if (t1.Item2 < t2.Item2) return -1;
            else
            {
                if (t1.Item1 > t2.Item1) return 1;
                else if (t1.Item1 < t2.Item1) return -1;
                return 0;
            }
        }
        public void sort_dimension()
        {
            this.dimensions.Sort(comp_dimension);
        }

        /// <summary>
        /// 两个推理基因的关系
        /// </summary>
        /// <param name="gene">基因</param>
        /// <returns>0表示一致；1表示this包含另外一个；-1表示this被包含；2表示交叉；-2表示没有交叉</returns>
        public int relation(InferenceGene gene)
        {
            int[] rs = { 1, 1, 1, 1, 1 };
            int[] r = { 0, 1, -1, 2, -2 };
            for(int i=0;i<dimensions.Count;i++)
            {
                if (!gene.dimensions.Exists(d => d.Item1 == this.dimensions[i].Item1))
                {
                    rs[0] = rs[2] = 0;
                    continue;
                }
                if (!gene.dimensions.Exists(d => d.Item1 == this.dimensions[i].Item1 && d.Item2 == this.dimensions[i].Item2))
                {
                    rs[0] = 0;
                }
                else rs[4] = 0;

            }

            for (int i = 0; i < gene.dimensions.Count; i++)
            {
                if (!dimensions.Exists(d => d.Item1 == gene.dimensions[i].Item1))
                {
                    rs[0] = rs[1] = 0;
                    continue;
                }
                if (!dimensions.Exists(d => d.Item1 == gene.dimensions[i].Item1 && d.Item2 == gene.dimensions[i].Item2))
                {
                    rs[0] = 0;
                }
                else rs[4] = 0;

            }
            for (int i = 0; i < rs.Length; i++)
                if (rs[i] != 0) return r[i];
            throw new ExecutionEngineException();

        }
        public override string ToString()
        {
            this.sort_dimension();
            return base.ToString() + ",dimensions=" +
                dimensions.ConvertAll(d=>d.Item1.ToString()+"-"+d.Item2.ToString())
                .Aggregate((x,y)=>x+","+y);
        }
        public static new InferenceGene parse(String str)
        {
            InferenceGene gene = new InferenceGene();
            ((NodeGene)gene).parse(str);

            int i1 = str.IndexOf("dimensions");
            int i2 = str.IndexOf("=", i1 + 1);
            String s = str.Substring(i2+1);
            String[] ss = s.Split(',');
            for(int i = 0; i < ss.Length; i++)
            {
                if (ss[i] == null || ss[i].Trim() == "") continue;
                String[] s2 = ss[i].Trim().Split('-');
                if (s2 == null || s2.Length < 2) continue;
                int t1 = int.Parse(s2[0]);
                int t2 = int.Parse(s2[1]);
                gene.dimensions.Add((t1,t2));
            }
            return gene;
        } 

        /// <summary>
        /// 得到推理变量Id对应的索引
        /// </summary>
        /// <param name="varId"></param>
        /// <returns></returns>
        public int getVariableIndex(int varId=-1)
        {
            (int t1, int t2) = this.getTimeDiff();
            for (int i = 0; i < dimensions.Count; i++)
            {
                if ((varId == -1 || dimensions[i].Item1 == varId) && dimensions[i].Item2 == t2)
                    return i;
            }
            return -1;
        }
        /// <summary>
        /// 得到条件Id对应的索引
        /// </summary>
        /// <param name="condId"></param>
        /// <returns></returns>
        public int getConditionIndex(int condId)
        {
            (int t1, int t2) = this.getTimeDiff();
            for (int i = 0; i < dimensions.Count; i++)
            {
                if (dimensions[i].Item1 == condId && dimensions[i].Item2 == t1)
                    return i;
            }
            return -1;
        }
        /// <summary>
        /// 取得条件和后置变量的时间差
        /// </summary>
        /// <returns></returns>
        public (int, int) getTimeDiff()
        {
            (int t1, int t2) = (
                dimensions.ConvertAll<int>(d => Math.Abs(d.Item2)).Max(),
                dimensions.ConvertAll<int>(d => Math.Abs(d.Item2)).Min()
                );
            return (t1, t2);
        }
        /// <summary>
        /// 得到所有的条件，包括Id和相对时间
        /// </summary>
        /// <returns></returns>
        public List<(int, int)> getConditions()
        {
            (int t1, int t2) = this.getTimeDiff();
            return dimensions.FindAll(d => d.Item2 == t1);
        }
        /// <summary>
        /// 得到后置变量Id和相对时间
        /// </summary>
        /// <returns></returns>
        public (int, int) getVariable()
        {
            (int t1, int t2) = this.getTimeDiff();
            return dimensions.FirstOrDefault<(int,int)>(d => d.Item2 == t2);
        }
        
        /// <summary>
        /// 条件是否匹配
        /// </summary>
        /// <param name="allmatched">要求全部匹配</param>
        /// <param name="conditions">条件Id</param>
        /// <returns></returns>
        public bool matchCondition(bool allmatched, params int[] conditions)
        {
            (int t1, int t2) = this.getTimeDiff();
            List<int> conds = conditions.ToList();
            for (int i = 0; i < dimensions.Count; i++)
            {
                if (!conds.Contains(dimensions[i].Item1)) continue;
                if (dimensions[i].Item2 < t1) return false;
                conds.Remove(dimensions[i].Item1);
            }
            return allmatched ? conds.Count <= 0 : conds.Count < conditions.Length;
        }
        /// <summary>
        /// 变量是否匹配
        /// </summary>
        /// <param name="allmatch">全部匹配</param>
        /// <param name="variables"></param>
        /// <returns></returns>
        public bool matchVariable(int variableId)
        {
            (int t1, int t2) = this.getTimeDiff();
         
            for (int i = 0; i < dimensions.Count; i++)
            {
                if (variableId != dimensions[i].Item1) continue;
                if (dimensions[i].Item2 != t2) continue;
                return true;
            }
            return false;
        }
        


    }
}
