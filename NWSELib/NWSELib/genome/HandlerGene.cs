using System;
using System.Collections.Generic;
using System.Linq;

using NWSELib.common;

namespace NWSELib.genome
{
    /// <summary>
    /// 处理节点基因
    /// </summary>
    public class HandlerGene : NodeGene
    {
        #region 基本信息
        /// <summary>
        /// 处理功能
        /// </summary>
        public readonly String function;
        /// <summary>
        /// 输入节点基因id，其中可能会有重复
        /// </summary>
        public List<int> inputs = new List<int>();
        /// <summary>
        /// 处理参数，依据function不同而不同
        /// </summary>
        public List<double> param = new List<double>();

        public override List<int> Dimensions
        {
            get
            {
                List<NodeGene> inputs = this.inputs.ConvertAll(i => owner[i]);
                return inputs.ConvertAll(e => e.Dimension);
            }
        }

        /// <summary>
        /// 取得输入基因
        /// </summary>
        /// <returns></returns>
        public override List<NodeGene> getInputGenes()
        {
            return this.inputs.ConvertAll(i => owner[i]);
        }

        public HandlerGene mutate()
        {
            HandlerGene gene = this.clone<HandlerGene>();
            NodeGene g = owner[gene.inputs[0]];
            int num = 0, maxcount = 5;
            while(++num<=maxcount)
            {
                List<NodeGene> gs = null;
                if (num <= maxcount / 2) gs = owner.filterByCataory(owner.receptorGenes.ConvertAll(r => (NodeGene)r), g.Cataory);
                else gs = owner.filterByCataory(owner.getReceptorAndHandlerGenes(), g.Cataory);
                int index = rng.Next(0, gs.Count);
                if (gene.inputs.Contains(gs[index].Id)) continue;
                gene.inputs[0] = gs[index].Id;
                gene.sortInput();
                gene.Id = Session.idGenerator.getGeneId(gene);
                return gene;
            }
            return null;
        }

        #endregion

        #region 显示和初始化
        public HandlerGene(NWSEGenome genome, String function, List<int> inputs, params double[] ps) : base(genome)
        {
            this.function = function;
            this.inputs.AddRange(inputs);
            if (ps != null || ps.Length <= 0) param.AddRange(ps);
        }

        /// <summary>
        /// 显示文本
        /// </summary>
        public override String Text
        {
            get
            {
                List<NodeGene> inputs = this.inputs.ConvertAll(i => owner[i]);
                inputs.Sort();
                //return function + "(" + inputs.ConvertAll(x => x.Text).Aggregate((m, n) =>"{" + m + "}"+ ",{" + n + "}") + ")";
                return function + "(" + inputs.ConvertAll(x => x.Text).Aggregate((m, n) => m + "," + n) + ")";
            }
        }


        public override T clone<T>()
        {
            return new HandlerGene(this.owner, this.function, this.inputs, this.param.ToArray()).copy<T>(this);
        }



        public void sortInput()
        {
            this.inputs.Sort();
        }

        #endregion

        #region 读写

        /// <summary>
        /// 转换字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "HandlerGene:" + Text + ";info:" + base.ToString() + ";function=" + this.function +
                ";inputs=" + Utility.toString(this.inputs) +
                ";param=" + Utility.toString(param);
        }


        public static HandlerGene parse(NWSEGenome genome, String s)
        {
            int t1 = s.IndexOf("info");
            int t2 = s.IndexOf(":",t1+1);
            int t3 = s.IndexOf(";", t2 + 1);
            String info = s.Substring(t2 + 1, t3 - t2 - 1);

            //解析param部分
            t1 = s.IndexOf("function");
            t2 = s.IndexOf("=", t1 + 1);
            t3 = s.IndexOf(";", t2 + 1);
            String function = s.Substring(t2 + 1, t3 - t2-1).Trim();

            t1 = s.IndexOf("inputs");
            t2 = s.IndexOf("=", t1 + 1);
            t3 = s.IndexOf(";", t2 + 1);
            String inputstr = s.Substring(t2 + 1, t3 - t2 - 1).Trim();
            List<int> inputids = Utility.parse<int>(inputstr);
            //解析info
            HandlerGene gene = new HandlerGene(genome, function, inputids, null);
            gene.parseInfo(info);
            //解析参数
            t1 = s.IndexOf("param");
            t2 = s.IndexOf("=", t1 + 1);
            String paramtext = s.Substring(t2 + 1, s.Length - t2 - 1).Trim();
            gene.param = Utility.parse<double>(paramtext);

            return gene;
        }
        
        #endregion
    }

    
}
