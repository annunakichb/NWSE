﻿using NWSELib.common;
using NWSELib.genome;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using NWSELib.common;

namespace NWSELib.net
{
    
    /// <summary>
    /// 行动规划项
    /// </summary>
    public class ActionPlan
    {
        public const String JUDGE_RANDOM = "随机行动";
        public const String JUDGE_INSTINCT = "本能行动";
        public const String JUDGE_INFERENCE = "推理行动";
        internal static readonly String MODE_EXPLOITATION = "利用优先";
        internal static readonly String MODE_INSTINCT = "本能优先";
        internal static readonly String MODE_EXPLORATION = "探索优先";

        /// <summary>
        /// 判定产生动作的类型
        /// </summary>
        public String judgeType;
        /// <summary>
        /// 推理模式
        /// </summary>
        internal String mode = "";
        /// <summary>
        /// 判定发生时间
        /// </summary>
        public int judgeTime;

        /// <summary>
        /// 该行动计划获得的观察数据
        /// </summary>
        public List<Vector> inputObs = new List<Vector>();

        /// <summary>
        /// 计划执行的动作
        /// </summary>
        public List<double> actions;

        /// <summary>
        /// 执行这个动作的预期评估值
        /// </summary>
        internal double evluation;

        /// <summary>
        /// 所有可能动作的评估记录
        /// </summary>
        public List<(List<double>, double)> actionEvaulationRecords = new List<(List<double>, double)>();
        

        /// <summary>
        /// 预期得到的观察数据
        /// </summary>
        public List<Vector> expectNextObs;

        /// <summary>
        /// 真实得到的观察结果
        /// </summary>
        public List<Vector> realObs;



        /// <summary>
        /// 行动实施以后预期得到的观察与实际得到的观察相似距离
        /// </summary>
        public double SimilarityDistance
        {
            get
            {
                if (realObs == null || expectNextObs == null ||
                   realObs.Count <= 0 || expectNextObs.Count <= 0)
                    return double.NaN;
                return Vector.manhantan_distance(expectNextObs, realObs);    
            }
        }

        /// <summary>
        /// 预期结果依据的推理项
        /// </summary>
        public List<(Inference,InferenceRecord)> inferencesItems = new List<(Inference, InferenceRecord)>();
        

        public string print()
        {
            StringBuilder str = new StringBuilder();
            str.Append("    scene=" + this.inputObs.toString() + System.Environment.NewLine);
            str.Append("    actions=" + Utility.toString(this.actions) + System.Environment.NewLine);
            str.Append("    expect=" + this.expectNextObs.toString() + System.Environment.NewLine);
            str.Append("    distance(similarity)=" + this.SimilarityDistance.ToString("F3") + System.Environment.NewLine);
            str.Append("    inferences=" + System.Environment.NewLine);
            for(int i=0;i< inferencesItems.Count;i++)
            {
                (Inference inf, InferenceRecord record) = inferencesItems[i];
                str.Append("    " + (i + 1).ToString() + ". " + inf.getGene().Text);
                str.Append("        " + record.ToString());
            }
            return str.ToString();
        }

        
    }
}
