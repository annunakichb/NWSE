using NWSELib.common;
using NWSELib.genome;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using NWSELib.common;

namespace NWSELib.net
{
    public class ActionPlanChain
    {
        public static readonly String MODE_EXPLOITATION = "利用优先";
        public static readonly String MODE_INSTINCT = "本能优先";
        public static readonly String MODE_EXPLORATION = "探索优先";
        /// <summary>
        /// 行动计划集
        /// </summary>
        private List<ActionPlan> plans = new List<ActionPlan>();
        /// <summary>
        /// 各种行动评估记录
        /// </summary>
        public List<(List<double>, double,int)> EvaulationRecords = new List<(List<double>, double,int)>();
        /// <summary>
        /// 第一个行动计划
        /// </summary>
        public ActionPlan Root { get { return plans.Count <= 0 ? null : plans[0]; } }
        /// <summary>
        /// 最后一个
        /// </summary>
        public ActionPlan Last { get { return plans.Count <= 0 ? null : plans[plans.Count-1]; } }
        /// <summary>
        /// 链长度
        /// </summary>
        public int Length { get { return plans.Count; } }
        /// <summary>
        /// 清空
        /// </summary>
        public void Clear() { this.plans.Clear(); }
        /// <summary>
        /// 放入下一个
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        public ActionPlan PutNext(ActionPlan plan)
        {
            this.plans.Add(plan);
            return plan;
        }
        /// <summary>
        /// 重新开始一个新计划
        /// </summary>
        /// <param name="plan"></param>
        /// <returns></returns>
        public ActionPlan Reset(ActionPlan plan)
        {
            Clear();
            plans.Add(plan);
            return plan;
        }
        /// <summary>
        /// 所有行动计划
        /// </summary>
        public List<ActionPlan> ToList() {  return plans;  }
        /// <summary>
        /// 字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (plans.Count <= 0) return "";
            return plans[0].ToString() + "steps=" + this.Length.ToString() + System.Environment.NewLine;
            
        }
    }
    /// <summary>
    /// 行动规划项
    /// </summary>
    public class ActionPlan
    {
        #region 基本信息
        public const String JUDGE_RANDOM = "随机行动";
        public const String JUDGE_INSTINCT = "本能行动";
        public const String JUDGE_INFERENCE = "推理行动";
        public const String JUDGE_MAINTAIN = "维持行动";

        /// <summary>
        /// 判定产生动作的类型
        /// </summary>
        public String judgeType;
        /// <summary>
        /// 推理模式
        /// </summary>
        internal String reason = "";
        /// <summary>
        /// 判定发生时间
        /// </summary>
        public int judgeTime;

        /// <summary>
        /// 该行动计划对应的观察数据
        /// </summary>
        public List<Vector> inputObs = new List<Vector>();

        /// <summary>
        /// 计划执行的动作
        /// </summary>
        public List<double> actions;

        /// <summary>
        /// 执行这个动作的评估结果
        /// 当评估大于0的时候，表示走evluation步没有碰到障碍
        /// 当评估小于0的时候，表示走abs(evluation)步碰到障碍
        /// </summary>
        public double evaulation = double.NaN;

        /// <summary>
        /// 在制订行动方案时预测的评估值
        /// </summary>
        public double forcastEvaulation = double.NaN;

        /// <summary>
        /// 计划该动作维持步数
        /// </summary>
        public int planSteps;

        /// <summary>
        /// 该计划执行获得的奖励
        /// </summary>
        public double reward;


        public bool Equals(List<double> actions)
        {
            for(int i=0;i<actions.Count;i++)
            {
                if (this.actions[i] != actions[i]) return false;
            }
            return true;
        }
        public bool Equals(params double[] actions)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (this.actions[i] != actions[i]) return false;
            }
            return true;
        }



        #endregion

        #region 工厂方法
        public static ActionPlan CreateRandomPlan(Network net, int time)
        {
            ActionPlan plan = new ActionPlan();
            plan.actions = net.CreateRandomActions();
            plan.judgeTime = time;
            plan.judgeType = ActionPlan.JUDGE_RANDOM;
            plan.forcastEvaulation = double.NaN;
            plan.planSteps = 1;
            plan.reason = "";
            plan.inputObs = net.GetSplitReceptorValues().scene;
            return plan;
        }
        /// <summary>
        /// 创建当前动作的维持动作行动计划
        /// </summary>
        /// <param name="net"></param>
        /// <param name="time"></param>
        /// <param name="reason"></param>
        /// <returns></returns>
        public static ActionPlan createMaintainPlan(Network net,int time,String reason,double expect,int planSteps)
        {
            ActionPlan plan = new ActionPlan();
            plan.actions = new double[] { 0.5 }.ToList();
            plan.judgeTime = time;
            plan.judgeType = ActionPlan.JUDGE_MAINTAIN;
            plan.forcastEvaulation = expect;
            plan.planSteps = planSteps;
            plan.reason = reason;
            plan.inputObs = net.GetSplitReceptorValues().scene;
            return plan;
        }

        public static ActionPlan CreateActionPlan(Network net,List<double> actions,int time,String judgeType, String reason, int planSteps)
        {
            ActionPlan plan = new ActionPlan();
            plan.actions = actions;
            plan.judgeTime = time;
            plan.judgeType = judgeType;
            plan.planSteps = planSteps;
            plan.reason = reason;
            plan.inputObs = net.GetSplitReceptorValues().scene;
            return plan;
        }

        #endregion


        #region 读写

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.Append("judgeType=" + this.judgeType.ToString() + System.Environment.NewLine);
            str.Append("reason=" + this.reason.ToString() + System.Environment.NewLine);
            str.Append("scene=" + this.inputObs.toString() + System.Environment.NewLine);
            str.Append("actions=" + Utility.toString(this.actions) + System.Environment.NewLine);
            str.Append("evaulation=" + this.evaulation.ToString("F0") + System.Environment.NewLine);
            str.Append("expect = " + this.forcastEvaulation.ToString("F0") + System.Environment.NewLine);
            str.Append("planstep = " + this.planSteps.ToString() + System.Environment.NewLine);
            return str.ToString();
        }
        #endregion


    }
}
