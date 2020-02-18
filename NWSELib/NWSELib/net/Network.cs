
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.ML.Probabilistic.Distributions;
using NWSELib.common;
using NWSELib.env;
using NWSELib.genome;
namespace NWSELib.net
{
    /// <summary>
    /// 网络
    /// </summary>
    public class Network
    {
        #region 基本信息
        public static Random rng = new Random();
        /// <summary>
        /// 染色体
        /// </summary>
        private NWSEGenome genome;
        /// <summary>
        /// 所有节点
        /// </summary>
        private List<Node> nodes = new List<Node>();
        /// <summary>
        /// 邻接矩阵
        /// </summary>
        private double[,] adjMatrix;
        /// <summary>
        /// 染色体
        /// </summary>
        public NWSEGenome Genome { get => genome; }
        /// <summary>
        /// 节点集
        /// </summary>
        public List<Node> Nodes { get => nodes; }
        /// <summary>
        /// Id
        /// </summary>
        /// <returns></returns>
        public int getId() { return this.genome.id; } 
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get => this.genome.id; }
        
        /// <summary>
        /// 策略单元
        /// </summary>
        public Policy policy;
        /// <summary>
        /// 策略
        /// </summary>
        public Imagination imagination;

        public String policyName = "imagination";

        public override string ToString()
        {
            return "net id="+Id.ToString()+",fitness="+
                this.Fitness.ToString("F4")+
                ",reability="+this.Reability.ToString("F4");
        }
        #endregion

        #region 节点查询
        /// <summary>
        /// 所有感知节点
        /// </summary>
        public List<Receptor> Receptors
        {
            get => nodes.FindAll(n => n is Receptor).ConvertAll(n=>(Receptor)n);
        }
        /// <summary>
        /// 所有环境感知节点
        ///</summary>
        public List<Receptor> EnvReceptors
        {
            get => nodes.FindAll(n => n is Receptor && n.Group.StartsWith("env")).ConvertAll(n => (Receptor)n);
        }
        /// <summary>
        /// 所有姿态感知节点
        ///</summary>
        public List<Receptor> GesturesReceptors
        {
            get => nodes.FindAll(n => n is Receptor && n.Group.StartsWith("gestures")).ConvertAll(n => (Receptor)n);
        }
        /// <summary>
        /// 所有动作感知节点
        ///</summary>
        public List<Receptor> ActionReceptors
        {
            get => nodes.FindAll(n => n is Receptor && n.Group.StartsWith("action")).ConvertAll(n=>(Receptor)n);
        }


        /// <summary>
        /// 所有处理节点
        /// </summary>
        public List<Handler> Handlers
        {
            get => nodes.FindAll(n => n is Handler).ConvertAll(n=>(Handler)n);
        }

        public List<Inference> Inferences
        {
            get => nodes.FindAll(n => n is Inference).ConvertAll(n=>(Inference)n);
        }

        /// <summary>
        /// 效应器
        /// </summary>
        public List<Effector> Effectors
        {
            get => nodes.FindAll(n => n is Effector).ConvertAll(n=>(Effector)n);
        }

        

        /// <summary>
        /// 根据节点Id查找节点索引下标
        /// </summary>
        public int idToIndex(int id)
        {
            for (int i = 0; i < this.nodes.Count; i++)
            {
                if (this.nodes[i].Id == id)
                    return i;
            }
            return -1;
        }
        /// <summary>
        /// 根据节点Id的上游连接节点
        /// </summary>
        public List<Node> getInputNodes(int id)
        {
            int index = this.idToIndex(id);
            List<Node> r = new List<Node>();
            for (int i = 0; i < this.nodes.Count; i++)
            {
                if (this.adjMatrix[i, index] != 0)
                    r.Add(this.nodes[i]);
            }
            return r;
        }

        public Node getNode(int id)
        {
            return this.nodes.FirstOrDefault(n => n.Id == id);
        }
        public Node getNode(String name)
        {
            return this.nodes.FirstOrDefault(n => n.Name == name);
        }

        public Node this[Object idorname]
        {
            get 
            {
                if (idorname is int) return getNode((int)idorname);
                else if(idorname is String)return getNode((String)idorname);
                return null;
            }
        }
        public int getActionIdByName(String name)
        {
            Node n = this.Effectors.FirstOrDefault(e => e.Name == name || e.Name == name.Substring(1));
            return n == null ? 0 : n.Id;
        }

        public Effector getEffectorByActionSensorId(int id)
        {
            String name = this.getNode(id).Name;
            return (Effector)this.Effectors.FirstOrDefault(e => e.Name == name || e.Name == name.Substring(1));

        }
        public List<Node> getAcceptNodes()
        {
            List<Node> nodes = new List<Node>();
            nodes.AddRange(this.Receptors);
            nodes.AddRange(this.Handlers);
            return nodes;
        }
        #endregion

        #region 初始化
       
        /// <summary>
        /// 重置计算
        /// </summary>
        public void Reset()
        {
            this.nodes.ForEach(a => a.Reset());
            this.nodes.ForEach(n => n.think_reset());
        }

        public void thinkReset()
        {
            this.nodes.ForEach(n => n.think_reset());
        }


        /// <summary>
        /// 构造函数
        /// </summary>
        public Network(NWSEGenome genome)
        {
            this.genome = genome;
            //初始化节点
            for (int i = 0; i < genome.receptorGenes.Count; i++)
            {
                Receptor receptor = new Receptor(genome.receptorGenes[i],this);
                this.nodes.Add(receptor);
            }
            for (int i = 0; i < genome.handlerGenes.Count; i++)
            {
                Handler handler = Handler.create(genome.handlerGenes[i],this);
                this.nodes.Add(handler);
            }
            for (int i = 0; i < genome.infrernceGenes.Count; i++)
            {
                Inference inference = new Inference(genome.infrernceGenes[i],this);
                this.nodes.Add(inference);
            }
            for (int i = 0; i < this.ActionReceptors.Count; i++)
            {
                Effector effector = new Effector(((ReceptorGene)this.ActionReceptors[i].Gene).toActionGene(),this);
                this.nodes.Add(effector);
            }

            //构造连接矩阵
            this.adjMatrix = new double[this.nodes.Count, this.nodes.Count];
            for (int i = 0; i < genome.handlerGenes.Count; i++)
            {
                int k1 = idToIndex(genome.handlerGenes[i].Id);
                for(int j=0;j< genome.handlerGenes[i].inputs.Count;j++)
                {
                    int k2 = idToIndex(genome.handlerGenes[i].inputs[j]);
                    this.adjMatrix[k2, k1] = 1;
                }
            }
            for (int i = 0; i < genome.infrernceGenes.Count; i++)
            {
                int k1 = idToIndex(genome.infrernceGenes[i].Id);
                for (int j = 0; j < genome.infrernceGenes[i].getDimensions().Count; j++)
                {
                    int k2 = idToIndex(genome.infrernceGenes[i].getDimensions()[j].Item1);
                    this.adjMatrix[k2, k1] = 1;
                }
            }

            actionPlanChain = new ActionPlanChain(this);
            actionMemory = new ObservationHistory(this);
            policy = new Policy(this);
            imagination = new Imagination(this);

        }



        #endregion

        #region 状态
        /** 最后一次获得的奖励 */
        public double reward;
        /// <summary>
        /// 行动记忆空间
        /// </summary>
        public ObservationHistory actionMemory;
        /// <summary>
        /// 行动计划链
        /// </summary>
        public ActionPlanChain actionPlanChain;
        
        #endregion

        #region 评价信息
        protected double fitness = double.NaN;
        /// <summary>
        /// 适应度
        /// </summary>
        public double Fitness { get => fitness; set => fitness = value; }
        
        /// <summary>
        /// 网络可靠度
        /// </summary>
        public double Reability
        {
            get 
            {
                List<double> r = this.Inferences.ConvertAll(inf=>inf.Reability).FindAll(reability => !double.IsNaN(reability));
                if (r == null || r.Count <= 0) return double.NaN;
                return r.Average();
            }
        }

        public List<NodeGene> findNewVaildInferences()
        {
            double highlimit = Session.GetConfiguration().evaluation.gene_reability_range.Max;
            List<NodeGene> genes = this.Inferences.FindAll(n => !double.IsNaN(n.Reability) && n.Reability > highlimit).ConvertAll(n => n.Gene);
            if (genes == null || genes.Count <= 0) return genes;

            for (int i = 0; i < genes.Count; i++)
            {
                NodeGene gene = genes[i];
                if (this.genome.isVaildGene(gene))
                {
                    genes.RemoveAt(i--); continue;
                }
            }
            if (genes == null || genes.Count <= 0) return genes;

            for (int i=0;i<genes.Count;i++)
            {
                NodeGene gene = genes[i];
                List<NodeGene> temp = gene.getUpstreamGenes();
                if (temp == null || temp.Count<=0) continue;
                foreach(NodeGene t in temp)
                {
                    if (t is ReceptorGene) continue;
                    else if (genes.Contains(t)) continue;
                    genes.Add(t);
                }
            }

            return genes;
        }

        public List<NodeGene> findNewInvaildInference()
        {
            double lowlimit = Session.GetConfiguration().evaluation.gene_reability_range.Min;
            List < NodeGene > r=  this.Inferences.FindAll(n => !double.IsNaN(n.Reability) && n.Reability != 0 && n.Reability < lowlimit).ConvertAll(n=>n.Gene);
            if (r == null || r.Count <= 0) return r;
            for(int i=0;i<r.Count;i++)
            {
                if(this.genome.isInvaildGene(r[i]))
                {
                    r.RemoveAt(i--);
                }
            }
            return r;
        }
        #endregion


        #region 网络活动
        /// <summary>
        /// 激活
        /// </summary>
        /// <param name="obs"></param>
        /// <returns></returns>
        public List<double> activate(List<double> obs, int time,Session session,double reward)
        {
            
            //1.接收输入
            for (int i = 0; i < obs.Count; i++)
            {
                this.Receptors[i].activate(this, time, obs[i]);
            }

            //2.处理感知
            while (!this.Handlers.All(n => n.IsActivate(time)))
            {
                this.Handlers.ForEach(n => n.activate(this, time, null));
            }

            
            //3. 记忆整理
            for (int i=0;i<this.Inferences.Count;i++)
            {
                Inference inf = this.Inferences[i];
                inf.activate(this, time);
            }

            //4. 对现有推理记录的准确性进行评估
            for (int i = 0; i < this.Inferences.Count; i++)
            {
                Inference inf = this.Inferences[i];
                inf.Records.ForEach(r => r.adjustAccuracy(time));
                inf.removeWrongRecords();
            }

            //5. 行为决策   
            this.reward = reward;
            if (this.actionPlanChain.Last != null)
                this.actionPlanChain.Last.reward = reward;
            if(policyName == "policy")
                policy.doEvaluation(time, session);
            else
                imagination.doImagination(time, session);
            //        
            setEffectValue(time);

            //6.记录行为
            List<double> actions = this.Effectors.ConvertAll<double>(n => (double)n.Value);
            for(int i=0;i< this.ActionReceptors.Count;i++)
            {
                this.ActionReceptors[i].activate(this, time, actions[i]);
            }
           
            return actions;
        }
        /// <summary>
        /// 根据行动计划设定输出

        /// </summary>
        private void setEffectValue(int time, bool random = true)
        {
            for (int i = 0; i < this.Effectors.Count; i++)
            {
                this.Effectors[i].activate(this, time, this.actionPlanChain.Last.actions[i]);
            }
        }

        public List<Vector> forward_inference(List<Vector> obs = null)
        {
            if (obs == null)
                obs = this.GetReceoptorValues();

            //准备新的观察数据，初始值都是null
            Vector[] newObs = new Vector[obs.Count];
            //新的观察数据中的动作部分用原观察替换
            List<Receptor> receptors = this.Receptors;
            int[] actionIndex = this.ActionReceptors.ConvertAll(ar => receptors.IndexOf(ar)).ToArray();
            actionIndex.ToList().ForEach(i => newObs[i] = obs[i].clone());

            //对每一个推理节点执行前向推理
            foreach(Inference inf in this.Inferences)
            {
                int[] condIndex = inf.getGene().getConditionIds()
                                   .ConvertAll(id => (Receptor)this[id])
                                   .ConvertAll(node => receptors.IndexOf(node))
                                   .ToArray();
                int[] varIndex = inf.getGene().getVariableIds()
                                   .ConvertAll(id => (Receptor)this[id])
                                   .ConvertAll(node => receptors.IndexOf(node))
                                   .ToArray();
                List <Vector> condValues = condIndex.ToList().ConvertAll(index=>obs[index]);
                List<Vector> varValues = inf.forward_inference(condValues).Item2;
                for(int i=0;i< varValues.Count;i++)
                {
                    if (varIndex[i] == -1) continue;
                    newObs[varIndex[i]] = varValues[i].clone();
                }
            }

            return newObs.All(v => v != null) ? newObs.ToList() : null;

        }
        public List<InferenceRecord> GetMatchInfRecords(List<Vector> envValues,List<double> actions, int time)
        {
            return GetMatchInfRecords(this.GetMergeReceptorValues(envValues, actions), time);
        }
        public List<InferenceRecord> GetMatchInfRecords(List<Vector> observations,int time)
        {
            this.thinkReset();
            //初始化感知节点
            List<Receptor> receptors = this.Receptors;
            for (int i = 0; i < receptors.Count; i++)
            {
                receptors[i].think(this, time, observations[i]);
            }

            //激活处理节点
            List<Handler> handlers = this.Handlers;
            while (handlers != null && !handlers.All(n => n.IsThinkCompleted(time)))
            {
                handlers.ForEach(n => n.think(this, time, null));
            }

            

            List<Inference> inferences = this.Inferences;
            List<InferenceRecord> results = new List<InferenceRecord>();
            foreach (Inference inte in inferences)
            {
                //得到条件值
                List<Vector> condValues = inte.getGene().getConditionIds().ConvertAll(id => this[id]).ConvertAll(node => node.getThinkValues(time));
                //条件值不全
                if (condValues.Exists(v => v == null)) continue;
                var match = inte.getMatchRecord(this, condValues, time);
                if (match.Item1 == null) continue;
                results.Add(match.Item1);
            }
            return results;
        }
        #endregion

        #region 观察空间管理
        /// <summary>
        /// 取得当前外部观察
        /// </summary>
        /// <returns></returns>
        public List<Vector> GetReceoptorValues()
        {
            return this.Receptors.ConvertAll(r => r.Value.clone());
        }

        public List<Vector> GetReceptorSceneValues()
        {
            return 
                this.Receptors.FindAll(r => !r.Gene.IsActionSensor()).ConvertAll(r => r.Value.clone());
        }
        /// <summary>
        /// 分非动作和动作取得外部观察值
        /// </summary>
        /// <returns></returns>
        public (List<Vector> scene,List<double> actions) GetSplitReceptorValues()
        {
            return (
                this.Receptors.FindAll(r => !r.Gene.IsActionSensor()).ConvertAll(r => r.Value.clone()),
                this.Receptors.FindAll(r => r.Gene.IsActionSensor()).ConvertAll(r => r.Value==null?double.NaN:r.Value[0])
                );
        }

        public List<Vector> GetMergeReceptorValues(List<Vector> envValues,List<double> actions)
        {
            int aIndex = 0, eIndex = 0;
            List<Vector> observations = new List<Vector>();
            List<Receptor> receptors = this.Receptors;
            for (int i = 0; i < receptors.Count; i++)
            {
                if (receptors[i].Gene.IsActionSensor())
                    observations.Add(new Vector(actions[aIndex++]));
                else
                    observations.Add(envValues[eIndex++]);
            }
            return observations;
        }
        /// <summary>
        /// 计算两个外部观察的距离
        /// </summary>
        /// <param name="obs1"></param>
        /// <param name="obs2"></param>
        /// <returns></returns>
        public List<double> GetReceptorDistance(List<Vector> obs1, List<Vector> obs2)
        {
            List<double> r = new List<double>();
            for (int i = 0; i < Receptors.Count; i++)
            {
                r.Add(Receptors[i].distance(obs1[i][0],obs2[i][0]));
            }
            return r;
        }

        public List<double> GetReceptorDistance(List<Receptor> receptors,Vector obs1, Vector obs2)
        {
            List<double> r = new List<double>();
            for (int i = 0; i < receptors.Count; i++)
            {
                r.Add(receptors[i].distance(obs1[i], obs2[i]));
            }
            return r;
        }

        /// <summary>
        /// 创建一组新的观察，将其中动作部分移除
        /// </summary>
        /// <param name="obs"></param>
        /// <returns></returns>
        public List<Vector> RemoveActionFromReceptor(List<Vector> obs)
        {
            List<Receptor> receptors = this.Receptors;
            int[] actionIndex = this.ActionReceptors.ConvertAll(ar => receptors.IndexOf(ar)).ToArray();

            List<Vector> r = new List<Vector>();
            for(int i=0;i<obs.Count;i++)
            {
                if (actionIndex.Contains(i)) continue;
                r.Add(obs[i]);
            }
            return r;
        }
        /// <summary>
        /// 将观察中的行为部分用计划部分替换
        /// </summary>
        /// <param name="curObs"></param>
        /// <param name="plan"></param>
        /// <returns></returns>
        public List<Vector> ReplaceWithAction(List<Vector> curObs, List<double> actions)
        {
            int index = 0;
            for (int i = 0; i < Receptors.Count; i++)
            {
                if (Receptors[i].Gene.IsActionSensor())
                {
                    curObs[i] = new Vector(actions[index++]);
                }
            }
            return curObs;
        }
        /// <summary>
        /// 将当前观察中的行为部分替换为维持行动
        /// </summary>
        /// <param name="curObs"></param>
        /// <returns></returns>
        public List<Vector> ReplaceMaintainAction(List<Vector> curObs)
        {
            for (int i = 0; i < Receptors.Count; i++)
            {
                if (Receptors[i].Gene.IsActionSensor())
                {
                    curObs[i] = new Vector(0.5);
                }
            }
            return curObs;
        }

        /// <summary>
        /// 创建随机动作
        /// </summary>
        /// <param name="randomType"></param>
        /// <returns></returns>
        public List<double> CreateRandomActions(String randomType="uniform")
        {
            return this.Receptors.FindAll(r => r.getGene().IsActionSensor())
                .ConvertAll(r => r.randomValue(randomType));
        }

        #endregion

        
        #region 保存和读取
        public void save(String path,int generation)
        {
            String filename = path + "\\" + this.Id.ToString() + "_" +
            generation.ToString()+"_"+this.Fitness.ToString("F5") + ".ind";

            File.WriteAllText(filename, this.genome.ToString());
        }
        public static Network load(String filename)
        {
            String text = File.ReadAllText(filename);
            if (text == null || text.Trim() == "") return null;
            NWSEGenome genome = NWSEGenome.parse(text);
            return new Network(genome);
        }
        #endregion
    }
}
