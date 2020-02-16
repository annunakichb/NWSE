using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using NWSELib.common;
namespace NWSELib.net
{
    /// <summary>
    /// 行动策略
    /// </summary>
    public class Policy
    {
        public Network net;
        public Configuration.EvaluationPolicy policyConfig;

        public Policy(Network net)
        {
            this.net = net;
            policyConfig = Session.GetConfiguration().evaluation.policy;
        }
        /// <summary>
        /// 一 处理上一个行动链
        /// 1.若当前行动链为空，则执行二
        /// 2.若当前行动链没有得到reward，且行动次数小于阈值，
        ///   将动作修订为维持，继续执行，否则执行3
        /// 3.将行动链条和奖励置入环境记忆空间，执行二制定新的行动计划
        /// 二 制订行动计划
        /// 1.获得所有与环境匹配的推理场景,若没有则执行2，否则5
        /// 2.当前环境下本能动作在场景记忆中没有，创建本能动作链，结束，否则3
        /// 3.当前环境下本能动作在场景记忆评估为正值，创建本能动作链，结束，否则4
        /// 4.如果当前奖励为负，则创建反向随机动作链，否则创建高斯高斯随机动作链，
        ///   反复创建直到创建的动作结合场景要么在记忆中没有，要么记忆评估为正，结束
        /// 5.创建测试动作集，其中第一个是本能动作，其次都是按照与当前动作相近排序
        /// 6.对动作集中的每一个动作，执行前向推理，得到预测场景
        /// 7.若预测场景+维持动作在场景记忆中有，则记录评估值，否则回到6
        /// 8.若探索优先，则在未评估动作中选择构造新的行动计划，优先选择本能
        /// 9.否则在正评估行动中选择构建新的行动计划
        /// </summary>
        /// <param name="time"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public ActionPlan doEvaluation(int time, Session session)
        {
            
            //处理当前行动计划
            ActionPlan plan = processCurrentActionPlan(time,session);
            if (plan != null) return plan;

            //制订新的行动计划
            return makeNewActionPlan(time,session);

        }
        /// <summary>
        /// 制订新的行动计划
        /// </summary>
        /// <param name="time"></param>
        /// <param name="session"></param>
        /// <param name="reward"></param>
        /// <param name="policyConfig"></param>
        /// <returns></returns>
        private ActionPlan makeNewActionPlan(int time, Session session)
        {
            //取得与当前场景匹配的所有行动计划
            List<ActionPlan> plans = net.actionMemory.FindMatchActionPlans();
            //如果行动计划不是全部，补齐全部可能的行动计划，且按照与本能行动一致的顺序排序
            plans = checkActionPlansFull(plans,time);
            //如果探索优先，选择行动计划中还没有探索过的行动作为新的行动
            ActionPlan plan = null;
            if (policyConfig.exploration)
                plan = selectExplorationActionPlan(plans);
            if (plan != null) return net.actionPlanChain.Reset(plan);

            //如果本能方向没有探索，仍执行本能探索
            if (plans[0].evaulation == double.NaN)
                return net.actionPlanChain.Reset(plans[0]);

            //对每一个待选择动作进行评估
            plan = plans[0];
            double expectEvaulation = 0;
            List<(List<double>, double, int)> evaulationRecords = plans.ConvertAll(p => (p.actions, double.MinValue, 0));

            for (int i = 0; i < plans.Count; i++)
            {
                plan = plans[i];
                //如果是探索优先，且该行动尚未探索过，则执行探索
                if(policyConfig.exploration && double.IsNaN(plan.evaulation))
                {
                    plan.planSteps = policyConfig.init_plan_depth;
                    return net.actionPlanChain.Reset(plan);
                }

                //计算推理数
                int inferenceCount = policyConfig.init_plan_depth;
                if (plan.evaulation > 0)
                    inferenceCount += (int)plan.evaulation;
                else inferenceCount = (int)(plan.evaulation / 2);

                List<Vector> curObs = net.GetReceoptorValues();
                curObs = net.ReplaceWithPlanAction(curObs, plan);
                
                for(int j=0;j< inferenceCount;j++)
                {
                    curObs = net.forward_inference(curObs);
                    if (curObs == null) break;
                    ActionPlan temp = net.actionMemory.FindOptimaActionPlan(false,net.RemoveActionFromReceptor(curObs));
                    if(temp == null)
                    {
                        curObs = net.ReplaceMaintainAction(curObs);
                        continue;
                    }
                    if(expectEvaulation < temp.evaulation)
                    {
                        expectEvaulation = temp.evaulation;
                        plan = plans[i];
                        plan.planSteps = j + 1;
                    }
                    if(evaulationRecords[i].Item2< temp.evaulation)
                    {
                        evaulationRecords[i] = (evaulationRecords[i].Item1, temp.evaulation, j + 1);
                    
                    }
                }
                
            }
            net.actionPlanChain.EvaulationRecords = evaulationRecords;
            return net.actionPlanChain.Reset(plan);
            
        }
        
        /// <summary>
        /// 选择探索行动计划
        /// </summary>
        /// <param name="plans"></param>
        /// <returns></returns>
        private ActionPlan selectExplorationActionPlan(List<ActionPlan> plans)
        {
            if (plans.Count <= 0) return null;
            return plans.FirstOrDefault(p => double.IsNaN(p.evaulation));
        }

        /// <summary>
        /// 处理当前行动计划
        /// </summary>
        /// <param name="time">时间</param>
        /// <param name="session">会话</param>
        /// <param name="reward">本次观察得到的奖励</param>
        /// <param name="policyConfig">配置</param>
        /// <returns>如果不空，则表示当前行动计划继续</returns>
        private ActionPlan processCurrentActionPlan(int time, Session session)
        {
            ActionPlan plan = null;
            if (net.actionPlanChain.Length <= 0)
            {
                return net.actionPlanChain.Reset(ActionPlan.CreateRandomPlan(net,time));
            }
            if(time <= 100)
            {
                net.actionMemory.Merge(net, net.actionPlanChain.Last);
                return net.actionPlanChain.Reset(ActionPlan.CreateRandomPlan(net, time));
            }
            

            //1.1 记录奖励
            net.actionPlanChain.Last.reward = net.reward;
            //1.2 如果非探索优先，则根据当前环境寻找行动记忆空间中是否有更好行动方案
            if (!policyConfig.exploration)
            {
                plan = net.actionMemory.FindOptimaActionPlan();
                if (plan != null) return net.actionPlanChain.Reset(plan);
            }
            //1.3 本次行动探索是否还没有结束
            if (net.actionPlanChain.Length < net.actionPlanChain.Last.planSteps &&
                !policyConfig.PlanRewardRange.In(net.reward))
            {
                return net.actionPlanChain.PutNext(ActionPlan.createMaintainPlan(net, time,"", net.actionPlanChain.Last.forcastEvaulation, net.actionPlanChain.Last.planSteps-1));
            }

            //1.4 将本次探索结果放入行动记忆，本次探索结束
            net.actionMemory.Merge(net, net.actionPlanChain);
            return null;
        }

        /// <summary>
        /// 生成可以测试的动作计划集：从动作记忆中找到的行动计划，加上新补充的一些
        /// </summary>
        /// <param name="plans">从动作记忆中找到的行动计划</param>
        /// <param name="time"></param>
        /// <returns></returns>
        private List<ActionPlan> checkActionPlansFull(List<ActionPlan> plans,int time)
        {
            if (plans == null) plans = new List<ActionPlan>();
            List<List<double>> actionSets = CreateTestActionSet(Session.instinctActionHandler(net,time));

            for(int i=0;i<actionSets.Count;i++)
            {
                ActionPlan plan = plans.FirstOrDefault(p => p.Equals(actionSets[i]));
                if(plan != null)
                {
                    plans.Remove(plan);
                    plans.Insert(i, plan);
                }
                else
                {
                    String judgeType = ActionPlan.JUDGE_INFERENCE;
                    if (i == 0) judgeType = ActionPlan.JUDGE_INSTINCT;
                    else if (actionSets[i][0] == 0.5) judgeType = ActionPlan.JUDGE_MAINTAIN;
                    plan = ActionPlan.CreateActionPlan(net,actionSets[i], time,judgeType, "",policyConfig.init_plan_depth);
                    plans.Insert(i, plan);
                }
            }
            return plans;

        }

        /// <summary>
        /// 生成的测试集第一个是本能动作，第二个是方向不变动作，然后逐渐向两边增大
        /// </summary>
        /// <param name="instinctActions"></param>
        /// <returns></returns>
        private List<List<double>> CreateTestActionSet(List<double> instinctActions)
        {
            List<List<double>> r = new List<List<double>>();
            Receptor receptor = (Receptor)this.net["_a2"];
            double[] values = receptor.GetSampleValues();
            if (values != null)
            {
                int minIndex = values.ToList().ConvertAll(v => Math.Abs(v - instinctActions[0])).argmin();
                instinctActions[0] = values[minIndex];
            }
            r.Add(instinctActions);

            int count = receptor.getGene().SampleCount;
            double unit = receptor.getGene().LevelUnitDistance;

            int i = 1;
            while(r.Count < count)
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
                r.Add((new double[] { temp}).ToList());

                i++;
            }

            return r; 
        }
    }

}
