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
        /// <summary>
        /// 制订行动计划
        /// </summary>
        /// <param name="time"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public ActionPlan doImagination(int time, Session session)
        {
            //遍历所有推理节点
            List<InferenceRecord> records = new List<InferenceRecord>();
            foreach (Inference inf in net.Inferences)
            {
                List<Vector> condValues = inf.getGene().getConditionsExcludeActionSensor()
                    .ConvertAll(id => net[id].GetValue(time));
                //找到节点中条件（但不包括动作）最匹配的记录
                List<InferenceRecord> rs = inf.getMatchRecordExcludeAction(condValues,true);
                if (rs.Count <= 0) continue;
                List<double> rsEvas = rs.ConvertAll(rt => rt.evulation);
                InferenceRecord record = rs[rsEvas.argmin()];
                records.Add(record);
            }
            if (records.Count <= 0) return null;


            //统计推理场景记录中的动作出现次数
            List<(Vector actions, List<InferenceRecord> infRecords)> stats = doGroupInfRecordsByAction(records);

            //找出所有记录都是正向评估的动作,从中优先选取与本能方向大致相同，其次选择评估值最大的
            List<(Vector actions, List<InferenceRecord> infRecords)> positiveStats = stats.FindAll(s => s.infRecords.All(tr => tr.evulation >= 0));
            (Vector actions, List<InferenceRecord> infRecords) r = findByInstinctApproximate(time,positiveStats);
            if (r.actions != null) return ActionPlan.create(net,r.actions,time,r.infRecords,"选择正向近似本能动作");
            r = findByMaxEvaluation(positiveStats);
            if (r.actions != null) return ActionPlan.create(net, r.actions, time,r.infRecords, "选择正向最大评估动作");

            //找到所有记录是半正向评估的动作
            List<(Vector actions, List<InferenceRecord> infRecords)> halfpositiveStats = stats.FindAll(s => s.infRecords.Exists(tr => tr.evulation > 0));
            r = findByInstinctApproximate(time,positiveStats);
            if (r.actions != null) return ActionPlan.create(net, r.actions, time,r.infRecords, "选择半正向近似本能动作");
            r = findByMaxEvaluation(positiveStats);
            if (r.actions != null) return ActionPlan.create(net, r.actions, time,r.infRecords, "选择半正向最大评估动作");

            //剩下的说明所有评估都是负值，先分成>0.5,<0.5 =0.5三组，选择评估中位数最大的
            List<(Vector actions, List<InferenceRecord> infRecords)> largeGroup, samllGroup, middleGroup;
            (largeGroup,samllGroup,middleGroup) = doGroupInfRecordsByLevels(stats);
            double[] medians = new double[3];
            medians[0] = getEvaluationMedian(largeGroup);
            medians[1] = getEvaluationMedian(samllGroup);
            medians[2] = getEvaluationMedian(middleGroup);
            if(medians[0] == medians.Max())
            {
                r = findByMaxEvaluation(largeGroup);
                if (r.actions != null)
                    return ActionPlan.create(net,r.actions,time,r.infRecords,"选择顺时针转动最大评估动作");
            }else if(medians[1] == medians.Max())
            {
                r = findByMaxEvaluation(samllGroup);
                if (r.actions != null)
                    return ActionPlan.create(net, r.actions, time, r.infRecords, "选择逆时针转动最大评估动作");
            }
            else if (medians[2] == medians.Max())
            {
                r = findByMaxEvaluation(middleGroup);
                if (r.actions != null)
                    return ActionPlan.create(net, r.actions, time, r.infRecords, "选择原方向最大评估动作");
            }


            r = findByMaxEvaluation(stats);
            return ActionPlan.create(net, r.actions, time, r.infRecords, "选择最大评估动作");
        }
        /// <summary>
        /// 取得所有items的评估值中位数
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        private double getEvaluationMedian(List<(Vector actions, List<InferenceRecord> infRecords)> items)
        {
            if (items == null || items.Count <= 0) return 0;
            List<double> evas = items.ConvertAll(item => item.infRecords.ConvertAll(r => r.evulation).Sum());
            if (evas.Count % 2 == 0)
                return (evas[evas.Count / 2 - 1] + evas[evas.Count / 2]) / 2;
            else
                return evas[evas.Count/2 + 1];
        }
        /// <summary>
        /// 按照动作分组
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        private (List<(Vector actions, List<InferenceRecord> infRecords)>,
            List<(Vector actions, List<InferenceRecord> infRecords)>,
            List<(Vector actions, List<InferenceRecord> infRecords)>)
        doGroupInfRecordsByLevels(List<(Vector actions, List<InferenceRecord> infRecords)>  items)
        {
            if (items == null || items.Count <= 0) return (null, null, null);
            List<(Vector actions, List<InferenceRecord> infRecords)> larges = new List<(Vector actions, List<InferenceRecord> infRecords)>();
            List<(Vector actions, List<InferenceRecord> infRecords)> smalls = new List<(Vector actions, List<InferenceRecord> infRecords)>();
            List<(Vector actions, List<InferenceRecord> infRecords)> middles = new List<(Vector actions, List<InferenceRecord> infRecords)>();

            foreach(var temp in items)
            {
                if (temp.actions[0] > 0.5)
                    larges.Add(temp);
                else if (temp.actions[0] < 0.5)
                    smalls.Add(temp);
                else
                    middles.Add(temp);
            }

            return (larges, smalls, middles);
        }

        /// <summary>
        /// 寻找最大评估的动作
        /// </summary>
        /// <param name="time"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        private (Vector actions, List<InferenceRecord> infRecords) findByMaxEvaluation(List<(Vector actions, List<InferenceRecord> infRecords)> items)
        {
            if (items == null || items.Count <= 0) return (null,null);
            double maxEva = double.MinValue;
            (Vector actions, List<InferenceRecord> infRecords) result = (null,null);
            foreach(var temp in items)
            {
                double evaluation = temp.infRecords.ConvertAll(r => r.evulation).Sum();
                if(evaluation > maxEva)
                {
                    maxEva = evaluation;
                    result = temp;
                }
            }
            return result;
        }

        /// <summary>
        /// 寻找与本能动作最近似的动作
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        private (Vector actions, List<InferenceRecord> infRecords) findByInstinctApproximate(int time,List<(Vector actions, List<InferenceRecord>)> items)
        {
            if (items == null || items.Count <= 0)
                return (null, null);
            List<double> instinctActions = Session.instinctActionHandler(net, time);
            Vector instinct = new Vector(instinctActions.ToArray());

            List<double> dis = items.ConvertAll(item=>instinct.distance(item.actions));
            return items[dis.argmin()];
        }
        /// <summary>
        /// 对推理记录按照不同动作分组
        /// 如果记录中没有动作，那它在每个组里
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        private List<(Vector actions, List<InferenceRecord> infRecords)> doGroupInfRecordsByAction(List<InferenceRecord> records)
        {
            List<(Vector actions, List<InferenceRecord> infRecords)> r = new List<(Vector actions, List<InferenceRecord> infRecords)>();
            if (records == null || records.Count <= 0) return r;
            int nullcount = 0;
            foreach(InferenceRecord record in records)
            {
                Vector action = record.getActionValueInCondition();
                if (action == null) { nullcount += 1;continue; }
                bool exist = false;
                foreach(var temp in r)
                {
                    if(Vector.equals(action,temp.actions))
                    {
                        temp.infRecords.Add(record);
                        exist = true;
                        break;
                    }
                }
                if(!exist)
                {
                    (Vector actions, List<InferenceRecord> infRecords) t;
                    t.actions = action;
                    t.infRecords = new List<InferenceRecord>();
                    t.infRecords.Add(record);
                    r.Add(t);
                }
            }

            if (nullcount>0)
            {
                foreach (InferenceRecord record in records)
                {
                    Vector action = record.getActionValueInCondition();
                    if (action == null)
                    {
                        r.ForEach(tr => tr.infRecords.Add(record));
                    }
                }
            }
            return r;
        }





        
    }
}
