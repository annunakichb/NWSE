using NWSELib.net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NWSELib.genome;
using NWSELib.common;
using Microsoft.ML.Probabilistic.Distributions;

namespace NWSELib.evolution
{
    public class Evolution
    {
        
        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="inds"></param>
        public void execute(List<Network> inds,Session session)
        {
            session.triggerEvent(Session.EVT_LOG, "population evoluting...");
            //1.将无效推理（可靠度小于阈值）的推理节点和有效推理节点向周边传播 
            //  使得周围个体的下一代不会产生无效推理节点，并必然产生有效推理节点
            int invaildCount = 0, vaildCount = 0;
            for (int i = 0; i < inds.Count; i++)
            {
                List<NodeGene> invaildInference = inds[i].findNewInvaildInference();
                if(invaildInference != null && invaildInference.Count>0)
                {
                    invaildInference.ForEach(iinf =>
                        session.triggerEvent(Session.EVT_INVAILD_GENE, inds[i],iinf));
                    invaildCount += invaildInference.Count;
                }
                
                List<NodeGene> vaildInference = inds[i].findNewVaildInferences();
                vaildCount += vaildInference.Count;
                vaildInference.ForEach(vinf => session.triggerEvent(Session.EVT_VAILD_GENE, inds[i],vinf));
                List<EvolutionTreeNode> nearest = EvolutionTreeNode.search(session.getEvolutionRootNode(), inds[i]).getNearestNode();
                nearest.ForEach(node => node.network.Genome.gene_drift(invaildInference, vaildInference));
            }
            session.triggerEvent(Session.EVT_LOG, "count of invaild inf=" + invaildCount.ToString()+",count of vaild inf="+ vaildCount.ToString());

            //2.计算每个个体所有节点平均可靠度
            List<double> reability = inds.ConvertAll(ind => ind.AverageReability);
            session.triggerEvent(Session.EVT_LOG, "reability=" + Utility.toString(reability));

            
            //3.根据所有个体可靠度的均值和方差， 确定淘汰个体的可靠度下限：以可靠度均值和方差构成的高斯分布最大值的
            if (inds.Count >= Session.GetConfiguration().evolution.selection.min_population_capacity)
            {
                int prevcount = inds.Count;
                //计算分位数
                int q = (int)(1 + (reability.Count - 1) * Session.GetConfiguration().evolution.selection.reability_lowlimit);
                List<int> indexes = reability.argsort();
                double reability_lowlimit = reability[indexes[q]];
                List<Network> reversedinds = new List<Network>();
                for(int k=q;k<indexes.Count;k++)
                {
                    reversedinds.Add(inds[indexes[k]]);
                }

                inds.Clear();
                inds.AddRange(reversedinds);
                session.triggerEvent(Session.EVT_LOG, "die out: prev="+prevcount.ToString()+",now="+inds.Count.ToString()+ ",lowlimit of rebility="+ reability_lowlimit.ToString());
            }
            
            //4.计算每个个体所有节点适应度总和所占全部的比例，该比例乘以基数为下一代数量
            int propagate_base_count = inds.Count * Session.GetConfiguration().evolution.propagate_base_count;
            if (propagate_base_count > Session.GetConfiguration().evolution.selection.max_population_capacity)
                propagate_base_count = Session.GetConfiguration().evolution.selection.max_population_capacity;
            List<double> fitnessList = inds.ConvertAll(ind => ind.Fitness);
            for(int i=0;i<fitnessList.Count;i++)
            {
                if (fitnessList[i] == 0) fitnessList[i] = 0.000001;
            }
            double totalFitness = fitnessList.Sum();
            if(totalFitness != 0)
                fitnessList = fitnessList.ConvertAll(f => f / totalFitness);
            List<int> fitnessIndex = fitnessList.argsort();
            fitnessIndex.Reverse();
            int[] planPropagateCount = new int[inds.Count];
            for (int i=0;i< fitnessIndex.Count;i++)
            {
                planPropagateCount[fitnessIndex[i]] = (int)(fitnessList[fitnessIndex[i]] * propagate_base_count);
                if (planPropagateCount.Sum() >= Session.GetConfiguration().evolution.selection.max_population_capacity)
                    break;
            }

            //5.通过变异生成下一代
            List<Network> newinds = new List<Network>();
            for (int i = 0; i < inds.Count; i++)
            {
                EvolutionTreeNode node = EvolutionTreeNode.search(session.getEvolutionRootNode(), inds[i]);
                int childcount = planPropagateCount[i];
                for(int j=0;j<childcount;j++)
                {
                    NWSEGenome mutateGenome = inds[i].Genome.mutate(session);
                    while (inds.Exists(ind => ind.Genome.equiv(mutateGenome)))
                        mutateGenome = inds[i].Genome.mutate(session);
                    Network mutateNet = new Network(mutateGenome);
                    newinds.Add(mutateNet);
                    EvolutionTreeNode cnode = new EvolutionTreeNode(mutateNet, node);
                    node.childs.Add(cnode);

                    session.judgePaused();
                }
            }
            inds.AddRange(newinds);

            session.triggerEvent(Session.EVT_LOG, "population evoluting end");
            session.triggerEvent(Session.EVT_GENERATION_END);

        }
    }
}
