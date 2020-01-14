﻿using NWSELib.net.handler;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NWSELib.genome
{
    /// <summary>
    /// 染色体工厂
    /// </summary>
    public class NWSEGenomeFactory
    {
        private List<ReceptorGene> createReceptors(NWSEGenome genome,Session session)
        {
            
            List<Configuration.Sensor> sensors = Session.GetConfiguration().agent.receptors.GetAllSensor();
            for (int i = 0; i < sensors.Count; i++)
            {
                ReceptorGene receptorGene = new ReceptorGene(genome);
                receptorGene.Cataory = sensors[i].cataory;
                receptorGene.Generation = session.Generation;
                receptorGene.Group = sensors[i].group;
                receptorGene.Name = sensors[i].name;
                receptorGene.SectionCount = (int)sensors[i].Level.random();
                receptorGene.Id = session.GetIdGenerator().getGeneId(receptorGene);
                genome.receptorGenes.Add(receptorGene);
            }
            return genome.receptorGenes;

        }
        public NWSEGenome createSimpleGenome(Session session)
        {
            NWSEGenome genome = new NWSEGenome();
            //生成感受器
            this.createReceptors(genome,session);


            //生成缺省推理节点
            InferenceGene inferenceGene = new InferenceGene(genome);
            inferenceGene.Generation = session.Generation;


            inferenceGene.dimensions = new List<(int, int)>();
            inferenceGene.dimensions.Add((genome["d1"].Id, 1));
            inferenceGene.dimensions.Add((genome["d2"].Id, 1));
            inferenceGene.dimensions.Add((genome["d3"].Id, 1));
            inferenceGene.dimensions.Add((genome["d4"].Id, 1));
            inferenceGene.dimensions.Add((genome["d5"].Id, 1));
            inferenceGene.dimensions.Add((genome["d6"].Id, 1));
            inferenceGene.dimensions.Add((genome["_a2"].Id, 1));

            inferenceGene.dimensions.Add((genome["d3"].Id, 0));
            inferenceGene.sort_dimension();
            inferenceGene.Id = Session.idGenerator.getGeneId(inferenceGene);
            genome.infrernceGenes.Add(inferenceGene);



            //生成判定基因

            JudgeGene judgeItem = new JudgeGene(genome);
            judgeItem.conditions.Add(genome["_a2"].Id);
            judgeItem.variable = genome["d3"].Id;
            judgeItem.expression = JudgeGene.ARGMAX;
            judgeItem.weight = 1.0;
            judgeItem.Generation = session.Generation;
            judgeItem.Id = Session.idGenerator.getGeneId(judgeItem);
            genome.judgeGenes.Add(judgeItem);

            genome.id = Session.idGenerator.getGenomeId();
            genome.computeNodeDepth();
            return genome;
        }
        public NWSEGenome createDemoGenome(Session session)
        {
            NWSEGenome genome = new NWSEGenome();
            //生成感受器
            this.createReceptors(genome, session);

            //生成处理节点
            String[] inputnames = { "d1", "d2", "d3", "d4", "d5", "d6" };
            List<int> ids = inputnames.ToList().ConvertAll(n => genome[n].Id);
            HandlerGene handlerGene = new HandlerGene(genome, "argmax",ids);
            handlerGene.Cataory = genome["d3"].Cataory;
            handlerGene.Generation = session.Generation;
            handlerGene.Group = genome["d3"].Group;
            handlerGene.Id = session.GetIdGenerator().getGeneId(handlerGene);
            handlerGene.Name = handlerGene.Text;
            genome.handlerGenes.Add(handlerGene);

            InferenceGene inferenceGene = null;
            //生成推理节点:1 
            /*inferenceGene = new InferenceGene(genome);
            inferenceGene.Generation = session.Generation;
            inferenceGene.dimensions = new List<(int, int)>();
            inferenceGene.dimensions.Add((genome["d3"].Id, 1));
            inferenceGene.dimensions.Add((genome["_a1"].Id, 1));
            inferenceGene.dimensions.Add((genome["d3"].Id, 0));
            inferenceGene.sort_dimension();
            inferenceGene.Id = Session.idGenerator.getGeneId(inferenceGene);
            genome.infrernceGenes.Add(inferenceGene);*/
            //生成推理节点:2
            /*inferenceGene = new InferenceGene(genome);
            inferenceGene.Generation = session.Generation;
            inferenceGene.dimensions = new List<(int, int)>();
            inferenceGene.dimensions.Add((genome["d3"].Id, 1));
            inferenceGene.dimensions.Add((handlerGene.Id, 1));
            inferenceGene.dimensions.Add((genome["_a2"].Id, 1));
            inferenceGene.dimensions.Add((genome["d3"].Id, 0));
            inferenceGene.sort_dimension();
            inferenceGene.Id = Session.idGenerator.getGeneId(inferenceGene);
            genome.infrernceGenes.Add(inferenceGene);*/

            //生成推理节点:3
            inferenceGene = new InferenceGene(genome);
            inferenceGene.Generation = session.Generation;
            inferenceGene.dimensions = new List<(int, int)>();
            inferenceGene.dimensions.Add((genome["pos"].Id, 1));
            inferenceGene.dimensions.Add((genome["heading"].Id, 1));
            inferenceGene.dimensions.Add((genome["d3"].Id, 1));
            //inferenceGene.dimensions.Add((genome["_a1"].Id, 1));
            inferenceGene.dimensions.Add((genome["_a2"].Id, 1));
            inferenceGene.dimensions.Add((genome["pos"].Id, 0));
            inferenceGene.dimensions.Add((genome["heading"].Id, 0));
            inferenceGene.dimensions.Add((genome["d3"].Id, 0));
            inferenceGene.sort_dimension();
            inferenceGene.Id = Session.idGenerator.getGeneId(inferenceGene);
            genome.infrernceGenes.Add(inferenceGene);

            //生成推理节点:3
            /*InferenceGene  inferenceGene = new InferenceGene(genome);
            inferenceGene.Generation = session.Generation;
            inferenceGene.dimensions = new List<(int, int)>();
            inferenceGene.dimensions.Add((genome["pos"].Id, 1));
            inferenceGene.dimensions.Add((genome["heading"].Id, 1));
            inferenceGene.dimensions.Add((genome["_a1"].Id, 1));
            inferenceGene.dimensions.Add((genome["_a2"].Id, 1));
            inferenceGene.dimensions.Add((genome["pos"].Id, 0));
            inferenceGene.sort_dimension();
            inferenceGene.Id = Session.idGenerator.getGeneId(inferenceGene);
            genome.infrernceGenes.Add(inferenceGene);*/

            genome.id = Session.idGenerator.getGenomeId();
            genome.computeNodeDepth();
            return genome;
        }
    }
}
