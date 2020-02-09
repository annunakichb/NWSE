using System;
using System.Linq;
using NWSELib.common;
using NWSELib.genome;
using System.Collections.Generic;
using Microsoft.ML.Probabilistic.Distributions;
using Microsoft.ML.Probabilistic.Models;
using Microsoft.ML;
using log4net;
using System.Text;

namespace NWSELib.net
{
    public class InferenceRecord
    {
        
        #region 基本信息   
        /// <summary>
        /// 对应每个维的均值
        /// </summary>
        public List<Vector> means = new List<Vector>();
        /// <summary>
        /// 权重
        /// </summary>
        public double weight;

        /// <summary>
        /// 所有维构成的协方差矩阵
        /// </summary>
        public double[,] covariance;

        /// <summary>
        /// 高斯分布
        /// </summary>
        public VectorGaussian gaussian;

        /// <summary>
        /// 归属节点
        /// </summary>
        public Inference inf;

        /// <summary>
        /// 创建缺省协方差矩阵
        /// </summary>
        /// <returns></returns>
        public double[,] createDefaultCoVariance()
        {
            if (this.means == null) return null;
            this.covariance = new double[this.means.size(), this.means.size()];
            return this.covariance;
        }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="inf"></param>
        public InferenceRecord(Inference inf)
        {
            this.inf = inf;
        }
        #endregion

        #region 临时数据，用于记录未融合采样
        /// <summary>
        /// 密度值
        /// </summary>
        public double density;

        /// <summary>
        /// 接收记录
        /// </summary>
        public List<List<Vector>> acceptRecords = new List<List<Vector>>();

        #endregion

        #region 统计数据
        /// <summary>
        /// 接收数量
        /// </summary>
        public int acceptCount;
        /// <summary>
        /// 评估值（表示按此记录做出行动的结果好坏）
        /// </summary>
        public double evulation;
        /// <summary>
        /// 使用次数
        /// </summary>
        public int usedCount;
        /// <summary>
        /// 表示该前置条件和后置变量映射关系的准确程度
        /// </summary>
        public double accuracy = double.NaN;
        /// <summary>
        /// 抽象之前的记录
        /// </summary>
        public List<InferenceRecord> childs = new List<InferenceRecord>();
        #endregion

        #region 字符串

        static ILog logger = LogManager.GetLogger(typeof(Inference));
        /// <summary>
        /// 字符串，不需要反向解析，因为记录为后天学习，不遗传
        /// </summary>
        /// <param name="xh"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public string toString(Inference inf, int xh, String prefix = "   ")
        {
            StringBuilder str = new StringBuilder();

            str.Append("mean" + xh.ToString() + "=" + meanToString(inf) + System.Environment.NewLine +
                       prefix + "evulation=" + evulation.ToString("F3") + System.Environment.NewLine +
                       prefix + "accuracy=" + accuracy.ToString("F3") + System.Environment.NewLine +
                       prefix + "usedCount=" + usedCount.ToString() + System.Environment.NewLine +
                       prefix + "acceptCount=" + acceptCount.ToString() + System.Environment.NewLine);
            return str.ToString();
        }
        private String meanToString(Inference inf)
        {
            (Vector flattenedValue, List<int> sizes) = this.means.flatten();
            return inf.getValueText(flattenedValue);
        }

        public override string ToString()
        {
            return this.means.toString();
        }

        #endregion

        #region 均值处理
        /// <summary>
        /// 分解均值为条件和结论部分
        /// </summary>
        /// <returns></returns>
        public (List<Vector> condValues, List<Vector> varValues) getMeanValues()
        {
            List<Vector> condValues = new List<Vector>();
            List<Vector> varValues = new List<Vector>();

            int condcount = this.inf.getGene().ConditionCount;
            int varcount = this.inf.getGene().VariableCount;
            condValues.AddRange(this.means.GetRange(0, condcount));
            varValues.AddRange(this.means.GetRange(condcount,varcount));
            return (condValues, varValues);
        }

        /// <summary>
        /// 将记录值分解为环境、动作、后置变量三部分
        /// 这里的动作部分总是包含全部动作，即使inf中不需要动作，也会产生所有动作的缺省值
        /// </summary>
        /// <param name="net"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        public (List<Vector>, List<Vector>, List<Vector>) splitMeans3(Network net)
        {
            //待返回结果
            List<Vector> env = new List<Vector>();
            List<Vector> actions = new List<Vector>();
            List<Vector> expects = new List<Vector>();

            //动作比较特殊，每个动作都要设置结构，尽管该节点可能不涉及
            int actioncount = net.Effectors.Count;
            for (int i = 0; i < actioncount; i++)
                actions.Add(new Vector(0.5));//0.5表示啥都不做

            //分解条件部分
           
            List<(int, int)> conditions = inf.getGene().conditions;
            for (int i = 0; i < conditions.Count; i++)
            {
                Node node = net.getNode(conditions[i].Item1);
                if (node.Gene.IsActionSensor())
                {
                    Effector effector = (Effector)net.Effectors.FirstOrDefault(e => "_" + e.Name == node.Name);
                    int effectorIndex = net.Effectors.IndexOf(effector);
                    actions[effectorIndex] = means[i];
                }
                else
                {
                    env.Add(means[i]);
                }
            }

            int condCount = inf.getGene().ConditionCount;
            int varCount = inf.getGene().VariableCount;
            return (env, actions, this.means.GetRange(condCount, varCount));
        }

        // <summary>
        /// 将记录值分解为环境、后置变量部分
        /// </summary>
        /// <param name="net"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        public (List<Vector>, List<Vector>) splitRecordMeans2()
        {
            return this.getMeanValues();
        }
        /// <summary>
        /// 取得条件中的一部分
        /// </summary>
        /// <param name="condIds"></param>
        /// <returns></returns>
        public List<Vector> getConditionValueByIds(List<int> condIds)
        {
            List<Vector> r = new List<Vector>();
            List<(int, int)> conditions = this.inf.getGene().conditions;
            for(int i=0;i<conditions.Count;i++)
            {
                if (condIds.Contains(conditions[i].Item1))
                    r.Add(this.means[i]);
            }
            return r;
        }
        #endregion

        #region  统计学习功能
        /// <summary>
        /// 高斯采样
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<List<Vector>> sample(int count)
        {
            this.initGaussian();
            try
            {
                List<int> dimension = means.ConvertAll(v => v.Size);
                List<List<Vector>> r = new List<List<Vector>>();
                for (int i = 0; i < count; i++)
                    r.Add(this.gaussian.Sample().fromMathVector(dimension));
                return r;
            }
            catch (Exception e)
            {
                //这里异常一般是协方差矩阵是奇异矩阵造成的，修改协方差矩阵
                logger.Error(e.Message);
                for (int i = 0; i < this.covariance.GetLength(0); i++)
                    this.covariance[i, i] += 0.001;
                this.gaussian = null;
                this.initGaussian();
                return sample(count);
            }
        }
        /// <summary>
        /// 根据均值和协方差数据生成高斯对象
        /// </summary>
        public void initGaussian()
        {
            if (gaussian != null) return;
            Microsoft.ML.Probabilistic.Math.Vector mean = null;
            Microsoft.ML.Probabilistic.Math.PositiveDefiniteMatrix covar = null;
            try
            {
                mean = this.means.toMathVector();
                covar = new Microsoft.ML.Probabilistic.Math.PositiveDefiniteMatrix(covariance);
                gaussian = VectorGaussian.FromMeanAndVariance(mean, covar);
            }
            catch (Exception e)
            {
                //这里异常的主要原因是得到的协方差矩阵不是正定矩阵。
                //This happens if the diagonal values of the covariance matrix are (very close to) zero. 
                //A simple fix is add a very small constant number to c.
                logger.Error(e.Message);
                for (int i = 0; i < covariance.GetLength(0); i++)
                {
                    for (int j = 0; j < covariance.GetLength(1); j++)
                    {
                        if (i == j) covariance[i, j] += 0.001;
                        //if (i == j && covariance[i, j] < 0) covariance[i, j] += 0.01;
                        //if (i != j) covariance[i,j] = 0;
                    }
                }
                covar = new Microsoft.ML.Probabilistic.Math.PositiveDefiniteMatrix(covariance);
                gaussian = VectorGaussian.FromMeanAndVariance(mean, covar);
            }
        }

        /// <summary>
        /// 高斯分布概率值
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal double prob(List<Vector> values)
        {
            this.initGaussian();
            Microsoft.ML.Probabilistic.Math.Vector v = values.toMathVector();
            return Math.Exp(gaussian.GetLogProb(v));
        }
        /// <summary>
        /// 给定值距均值的马氏距离
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public double mahalanobis_distance(List<Vector> values)
        {
            Microsoft.ML.Probabilistic.Math.Vector value = values.toMathVector();

            Microsoft.ML.Probabilistic.Math.Matrix m = new Microsoft.ML.Probabilistic.Math.Matrix(value.Count, 1);
            m.SetTo(value.ToArray());

            return Math.Sqrt((m.Transpose() * this.gaussian.GetVariance().Inverse() * m)[0, 0]);
        }
        /// <summary>
        /// 前向推理
        /// 单个高斯的前向推理有两种方案：
        /// 1.只要条件满足，返回均值
        /// 2.只要条件满足，返回高斯采样中和条件最近的
        /// 目前实现的只是第1种
        /// </summary>
        /// <param name="inf"></param>
        /// <param name="allCondValues"></param>
        /// <returns></returns>
        public List<Vector> forward_inference(Inference inf, List<Vector> allCondValues)
        {
            return this.getMeanValues().varValues;
        }
        #endregion

        #region 自适应学习
        /// <summary>
        /// 对推理记录的均值和协方差矩阵进行调整
        /// Adjust the mean and covariance matrix of the record of inference node
        /// </summary>
        /// <param name="net"></param>
        /// <param name="inf"></param>
        internal void do_adjust(Network net)
        {
            List<int> dimensions = inf.getGene().Dimensions;
            int totaldimension = dimensions.Sum();
            List<List<Vector>> values = new List<List<Vector>>();
            for (int i = 0; i < Session.GetConfiguration().learning.inference.accept_max_count; i++)
                values.Add(this.means);
            for (int i = 0; i < this.acceptRecords.Count; i++)
                values.Add(this.acceptRecords[i]);
            List<Vector> flatten = values.ConvertAll(v => v.flatten()).ConvertAll(p => p.Item1);
            Vector flatten_mean = flatten.average();
            this.covariance = Vector.covariance(flatten.ToArray());

            this.gaussian = null;
            this.initGaussian();
            this.acceptRecords.Clear();

        }

        /// <summary>
        /// 根据前后观察验证该记录的准确度
        /// 以time为后置变量获得时间，若前置条件匹配，则检查后置变量是否与实际匹配
        /// </summary>
        /// <param name="net"></param>
        /// <returns></returns>
        public double adjustAccuracy(int time)
        {
            //取得inf的真实条件值和后置变量值
            (List<Vector> realCondValues, List<Vector> realVarValues) = inf.getValues2(inf.net, time);
            //判断条件部分是否匹配
            double dc = 0;
            bool match = this.isConditionValueMatch(realCondValues, out dc);
            if (!match) return this.accuracy;

            List<double> dvs = this.ditanceFromVariable(realVarValues);

            int n1 = realCondValues.size(), n2 = realVarValues.size();
            //this.accuracy = 1 - (n1 * dc + dvs.Sum()) / (n1 + n2);
            this.accuracy = 1 - dvs.Average();

            return this.accuracy;
        }
        #endregion

        #region 环境与推理的匹配性检查

        
        
        public bool isConditionValueMatch(List<Vector> condValues, out double distance)
        {
            List<double> distances = distanceFromCondition(condValues);
            distance = distances.Average();
            return distances.All(d => d <= 1);
        }

        public bool isVariableMatch(List<Vector> varValues,out double distance)
        {
            List<double> distances = ditanceFromVariable(varValues);
            distance = distances.Average();
            return distances.All(d => d <= 1);
        }

        

        public List<double> distanceFromCondition(List<Vector> value)
        {
            Vector v1 = value.flatten().Item1;
            Vector v2 = this.means.flatten().Item1;

            List<double> distances = new List<double>();
            for (int i = 0; i < this.inf.conditionReceptors.Count; i++)
            {
                double d = this.inf.conditionReceptors[i].distance(v1[i], v2[i]);
                distances.Add(d);
            }

            return distances;
        }

        public List<double> ditanceFromVariable(List<Vector> value)
        {
            Vector v1 = value.flatten().Item1;
            Vector v2 = this.getMeanValues().varValues.flatten().Item1;

            List<double> distances = new List<double>();
            for (int i = 0; i < this.inf.variablesReceptors.Count; i++)
            {
                double d = this.inf.variablesReceptors[i].distance(v1[i], v2[i]);
                distances.Add(d);
            }

            return distances;
        }

        
        public List<double> distance(List<Vector> value)
        {
            Vector v1 = value.flatten().Item1;
            Vector v2 = this.means.flatten().Item1;

            List<double> distances = new List<double>();
            for (int i = 0; i < this.inf.receptors.Count; i++)
            {
                double d = this.inf.receptors[i].distance(v1[i], v2[i]);
                distances.Add(d);
            }
            return distances;
        }


        /// <summary>
        /// 本记录的均值中条件部分与输入值的曼哈顿距离
        /// </summary>
        /// <param name="net"></param>
        /// <param name="inf"></param>
        /// <param name="condValues"></param>
        /// <returns></returns>
        public double getConditionValueDistance(Network net, Inference inf, List<Vector> condValues)
        {
            (List<Vector> meanCondValues, List<Vector> meanVarValues) = this.getMeanValues();

            return Vector.max_manhantan_distance(condValues, meanCondValues);
        }

        


        #endregion
    }
}
