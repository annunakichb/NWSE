using NWSELib.common;
using NWSELib.genome;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static NWSELib.Configuration;

namespace NWSELib.net
{
    /// <summary>
    /// 推理机能
    /// </summary>
    public class Imagination
    {
        /// <summary>
        /// 所属网络
        /// </summary>
        protected Network net;
        /// <summary>
        /// 观察历史
        /// </summary>
        protected ObservationHistory history;

        /// <summary>
        /// 这个是抽象处理后的整合节点，将用于推理任务
        /// </summary>
        public List<Inference> inferences = new List<Inference>();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="net"></param>
        public Imagination(Network net)
        {
            this.net = net;
            history = new ObservationHistory(net);
            inferences = net.Inferences;
        }

        
        
        #region 价值判断
        /// <summary>
        /// 进行推理，返回推理后得到的动作
        /// </summary>
        /// <returns></returns>
        public ActionPlan doImagination(int time, Session session)
        {
            //传播奖励
            processReward(time);

            if (time < 50)
            {
                //net.actionMemory.Merge(net, net.actionPlanChain.Last,false);
                return net.actionPlanChain.Reset(ActionPlan.CreateRandomPlan(net, time, "随机动作阶段"));
            }

            //得到实际环境
            (List<Vector> curObs, List<double> curActions) = net.GetSplitReceptorValues();
            //得到近似场景
            List<ActionPlan> plans = net.actionMemory.FindMatchActionPlans();
            //如果行动计划不是全部，补齐全部可能的行动计划，且按照与本能行动一致的顺序排序
            plans = checkActionPlansFull(plans, time);

            //遍历每个行动计划
            for (int a = 0; a < plans.Count; a++)
            {
                if (!double.IsNaN(plans[a].evaulation) || plans[a].evaulation<0) continue;
                List<double> actions = plans[a].actions;
                //执行直到无法执行，得到评估值
                (double evulation, List<Vector> initObs, List<(InferenceRecord,double)> infRecords) = doActionImagination(plans[a], time, session);

                plans[a].evaulation = evulation;
                plans[a].inferenceRecords = infRecords;

                net.actionMemory.Merge(net, plans[a], false);

            }
            
            //制订行动计划
            return doActionPlan(plans,time);
        }

        public void processReward(int time)
        {
            if (net.reward == 0) return;
            if (net.actionPlanChain.Length <= 0) return;

            //遍历行动计划链
            List<InferenceRecord> records = new List<InferenceRecord>();
            List<ActionPlan> plans = net.actionPlanChain.ToList();
            for (int i = plans.Count - 1; i >= 0; i--)
            {

                //double r = reward / (actionPlanTraces.Count-i);
                double r = Math.Exp(i - plans.Count + 1) * net.reward;

                if (plans[i].inferenceRecords.Count > 0)
                {
                    plans[i].inferenceRecords.ForEach(item =>
                        { if (!records.Contains(item.Item1)) { item.Item1.evulation += r; records.Add(item.Item1); } }
                    );

                   // plans[i].evaulation = plans[i].inferenceRecords
                   // .ConvertAll(rc => rc.Item1.evulation * (rc.Item2 < 1 ? 1 - rc.Item2 : 0.001)).Sum();


                }
                else if (i == plans.Count - 1)
                {
                    List<InferenceRecord> infRecords = net.GetMatchInfRecords(plans[i].inputObs,plans[i].actions,time);

                    if (infRecords.Count > 0)
                    {
                        infRecords.ForEach(item => { if (!records.Contains(item)) { item.evulation = r; records.Add(item); } });

                       // plans[i].evaulation = infRecords
                    //.ConvertAll(rc => rc.evulation).Sum();
                    }    
                }
                if (net.reward >= 0)
                    break;
            }

            

            //net.actionMemory.Merge(net, net.actionPlanChain,false);

            //清空行动计划链
            if (net.reward < 0)
                net.actionPlanChain.Clear();

        }

        /// <summary>
        /// 生成可以测试的动作计划集：从动作记忆中找到的行动计划，加上新补充的一些
        /// </summary>
        /// <param name="plans">从动作记忆中找到的行动计划</param>
        /// <param name="time"></param>
        /// <returns></returns>
        private List<ActionPlan> checkActionPlansFull(List<ActionPlan> plans, int time)
        {
            if (plans == null) plans = new List<ActionPlan>();
            List<List<double>> actionSets = CreateTestActionSet(Session.instinctActionHandler(net, time));

            ActionPlan[] r = new ActionPlan[actionSets.Count];
            for (int i = 0; i < actionSets.Count; i++)
            {
                ActionPlan plan = plans.FirstOrDefault(p => p.Equals(actionSets[i]));
                if (plan == null)
                {
                    String judgeType = ActionPlan.JUDGE_INFERENCE;
                    if (i == 0) judgeType = ActionPlan.JUDGE_INSTINCT;
                    else if (actionSets[i][0] == 0.5) judgeType = ActionPlan.JUDGE_MAINTAIN;
                    plan = ActionPlan.CreateActionPlan(net, actionSets[i], time, judgeType, "", 0);
                }
                r[i] = plan;
            }
            return r.ToList();

        }

        private ActionPlan doActionPlan(List<ActionPlan> plans, int time)
        {
            //找到本能行动计划
            List<double> instictAction = Session.instinctActionHandler(net, time);
            ActionPlan instinctPlan = plans.FirstOrDefault(p => p.actions[0] == instictAction[0]);

            //找到当前行动计划
            ActionPlan maintainPlan = plans.FirstOrDefault(p => p.actions[0] == 0.5);

            //记录各个方向的评估
            List<(List<double>, double)> actionEvaulationRecords = plans.ConvertAll(p => (p.actions, p.evaulation));

            if(this.net.reward < 0)
            {
                if(instinctPlan != maintainPlan && (double.IsNaN(instinctPlan.evaulation) || instinctPlan.evaulation>=0))
                {
                    instinctPlan.judgeTime = time;
                    instinctPlan.judgeType = ActionPlan.JUDGE_INSTINCT;
                    instinctPlan.reason = "行动受挫，选择本能";
                    instinctPlan.actionEvaulationRecords = actionEvaulationRecords;
                    return net.actionPlanChain.PutNext(instinctPlan);
                }
                for(int i=1;i<plans.Count;i++)
                {
                    if (plans[i] == maintainPlan) continue;
                    if(double.IsNaN(plans[i].evaulation) || plans[i].evaulation>=0)
                    {
                        plans[i].judgeTime = time;
                        plans[i].judgeType = ActionPlan.JUDGE_INFERENCE;
                        plans[i].reason = "行动受挫，顺序选择";
                        plans[i].actionEvaulationRecords = actionEvaulationRecords;
                        return net.actionPlanChain.PutNext(plans[i]);
                    }
                }
                //选择评估值最大的
                List<double> fevas = plans.ConvertAll(p => p.evaulation);
                List<int> fevaIndex = fevas.argsort();
                fevaIndex.Reverse();
                for(int i=0;i<3;i++)
                {
                    if (plans[fevaIndex[i]] == maintainPlan) continue;
                    plans[fevaIndex[i]].judgeTime = time;
                    plans[fevaIndex[i]].judgeType = ActionPlan.JUDGE_RANDOM;
                    plans[fevaIndex[i]].reason = "行动受挫，择优选择";
                    plans[fevaIndex[i]].actionEvaulationRecords = actionEvaulationRecords;
                    return net.actionPlanChain.PutNext(plans[fevaIndex[i]]);

                }

                int index = Network.rng.Next(0,plans.Count);
                while(plans[index] == maintainPlan)
                    index = Network.rng.Next(0, plans.Count);

                plans[index].judgeTime = time;
                plans[index].judgeType = ActionPlan.JUDGE_RANDOM;
                plans[index].reason = "行动受挫，随机选择";
                plans[index].actionEvaulationRecords = actionEvaulationRecords;
                return net.actionPlanChain.PutNext(plans[index]);
            }


            if (net.actionPlanChain.GetMaintainCount() < 20)
            {
                maintainPlan.judgeTime = time;
                maintainPlan.judgeType = (instinctPlan == maintainPlan) ? ActionPlan.JUDGE_INSTINCT : ActionPlan.JUDGE_MAINTAIN;
                maintainPlan.reason = "动作维持";
                maintainPlan.actionEvaulationRecords = actionEvaulationRecords;
                return net.actionPlanChain.PutNext(maintainPlan);
            }

            if ((double.IsNaN(instinctPlan.evaulation) || instinctPlan.evaulation >= 0))
            {
                instinctPlan.judgeTime = time;
                instinctPlan.judgeType = ActionPlan.JUDGE_INSTINCT;
                instinctPlan.reason = "选择本能";
                instinctPlan.actionEvaulationRecords = actionEvaulationRecords;
                return net.actionPlanChain.PutNext(instinctPlan);
            }
            for (int i = 1; i < plans.Count; i++)
            {
                if (plans[i] == maintainPlan) continue;
                if (double.IsNaN(plans[i].evaulation) || plans[i].evaulation >= 0)
                {
                    plans[i].judgeTime = time;
                    plans[i].judgeType = ActionPlan.JUDGE_INFERENCE;
                    plans[i].reason = "选择本能相似行动";
                    plans[i].actionEvaulationRecords = actionEvaulationRecords;
                    return net.actionPlanChain.PutNext(plans[i]);
                }
            }

            //选择评估值最大的
            List<double> evas = plans.ConvertAll(p => p.evaulation);
            int evaIndex = evas.argmax();

            plans[evaIndex].judgeTime = time;
            plans[evaIndex].judgeType = ActionPlan.JUDGE_RANDOM;
            plans[evaIndex].reason = "择优选择";
            plans[evaIndex].actionEvaulationRecords = actionEvaulationRecords;
            return net.actionPlanChain.PutNext(plans[evaIndex]);



        }

        private (double, List<Vector>, List<(InferenceRecord, double)>) doActionImagination(ActionPlan plan, int time, Session session)
        {
            //取得感知环境值，动作感知替换为预想动作
            List<Vector> observations = net.GetReceoptorValues();
            net.ReplaceWithAction(observations, plan.actions);

            (List<Vector> orginEnvValues, List<double> orginAction) = net.GetSplitReceptorValues();

            List<Vector> initObs = new List<Vector>();
            foreach (Vector v in orginEnvValues) initObs.Add(v.clone());

            //推理下一个环境变化
            ////虚拟初始化
            net.thinkReset();

            ////对网络进行虚拟激活，得到新的观察值
            int maxsteps = 5, step = 0;
            double evluation = double.NaN;
            List<(InferenceRecord, double)> infRecords = new List<(InferenceRecord, double)>();
            while (true)
            {

                //初始化感知节点
                for (int i = 0; i < net.Receptors.Count; i++)
                {
                    net.Receptors[i].think(net, time, observations[i]);
                }

                //激活处理节点
                List<Handler> handlers = this.getCanThinkHandlers(net, time);
                while (handlers != null && !handlers.All(n => n.IsThinkCompleted(time)))
                {
                    net.Handlers.ForEach(n => n.think(net, time, null));
                }
                //取得有效推理节点
                List<Inference> infs = getCanThinkInferences(net, time);
                if (infs == null || infs.Count <= 0)
                    break;
                //将观察值情况，后面要存放下一次观察
                for (int i = 0; i < observations.Count; i++) observations[i] = null;
                //遍历所有推理节点
                double[] distances = new double[observations.Count];
                double eva = 0;
                foreach (Inference inte in infs)
                {
                    //得到推理节点后置变量
                    List<int> varIds = inte.getGene().getVariableIds();
                    List<int> receptorIndexs = varIds.ConvertAll(id => net.Receptors.IndexOf((Receptor)net[id]));

                    //得到条件值
                    List<Vector> condValues = inte.getGene().getConditionIds().ConvertAll(id => net[id]).ConvertAll(node => node.getThinkValues(time));
                    //匹配推理场景
                    var postvar = inte.forward_inference(condValues);
                    if (postvar.postValues == null) continue;

                    if (step == 0 && !infRecords.ConvertAll(t=>t.Item1).Contains(postvar.record) && postvar.record!=null)
                        infRecords.Add((postvar.record,postvar.distance));

                    //更新观察
                    for (int i = 0; i < postvar.postValues.Count; i++)
                    {
                        if (receptorIndexs[i] == -1) continue;
                        if (distances[i] == 0 || distances[i] > postvar.distance)
                        {
                            observations[receptorIndexs[i]] = postvar.postValues[i].clone();
                        }
                    }

                    //更新评估值
                    eva += postvar.record.evulation * (postvar.distance < 1 ? 1 - postvar.distance : 0.001); 
                }

                step += 1;
                //检查是否所有感知都已经被观察
                if (observations.Count(v=>v==null)!=plan.actions.Count || step >= maxsteps)
                {
                    if(eva != 0)evluation = eva;
                    break;

                }

                //动作改成0.5，即维持不变
                net.ReplaceMaintainAction(observations);
                
            }
            return (evluation, initObs, infRecords);
        }


        
        private List<Inference> getCanThinkInferences(Network net, int time)
        {
            List<Inference> nodes = new List<Inference>();
            this.inferences = net.Inferences;
            foreach (Inference h in this.inferences)
            {
                if(h.getGene().getConditions().All(c=>net.getNode(c.Item1).IsThinkCompleted(time)))
                    nodes.Add(h);
                
            }
            return nodes;
        }

        private List<Handler> getCanThinkHandlers(Network net, int time)
        {
            List<Node> nodes = new List<Node>();
            foreach(Handler h in net.Handlers)
            {
                nodes = nodeThink(net, h, time, nodes);
            }
            return nodes.FindAll(n => n is Handler).ConvertAll(n => (Handler)n);
            
        }

        private List<Node> nodeThink(Network net, Node h, int time, List<Node> nodes)
        {
            if (h == null) return nodes;
            if (nodes.Contains(h)) return nodes;

            List<Node> childs = net.getInputNodes(h.Id);
            if (childs == null || childs.Count <= 0)
            {
                nodes.Add(h);return nodes;
            }
            foreach(Node c in childs)
            {
                nodes = nodeThink(net, c, time, nodes);
            }
            if (childs.All(c => nodes.Contains(c)))
                nodes.Add(h);
            return nodes;
        }



        private Dictionary<double, List<List<double>>> _cached_ActionSet = new Dictionary<double, List<List<double>>>();

        /// <summary>
        /// 生成的测试集第一个是本能动作，第二个是方向不变动作，然后逐渐向两边增大
        /// </summary>
        /// <param name="instinctActions"></param>
        /// <returns></returns>
        private List<List<double>> CreateTestActionSet(List<double> instinctActions)
        {
            List<List<double>> r = new List<List<double>>();
            Receptor receptor = (Receptor)this.net["_a2"];
            int count = receptor.getGene().SampleCount;
            double unit = receptor.getGene().LevelUnitDistance;

            double[] values = receptor.GetSampleValues();
            if (values != null)
            {
                int minIndex = values.ToList().ConvertAll(v => Math.Abs(v - instinctActions[0])).argmin();
                instinctActions[0] = values[minIndex];

                if (_cached_ActionSet.ContainsKey(instinctActions[0]))
                    return _cached_ActionSet[instinctActions[0]];

                r.Add(instinctActions);
                int index = 1;
                while (r.Count < count)
                {
                    int t = (minIndex + index) % (values.Length);
                    r.Add(new double[] { values[t] }.ToList());
                    if (r.Count >= count) break;

                    t = minIndex - index;
                    if (t < 0) t = values.Length - 1;
                    r.Add(new double[] { values[t] }.ToList());

                    index += 1;
                }

                _cached_ActionSet.Add(instinctActions[0], r);
                return r;
            }

            r.Add(instinctActions);


            int i = 1;
            while (r.Count < count)
            {
                double temp = instinctActions[0] + i * unit;
                if (temp < 0) temp = 1.0 + unit + temp;
                else if (temp > 1) temp = temp - 1.0 - unit;
                if (temp <= 0.0000001) temp = 0;
                r.Add((new double[] { temp }).ToList());
                if (r.Count >= count) break;

                temp = instinctActions[0] - i * unit;
                if (temp < 0) temp = 1.0 + unit + temp;
                else if (temp > 1) temp = temp - 1.0 - unit;
                if (temp <= 0.0000001) temp = 0;
                r.Add((new double[] { temp }).ToList());

                i++;
            }

            return r;
        }

        #endregion


    }
}
