
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
        /// 抽象思维
        /// </summary>
        public Imagination imagination;

        public override string ToString()
        {
            return "net id="+Id.ToString()+",fitness="+
                this.Fitness.ToString("F4")+
                ",reability="+this.AverageReability.ToString("F4");
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
        private double eplison = 1.0;
        /// <summary>
        /// 重置计算
        /// </summary>
        public void Reset()
        {
            this.nodes.ForEach(a => a.Reset());
            this.nodes.ForEach(n => n.think_reset());
            this.Inferences.ForEach(inf =>
                inf.Records.ForEach(r => r.CachedCondNodes = null)
            ) ;
            eplison = 1.0;
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

            imagination = new Imagination(this);
        }



        #endregion

        #region 状态
        

        /// <summary>
        /// 行动轨迹
        /// </summary>
        public List<ActionPlan> actionPlanTraces = new List<ActionPlan>();

        /// <summary>
        /// 最后行动计划
        /// </summary>
        public ActionPlan lastActionPlan
        {
            get
            {
                return actionPlanTraces.Count <= 0 ? null : actionPlanTraces[actionPlanTraces.Count - 1];
            }
        }
        /// <summary>
        /// 取得某个时间的行动计划
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public ActionPlan getActionPlan(int time)
        {
            return actionPlanTraces.FirstOrDefault(ap => ap.judgeTime == time);
        }


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
        public double AverageReability
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

            //3. 对现有推理记录的准确性进行评估
            for (int i = 0; i < this.Inferences.Count; i++)
            {
                Inference inf = this.Inferences[i];
                inf.Records.ForEach(r => r.adjustAccuracy(time));
                
            }
            //4. 记忆整理
            for (int i=0;i<this.Inferences.Count;i++)
            {
                Inference inf = this.Inferences[i];
                inf.activate(this, time);
            }
            //5. 归纳
            if (Session.GetConfiguration().learning.imagination.abstractLevel > 0)
                imagination.doAbstract();
            else
                imagination.inferences = new List<Inference>(this.Inferences);
            //imagination.inferences = this.Inferences;

            //6. 推理
            //imagination.doInference(time, session);

            //7. (上一次)行为评估
            this.setReward(reward,time,1);

            //8. 推理想象、行为决策
            ActionPlan plan = null;
            eplison = time == 0 ? 1.0 : 1 / Math.Sqrt(time);
            if (rng.NextDouble() <= eplison)
            {
                plan = createDefaultPlan(time);
            }
            else 
            {
                plan = imagination.doImagination(time, session);
                if (plan == null) plan = createDefaultPlan(time);
            }
            this.actionPlanTraces.Add(plan);
            setEffectValue(time);

            //9.记录行为
            List<double> actions = this.Effectors.ConvertAll<double>(n => (double)n.Value);
            for(int i=0;i< this.ActionReceptors.Count;i++)
            {
                this.ActionReceptors[i].activate(this, time, actions[i]);
            }
           
            return actions;
        }

        #region 回忆和推理
        public ActionPlan createDefaultPlan(int time)
        {
            ActionPlan plan = new ActionPlan();
            if(rng.NextDouble()<=0.0)
            {
                plan.actions = Session.instinctActionHandler(this, time);
                plan.judgeTime = time;
                plan.judgeType = ActionPlan.JUDGE_INSTINCT;
            }else
            {
                plan.actions = Effectors.ConvertAll(e=>e.randomValue(this,time));
                plan.judgeTime = time;
                plan.judgeType = ActionPlan.JUDGE_RANDOM;
            }
            int index = 0;
            plan.inputObs = this.Receptors.ConvertAll(r => r.Gene.IsActionSensor()? new Vector(plan.actions[index++]):r.Value);

            return plan;
        }
        public String showActionPlan()
        {
            if (lastActionPlan == null) return "";
            StringBuilder str = new StringBuilder();
            str.Append("行动方式="+ lastActionPlan.judgeType + System.Environment.NewLine);
            str.Append("   推理准则="+ lastActionPlan.mode + System.Environment.NewLine);
            str.Append("   评估值=" + lastActionPlan.evluation.ToString("F3") + System.Environment.NewLine);
            str.Append("   行为=");
            str.Append(showActionText() + System.Environment.NewLine);

            str.Append(System.Environment.NewLine);
            return str.ToString();
        }
        public String showActionText()
        {
            return this.Effectors.ConvertAll(e => e.getValueText()).Aggregate((x, y) => x + "," + y); 
        }
        /// <summary>
        /// 根据行动计划设定输出
        /// //没有行动计划,两种原因导致：1）没有找到相似场景；2）行动计划的最大评估值太小,相当于处于困境
            //对于前一种，可以执行本能行为，后一种则执行随机行为
        /// </summary>
        private void setEffectValue(int time,bool random=true)
        {
            for (int i = 0; i < this.Effectors.Count; i++)
            {
                //Gussian gau = Gaussian.FromMeanAndVariance(this.lastActionPlan.actions[i],0.01);
                this.Effectors[i].activate(this, time, this.lastActionPlan.actions[i]);
                //this.Effectors[i].activate(this, time, gau.Sample());
            }
        }

        
        
        /// <summary>
        /// 取得特定id的最新值
        /// </summary>
        /// <param name="inf"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public List<Vector> getValues(List<int> ids)
        {
            return ids.ConvertAll(id => this.getNode(id).Value);
        }

        public (Vector value, List<Node> nodes) flattenValues(List<int> ids, List<Vector> values)
        {
            Vector rv = new Vector(true, values.size());
            List<Node> rn = new List<Node>();

            int index = 0;
            for (int i=0;i<ids.Count;i++)
            {
                Node node = this[ids[i]];
                (List<double> vs, List<Node> nodes) = flattenValues(node, values[i]);
                for(int j=0;j<vs.Count;j++)
                {
                    rv[index++]=vs[j];
                    rn.Add(nodes[j]);
                }
            }
            return (rv, rn);
            
        }
        /// <summary>
        /// 将某个节点的值分解到最小的输入double值,以及每个值的对应基础节点
        /// </summary>
        /// <param name="node"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public (List<double>,List<Node>) flattenValues(Node node,Vector values, List<double> v=null, List<Node> n=null)
        {
            if (v == null) v = new List<double>();
            if (n == null) n = new List<Node>();

            List<Node> childs = node.getInputNodes(this);
            if(childs == null || childs.Count<=0)
            {
                v.Add(values[v.Count]);
                n.Add(node);
            }
            for(int i=0;i<childs.Count;i++)
            {
                (v, n) = flattenValues(childs[i], values, v, n);
            }
            return (v, n);
        }

        
        public double getRankedValue(Receptor r, double value,int abstractLevel)
        {
            if (abstractLevel == 0) return value;

            int sectionCount = r.getGene().AbstractSectionCount;
            if (sectionCount <= 0) return value;

            return MeasureTools.GetMeasure(r.Cataory).getRankedValue(value, abstractLevel, sectionCount);
        }


        
        public List<Vector> getRankedValues(Node node,List<Vector> orginValues,int abstractLevel)
        {
            (Vector flattenValue,List<int> dSize) = orginValues.flatten();
            double[] rankedValues = new double[flattenValue.Size];
            
            List<Receptor> receptors = node.Gene.getLeafGenes().ConvertAll(g=>(Receptor)this[g.Id]);
            for(int i = 0;i< receptors.Count;i++)
            {
                double d = this.getRankedValue(receptors[i], flattenValue[i], abstractLevel);
                rankedValues[i] = d;
            }
            return new Vector(rankedValues).split(dSize); 
        }
        
        
        /// <summary>
        /// 取得inference接续的推理节点
        /// 要求是inference的后置变量部分完全包含了其他推理的前置条件部分（动作感知除外）
        /// </summary>
        /// <param name="inference"></param>
        /// <returns></returns>
        public List<Inference> getNextInferences(Inference inference)
        {
            List<int> postVarIds = inference.getGene().getVariableIds();
            List<Inference> r = new List<Inference>();

            for (int i=0;i<this.Inferences.Count;i++)
            {
                if (this.Inferences[i] == inference) continue;
                List<int> varCondIds = ((Inference)this.Inferences[i]).getGene().getConditionsExcludeActionSensor();
                if (Utility.ContainsAll(postVarIds, varCondIds))
                    r.Add((Inference)this.Inferences[i]);
            }
            return r;
        }

        
        /// <summary>
        /// 因为inference的结果results包含了nextinf的所有输入（动作除外）
        /// 将这些输入提取出来
        /// </summary>
        /// <param name="inference">推理</param>
        /// <param name="results">结果</param>
        /// <param name="nextinf"></param>
        /// <returns>只是环境输入部分</returns>
        public List<Vector> computeInput(Inference inference, List<Vector> results, Inference nextinf)
        {
            List<int> infVarIds = inference.getGene().getVariableIds();
            if (infVarIds.Count != results.Count) return null;
            List<int> nextinfcondIds = nextinf.getGene().getConditionsExcludeActionSensor();
            List<Vector> r = new List<Vector>();
            for(int i=0;i< nextinfcondIds.Count;i++)
            {
                int index = infVarIds.IndexOf(nextinfcondIds[i]);
                if (index < 0) return null;
                r.Add(results[index]);
            }
            return r;
        }

        /// <summary>
        /// 取得推理节点输出部分(后置变量)的值
        /// 只要有一个输出在time处没有值，都将返回null
        /// </summary>
        /// <param name="inference"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public List<Vector> getOutputValues(Inference inference, int time)
        {
            List<int> varIds = inference.getGene().getVariableIds();
            List<Vector> vs = varIds.ConvertAll(id => this.getNode(id).GetValue(time));
            return vs.Contains(null) ? null : vs;
        }

        public const int REWARD_EXP = 1;
        public const int REWARD_MEAN = 2;
        public const int REWARD_ONCE = 3;

        public void setReward2(double reward, int time, int mode = 1, bool clear = true)
        {
            if (reward == 0) return;
            if (reward == 100.0) mode = 3;//如果是摆脱撞墙，这个正向奖励不传播

            List<InferenceRecord> rs = new List<InferenceRecord>();
            foreach (Inference inf in imagination.inferences) 
            {
                List<InferenceRecord> records = inf.getVariableMatchRecords(this,time);
                if (records == null || records.Count <= 0) continue;
                rs.AddRange(records);
            }
            if (rs.Count <= 0) return;
            propagateReward(reward,time, 0, rs, mode);
        }
        private void propagateReward(double reward, int time,int level, List<InferenceRecord> records,int mode = 1)
        {
            //检查传播终止条件
            if (records == null || records.Count <= 0) return;
            if (records.All(r => !r.inf.getGene().hasEnvDenpend())) return;
            
            //计算奖励并分配
            double rr = reward;
            if (mode == 1) rr = Math.Exp(-level) * reward;
            records.ForEach(r => { if (r.inf.getGene().hasEnvDenpend()) r.evulation += rr; });
            if (mode == 3) return;
            if (level >= 5) return;

            //反向传播
            List<InferenceRecord> nextRecords = new List<InferenceRecord>();
            for(int i=0;i<records.Count;i++)
            {
                List<Inference> infs = this.getInverseMatchInference(records[i].inf,this.imagination.inferences);
                if (infs == null || infs.Count <= 0) continue;
                foreach(Inference nextInf in infs)
                {
                    List<int> varIds = nextInf.getGene().getVariableIds();
                    List<Vector> varValues = records[i].getConditionValueByIds(varIds);
                    List<InferenceRecord> temprecords = nextInf.getVariableMatchRecords(this,time, varValues);
                    //if (temprecords.Contains(records[i])) temprecords.Remove(records[i]);
                    if (temprecords == null || temprecords.Count <= 0) continue;
                    foreach(InferenceRecord temp in temprecords)
                    {
                        if (nextRecords.Contains(temp)) continue;
                        nextRecords.Add(temp);
                    }
                }
            }

            if (nextRecords.Count <= 0) return;

            propagateReward(reward,time,level+1,nextRecords,mode);

        }

        public List<Inference> getInverseMatchInference(Inference inf,List<Inference> infs)
        {
            if (inf == null || infs==null || infs.Count<=0) return null;
            List<int> condIds = inf.getGene().getConditionIds();

            return infs.FindAll(i =>
                Utility.ContainsAll(condIds,i.getGene().getVariableIds())
            );

        }
        public void setReward(double reward,int time,int mode = 1,bool clear=true)
        {
            if (reward == 0) return;
            if (actionPlanTraces.Count <= 0) return;
            if (reward == 1.0) mode = 3;//如果是摆脱撞墙，这个正向奖励不传播
            for (int i = actionPlanTraces.Count - 1; i >= 0; i--)
            {
                ActionPlan plan = actionPlanTraces[i];
                thinkReset();
                for(int j=0;j<this.Receptors.Count;j++)
                {
                    this.Receptors[j].think(this, time, plan.inputObs[j]);
                }
                this.Handlers.ForEach(h => h.think(this, time, null));

                List<InferenceRecord> temp = new List<InferenceRecord>();
                for (int j=0;j<this.imagination.inferences.Count;j++)
                {
                    Inference inf = this.imagination.inferences[j];
                    if (!inf.getGene().hasEnvDenpend()) continue; //根外界环境无关的不做评估
                    List<(InferenceRecord,double)> matchedRecords = inf.getMatchRecordsInThink(this,time);
                    if (matchedRecords == null || matchedRecords.Count <= 0) continue;
                    for(int k=0;k<matchedRecords.Count;k++)
                    {
                        if (temp.Contains(matchedRecords[k].Item1))
                        {
                            matchedRecords.RemoveAt(k--);
                        }
                    }
                    if (matchedRecords == null || matchedRecords.Count <= 0) continue;
                    temp.AddRange(matchedRecords.ConvertAll(m=>m.Item1));
                    double r = reward;
                    if(mode == 1)r = Math.Exp(i - this.actionPlanTraces.Count + 1) * reward;
                    matchedRecords.ForEach(mr =>
                    {
                        mr.Item1.evulation += r*(1-mr.Item2);
                        mr.Item1.childs.ForEach(mrc => mrc.evulation += r * (1 - mr.Item2));
                    });
                }
                
                if (mode == REWARD_ONCE) break;
            }
            if (reward == Session.GetConfiguration().evaluation.reward.collision) actionPlanTraces.Clear();
        }
        #endregion

        #region 反向推理
        /*
        /// <summary>
        /// 评判
        /// </summary>
        private void judge(int time)
        {
            //这是该函数将得到的结果，第一项是推理链，第二项是对每一个动作Id，其选择的值和推理路径
            List<(InferenceChain chain, Dictionary<int, (double, int[])>)> judgeResultList = new List<(InferenceChain chain, Dictionary<int, (double, int[])>)>();

            
            //拷贝评判项和权重，做好一项就删除一项
            List<JudgeGene> judges = new List<JudgeGene>(this.genome.judgeGenes);
            List<double> ws = judges.ConvertAll(j=>j.weight);
            //对每项做评判
            while (judges.Count > 0)
            {
                int judgeIndex = ws.argmax();
                JudgeGene judgeItem = judges[judgeIndex];
                double juegeItemWeight = ws[judgeIndex];

                (var var1, var var2) = doJudge(judgeItem);
                if (var1 != null)
                {
                    judgeResultList.Add((var1, var2));  
                }
                ws.RemoveAt(judgeIndex);
                judges.RemoveAt(judgeIndex);
            }
            //没有得到有效评判结果（初始的时候所有节点都没有值）
            if(judgeResultList.Count<=0)
            {
                this.Effectors.ForEach(e => e.randomValue(this,time));
                this.currentActionTraces = null;
                this.currentInferenceChain = null;
                this.judgeTime = time;
                return;
            }
            //对每一个评判的结果一个评分：进行正向推断，选择距离容忍界限最近的
            List<double> errors = new List<double>();
            for(int i=0;i<judgeResultList.Count;i++)
            {
                double error = 0;
                if(judgeResultList[i].chain == null)
                {
                    errors.Add(10000);
                    continue;
                }
                for(int j=0;j< judgeResultList.Count;j++)
                {
                    Inference inf = (Inference)this.getNode(judgeResultList[j].chain.head.referenceNode);
                    List<int> condids = ((InferenceGene)inf.Gene).getConditions().ConvertAll(x => x.Item1);
                    List<Vector> condValues = new List<Vector>();
                    for (int k = 0; k < condids.Count; k++)
                    {
                        Node node = this.getNode(condids[k]);
                        if (!node.Group.StartsWith("action"))
                            condValues.Add(node.Value);
                        else
                        {
                            int actionId = this.getActionIdByName(node.Name.Substring(1));
                            double val = judgeResultList[i].Item2[actionId].Item1;
                            condValues.Add(new Vector(new double[] { val }));
                        }
                    }
                    Vector varValue = inf.forwardinference(condValues);
                    double varlen = varValue.length();
                    double expectlen = judgeResultList[i].chain.varValue;
                    if (judgeResultList[i].chain.juegeItem.expression == "argmax")
                    {
                        if (varlen >= expectlen) error += 0;
                        else error += System.Math.Abs(varlen - expectlen);
                    }
                    else
                    {
                        if (varlen <= expectlen) error += 0;
                        else error += System.Math.Abs(varlen - expectlen);

                    }
                    int judgeIndex = this.genome.judgeGenes.IndexOf(judgeResultList[i].chain.juegeItem);
                    error *= this.genome.judgeGenes[judgeIndex].weight;
                    errors.Add(error);
                }
            }

            //选择误差最小的推理
            int minerrorIndex = errors.argmin();
            this.currentInferenceChain = judgeResultList[minerrorIndex].chain;
            this.currentActionTraces = judgeResultList[minerrorIndex].Item2;

            if (currentActionTraces == null || currentActionTraces.Count <= 0)
            {
                this.Effectors.ForEach(e => e.activate(this, time, 0));
            }
            else
            {
                //计算效应器输出
                foreach (int key in this.currentActionTraces.Keys)
                {
                    double value = this.currentActionTraces[key].Item1;
                    Effector effector = getEffectorByActionSensorId(key);
                    effector.activate(this, time, new Vector(value));

                }
            }
            
            this.judgeTime = time;
        }
        private (InferenceChain chain, Dictionary<int, (double, int[])> actionValues) doJudge(JudgeGene judgeItem)
        { 
            List<int> judge_conditions = judgeItem.conditions;
            double judge_variableValue = judgeItem.expression == "argmax" ? double.MinValue : double.MaxValue;

            //找到所有包含推理变量（后置）的推理项
            List<Node> varInferences = this.Inferences.FindAll(inf => ((InferenceGene)inf.Gene).getVariable().Item1 == judgeItem.variable);
            if (varInferences == null || varInferences.Count <= 0) return (null,null);

            //选择一个最合适的根推理
            Inference rootInference = null;
            List<Vector> rootInferenceValues = null;
            int rootInferenceVarId = 0, rootInferenceVarIndex = -1;
            int rootInferenceRecordIndex = -1;
            for (int j = 0; j < varInferences.Count; j++)
            {
                (List<Vector> values, int varId, double value,int recordindex) = ((Inference)varInferences[j]).arginference(judgeItem.expression);
                if (values == null) continue;
                if (judgeItem.expression == "argmax" && value > judge_variableValue)
                {
                    judge_variableValue = value;
                    rootInference = (Inference)varInferences[j];
                    rootInferenceValues = values;
                    rootInferenceRecordIndex = recordindex;
                }
                else if (judgeItem.expression == "argmin" && value < judge_variableValue)
                {
                    judge_variableValue = value;
                    rootInference = (Inference)varInferences[j];
                    rootInferenceValues = values;
                    rootInferenceRecordIndex = recordindex;
                }
            }
            if (rootInference == null)
                return (null, null);
            rootInferenceVarId = ((InferenceGene)rootInference.Gene).getVariable().Item1;
            rootInferenceVarIndex = ((InferenceGene)rootInference.Gene).getVariableIndex();

            //在选择的根推理上逐级回溯构造推理链
            InferenceChain chain = new InferenceChain()
            {
                juegeItem = judgeItem,
                varValue = judge_variableValue,
                head = new InferenceChain.Item()
                {
                    referenceNode = rootInference.Id,
                    referenceRecordIndex = rootInferenceRecordIndex,
                    values = rootInferenceValues,
                    varIndex = rootInferenceVarIndex,
                    varTime = 0
                }
            };

            
            chain = do_reverse_inference(chain,chain.head);
            this.currentInferenceChain = chain;



            //在推理链上选择要执行的动作
            List<int> actionSensorIds = this.ActionReceptors.ConvertAll(r => r.Id);
            Dictionary<int, (double,int[])> actionValues = new Dictionary<int, (double, int[])>();

            for(int k=0;k<actionSensorIds.Count;k++)
            {
                List<List<int>> traces = chain.findActionTrace(this,actionSensorIds[k]);
                if(traces == null || traces.Count<=0) //没有推理到该动作，给一个随机值
                {
                    double min = Session.GetConfiguration().agent.receptors.actions[k].Range.Min;
                    double max = Session.GetConfiguration().agent.receptors.actions[k].Range.Max;
                    double value = 0.5;//new Random().NextDouble() * (max - min) + min;
                    actionValues.Add(actionSensorIds[k], (value, null));
                }
                else
                {
                    //随机选择一个推理迹
                    int traceIndex = new Random().Next(0,traces.Count);
                    double value = chain.getValueFromTrace(this,actionSensorIds[k], 0, traces[traceIndex]);
                    double min = Session.GetConfiguration().agent.receptors.actions[k].Range.Min;
                    double max = Session.GetConfiguration().agent.receptors.actions[k].Range.Max;
                    value = Math.Min(Math.Max(value, min), max);
                    actionValues.Add(actionSensorIds[k], (value, traces[traceIndex].ToArray()));
                }
            }
            return (chain, actionValues);
        }

        

        /// <summary>
        /// 反向推理
        /// </summary>
        /// <param name="chain">当前推理链</param>
        /// <param name="item">当前推理项</param>
        /// <returns></returns>
        private InferenceChain do_reverse_inference(InferenceChain chain, InferenceChain.Item item)
        {
            //1.取得记忆项
            Inference inf = (Inference)this.getNode(item.referenceNode);

            //2取得与该记忆项的条件匹配的其他记忆项（即以inf的前置条件作为后置变量的所有记忆节点）
            ////2.1取得inf的条件部分
            List<int> condids = ((InferenceGene)inf.Gene).getConditions().ConvertAll(c => c.Item1);
            List<int> condTimes = ((InferenceGene)inf.Gene).getConditions().ConvertAll(c => c.Item2);
            ////2.2遍历所有记忆节点，查找满足条件的
            List<Node> childInfs = this.Inferences.FindAll(f => ((InferenceGene)f.Gene).matchVariable(condids.ToArray()));
            if (childInfs == null || childInfs.Count <= 0)
                return chain;

            //3去掉在推理轨迹上已经出现的记忆节点
            for (int i=0;i< childInfs.Count;i++)
            {
                if(inferenceInChainTrace(chain,item,(Inference)childInfs[i]))
                {
                    childInfs.RemoveAt(i--);
                }
            }
            if (childInfs == null || childInfs.Count <= 0)
                return chain;

            //4 深度遍历
            for(int i=0;i< childInfs.Count;i++)
            {
                //记忆节点基本信息
                Inference cinf = (Inference)childInfs[i];
                int varIndex = cinf.getGene().getVariableIndex();
                (int varid, int vartime) = cinf.getGene().getVariable();
                Vector varValue = item.values[item.varIndex];
                (int t1, int t2) = cinf.getGene().getTimeDiff();
                //记忆节点中的记忆记录的后置变量维的值与上一个推理获得的值最接近的
                IntegrationRecord cinfRecord =  cinf.getNearestRecord(varIndex,varValue);
                if (cinfRecord == null) continue;
                
                //在记忆记录附近采样
                int sampleCount = Session.GetConfiguration().agent.inferencesamples;
                List<List<Vector>> cinf_samples = cinfRecord.sample(sampleCount);
                List<double> distances = cinf_samples.ConvertAll(s => s[varIndex].distance(varValue));
                List<Vector> samplesSelected = cinf_samples[distances.argmin()];

                //创建新的推理项
                InferenceChain.Item newItem = new InferenceChain.Item()
                {
                    referenceNode = cinf.Id,
                    referenceRecordIndex = cinf.Records.IndexOf(cinfRecord),
                    values = samplesSelected,
                    varIndex = varIndex,
                    varTime = (t1==t2)?item.varTime:item.varTime+1,
                    prev = item
                };
                item.next.Add(newItem);

                //深度递归
                chain = do_reverse_inference(chain, newItem);
            }

            
            return chain;

        }

        /// <summary>
        /// 在从head到item的推理路径上是否有inf出现
        /// </summary>
        /// <param name="chain"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        private bool inferenceInChainTrace(InferenceChain chain,InferenceChain.Item item,Inference inf)
        {
            if (item == null) return false;
            if (item.referenceNode == inf.Id) return true;
            while(item != null)
            {
                if (item.referenceNode == inf.Id) return true;
                item = item.prev;
            }
            return false;
        }
        /// <summary>
        /// 处理接受到的奖励，相当于适应度（-1到1之间）
        /// 根据适应度，设定推理路径上的各个推断节点和处理节点的可靠度
        /// </summary>
        /// <param name="reward"></param>
        public void setRewardInInferenceChain(double reward)
        {
            if (currentInferenceChain == null) return;
            foreach(int key in currentActionTraces.Keys)
            {
                if (this.currentActionTraces[key].Item2 == null) continue;
                int count = this.currentActionTraces[key].Item2.Length;
                double avg = reward / (count + 1);
                List<InferenceChain.Item> items = currentInferenceChain.getItemsFromTrace(this.currentActionTraces[key].Item2);
                if (items == null || items.Count <= 0) continue;
                for (int j = 0; j < items.Count; j++)
                {
                    Node node = this.nodes.FirstOrDefault(n => n.Id == items[j].referenceNode);
                    node.Reability = node.Reability + avg;
                }
            }
            
        }
        */
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
