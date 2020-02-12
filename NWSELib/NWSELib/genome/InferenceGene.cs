using NWSELib.common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.genome
{

    /// <summary>
    /// 推断基因
    /// </summary>
    public class InferenceGene : NodeGene
    {
        #region 基本信息
        /// <summary>
        /// 推断各维的前提条件节点ID或者名称,以及时间项
        /// </summary>
        public List<(int, int)> conditions = new List<(int, int)>();

        /// <summary>
        /// 推断各维的后置变量节点ID或者名称,以及时间项
        /// </summary>
        public List<(int, int)> variables = new List<(int, int)>();
        /// <summary>
        /// 合并前提和后置
        /// </summary>
        /// <returns></returns>
        public List<(int,int)> getDimensions()
        {
            List<(int, int)> r = new List<(int, int)>(conditions);
            r.AddRange(variables);
            return r;

        }

        public override List<int> Dimensions
        {
            get
            {
                return getDimensions().ConvertAll(d => this.owner[d.Item1].Dimension);
            }
        }

        private int comp_dimension((int, int) t1, (int, int) t2)
        {
            if (t1.Item2 > t2.Item2) return -1;
            else if (t1.Item2 < t2.Item2) return 1;
            else
            {
                if (t1.Item1 > t2.Item1) return 1;
                else if (t1.Item1 < t2.Item1) return -1;
                return 0;
            }
        }
        public void sort_dimension()
        {
            this.conditions.Sort(comp_dimension);
            this.variables.Sort(comp_dimension);
        }


        /// <summary>
        /// 后置变量数
        /// </summary>
        public int VariableCount { get => this.variables.Count; }

        /// <summary>
        /// 前置条件数
        /// </summary>
        public int ConditionCount { get => this.conditions.Count; }


        /// <summary>
        /// 得到所有的条件，包括Id和相对时间
        /// </summary>
        /// <returns></returns>
        public List<(int, int)> getConditions()
        {
            return this.conditions;
        }

        /// <summary>
        /// 得到所有的条件Id
        /// </summary>
        /// <returns></returns>
        public List<int> getConditionIds()
        {
            return this.conditions.ConvertAll(c => c.Item1);
        }
        /// <summary>
        /// 得到后置变量Id和相对时间
        /// </summary>
        /// <returns></returns>
        public (int, int) getVariable()
        {
            return this.variables.Count > 0 ? this.variables[0] : (0, 0);
        }

        public List<(int, int)> getVariables()
        {
            return this.variables;
        }

        public List<int> getVariableIds()
        {
            return this.variables.ConvertAll(c => c.Item1);
        }

        public List<int> getConditionsExcludeActionSensor()
        {
            return this.conditions.FindAll(c => !owner[c.Item1].IsActionSensor())
                .ConvertAll(c => c.Item1);
        }

        public List<int> getActionSensorsConditions()
        {
            return this.conditions.FindAll(c => owner[c.Item1].IsActionSensor())
                .ConvertAll(c => c.Item1);
        }

        /// <summary>
        /// 取得输入基因
        /// </summary>
        /// <returns></returns>
        public override List<NodeGene> getInputGenes()
        {
            List<NodeGene> r = new List<NodeGene>();
            if(this.conditions.Count>0)
                r.AddRange(this.conditions.ConvertAll(c=>c.Item1).ConvertAll(c=>owner[c]));
            if(this.variables.Count>0)
                r.AddRange(this.variables.ConvertAll(c => c.Item1).ConvertAll(c => owner[c]));
            return r;
        }

        /// <summary>
        /// 该基因的条件部分全部包含了gene的条件部分
        /// </summary>
        /// <param name="gene"></param>
        /// <returns></returns>
        public bool contains(InferenceGene gene)
        {
            if (gene == null) return false;
            foreach ((int, int) small in gene.conditions)
            {
                int index = this.conditions.ConvertAll(d => d.Item1).IndexOf(small.Item1);
                if (index < 0) return false;
                if (this.conditions[index].Item2 != small.Item2) continue;
            }
            return true;
        }

        #endregion

        #region 初始化和显示
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="genome"></param>
        public InferenceGene(NWSEGenome genome) : base(genome)
        {
        }

        public override T clone<T>()
        {
            InferenceGene gene = new InferenceGene(this.owner).copy<InferenceGene>(this);
            gene.conditions.AddRange(this.conditions);
            gene.variables.AddRange(this.variables);
            return (T)(Object)gene;
        }
        
        /// <summary>
        /// 显示文本
        /// </summary>
        public override String Text
        {
            get
            {
                String s1 = this.getConditions()
                    .ConvertAll(c => c.Item1)
                    .ConvertAll(id => owner[id])
                    .ConvertAll(g => g.Text)
                    .Aggregate((x, y) => x + "," + y);
                String s2 = this.getVariables().ConvertAll(c => c.Item1)
                    .ConvertAll(id => owner[id])
                    .ConvertAll(g => g.Text)
                    .Aggregate((x, y) => x + "," + y);
                return s1 + (s2 == null || s2.Trim() == "" ? "" : "=>" + s2);
            }
        }
        #endregion

        #region 读写
        public override string ToString()
        {
            this.sort_dimension();
            String condstr = this.conditions.ConvertAll(c => c.Item1.ToString()).Aggregate((x, y) => x + "," + y);
            String varstr = this.variables.ConvertAll(c => c.Item1.ToString()).Aggregate((x, y) => x + "," + y);
            String dimensions = condstr + "=>" + varstr;
            return "InferenceGene:" + Text + ";dimensions:"+ dimensions+";info:" + base.ToString() + ";param:";
        }
        public static InferenceGene parse(NWSEGenome genome,String s)
        {
            int t1 = s.IndexOf("InferenceGene:")+ "InferenceGene:".Length;
            int t2 = s.IndexOf(";dimensions:") - 1;
            String text = s.Substring(t1, t2 - t1 + 1).Trim();

            t2 += ";dimensions:".Length+1;
            int t3 = s.IndexOf(";info:")-1;
            String dimensions = s.Substring(t2, t3 - t2 + 1);

            t3 += ";info:".Length;
            int t4 = s.IndexOf(";param:")-1;
            String info = s.Substring(t3, t4 - t3 + 1);

            t3 += ";info:".Length;
            String param = s.Substring(t3);


            //解析dimensions
            int t5 = dimensions.IndexOf("=>");
            String condstr = dimensions.Substring(0, t5);
            String varstr = dimensions.Substring(t5 + 2);
            List<(int, int)> conditions = condstr.Split(',').ToList().ConvertAll(str => int.Parse(str))
                .ConvertAll(v => (v, 1));
            List<(int, int)> variables = varstr.Split(',').ToList().ConvertAll(str => int.Parse(str))
                .ConvertAll(v => (v, 0));
            List<(int, int)> d = new List<(int, int)>(conditions);
            d.AddRange(variables);


            
            //解析info
            InferenceGene gene = new InferenceGene(genome);
            gene.conditions = conditions;
            gene.variables = variables;
            gene.parseInfo(info);

            return gene;
        }

        #endregion

    }
}
