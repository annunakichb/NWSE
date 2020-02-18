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
            //1.1 第一次规划，随机动作
            if (net.actionPlanChain.Length <= 0)
            {
                return net.actionPlanChain.Reset(ActionPlan.CreateInstinctPlan(net, time, "初始动作"));
            }

            //1.2 仍在随机动作阶段
            if (time < 100)
            {
                net.actionMemory.Merge(net, net.actionPlanChain.Last);
                return net.actionPlanChain.Reset(ActionPlan.CreateRandomPlan(net, time, "随机漫游"));
            }

            //1.3 随机漫游结束
            if (time == 100)
            {
                net.actionMemory.Merge(net, net.actionPlanChain.Last);
                return net.actionPlanChain.Reset(makeNewActionPlan(time, session));
            }

            //1.4 规划行动是否完成了(奖励负)
            if (policyConfig.PlanRewardRange.In(net.reward))
            {
                net.actionMemory.Merge(net, net.actionPlanChain);
                return net.actionPlanChain.Reset(makeNewActionPlan(time, session));
            }
            //1.5 规划行动是否完成了(完成计划步长)
            if (net.actionPlanChain.Length >= net.actionPlanChain.Root.planSteps && net.actionPlanChain.Root.planSteps > 0)
            {
                net.actionMemory.Merge(net, net.actionPlanChain.Last);
                return net.actionPlanChain.Reset(makeNewActionPlan(time, session));
            }

            //1.6 预测本次规划行动的结果是否会是负值
            double expect = forcastActionPlan();
            if (expect < 0)
            {
                net.actionMemory.Merge(net, net.actionPlanChain);
                return net.actionPlanChain.Reset(makeNewActionPlan(time, session));
            }

            //1.7 继续本次行动计划
            return net.actionPlanChain.PutNext(ActionPlan.createMaintainPlan(net, time, "", net.actionPlanChain.Last.expect, net.actionPlanChain.Last.planSteps - 1));

        }

        /// <summary>
        /// 对当前行为进行推理，预测其未来评估
        /// </summary>
        /// <returns></returns>
        private double forcastActionPlan()
        {

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
            List<ObservationHistory.ActionRecord> actionRecords = net.actionMemory.FindMatchActionPlans();
            //如果行动计划不是全部，补齐全部可能的行动计划，且按照与本能行动一致的顺序排序
            List<ActionPlan> plans = checkActionPlansFull(actionRecords, time);

            //找到本能行动计划和维持行动计划
            List<double> instictAction = Session.instinctActionHandler(net, time);
            ActionPlan instinctPlan = plans.FirstOrDefault(p => p.actions[0] == instictAction[0]);
            ActionPlan maintainPlan = plans.FirstOrDefault(p => p.actions[0] == 0.5);

            //遍历所有的计划
            for(int i=0;i<plans.Count;i++)
            {
                //上次是负奖励，则维持行动没有必要
                if (net.reward < 0 && plans[i].IsMaintainAction()) continue;
                //如果第i个行动确定是正评估，就是它了
                if (plans[i].evaulation > 0)
                {
                    plans[i].reason = "走向正评估";
                    plans[i].planSteps = (int)plans[i].evaulation + policyConfig.init_plan_depth;
                    return plans[i];
                }
                //如果第i个行动是未知评估，就是它了
                if (double.IsNaN(plans[i].evaulation))
                {
                    plans[i].reason = "探索未知";
                    plans[i].planSteps = policyConfig.init_plan_depth;
                    return plans[i];
                }
            }

            //执行到这里，说明所有的评估都是负评估了,选择最小负数的行动
            int index = plans.ConvertAll(p => p.evaulation).argmin();
            plans[index].reason = "选择最小负评估";
            plans[index].planSteps = -1*(int)plans[index].evaulation / 2;
            return plans[index];
        }
        
        

        /// <summary>
        /// 生成可以测试的动作计划集：从动作记忆中找到的行动计划，加上新补充的一些
        /// </summary>
        /// <param name="plans">从动作记忆中找到的行动计划</param>
        /// <param name="time"></param>
        /// <returns></returns>
        private List<ActionPlan> checkActionPlansFull(List<ObservationHistory.ActionRecord> actionRecords,int time)
        {
            List<List<double>> actionSets = CreateTestActionSet(Session.instinctActionHandler(net,time));

            ActionPlan[] r = new ActionPlan[actionSets.Count];
            for (int i=0;i<actionSets.Count;i++)
            {
                ActionPlan plan = null;
                String judgeType = ActionPlan.JUDGE_INFERENCE;
                if (i == 0) judgeType = ActionPlan.JUDGE_INSTINCT;
                else if (actionSets[i][0] == 0.5) judgeType = ActionPlan.JUDGE_MAINTAIN;

                ObservationHistory.ActionRecord record = actionRecords.FirstOrDefault(p => p.Equals(actionSets[i]));
                if(record == null)
                {
                    plan = ActionPlan.CreateActionPlan(net, actionSets[i], time, judgeType, "");
                }else
                {
                    plan = ActionPlan.CreateActionPlan(net, record.actions, time, judgeType, "");
                }
                r[i] = plan;
            }
            return r.ToList();

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
                while(r.Count<count)
                {
                    int t = (minIndex + index) % (values.Length);
                    r.Add(new double[] {values[t]}.ToList());
                    if (r.Count >= count) break;

                    t = minIndex - index;
                    if (t < 0) t = values.Length - 1;
                    r.Add(new double[] { values[t] }.ToList());

                    index += 1;
                }

                _cached_ActionSet.Add(instinctActions[0],r);
                return r;
            }

            r.Add(instinctActions);
            

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
