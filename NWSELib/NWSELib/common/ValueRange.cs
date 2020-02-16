using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Microsoft.ML.Probabilistic.Distributions;

namespace NWSELib.common
{

    /// <summary>
    /// 特定取值范围
    /// Range of values
    /// Different from range in python, it can be divided into three types: 
    /// value enumeration, arithmetic sequences and  geometrical sequences
    /// </summary>
    public class ValueRange : IEnumerable<double>,IEnumerator<double>
    {
        #region 基本信息
        public enum Cataory
        {
            /// <summary>并不是真正的范围，而是枚举了有效个值</summary>
            Enum = 1,
            /// <summary>等差序列</summary>
            EdiffSeries = 2,
            /// <summary>等比序列</summary>
            EratioSeries = 4,
            /// <summary>定制</summary>
            Custom = 8,
        }

        /// <summary>类别</summary>
        public readonly Cataory cataory;
        /// <summary>包括最小值</summary>
        public readonly bool includeMin;
        /// <summary>包括最大值</summary>
        public readonly bool includeMax;
        /// <summary>种子值列表，即用于生成值的列表</summary>
        public readonly List<double> values = new List<double>();
        /// <summary>距离,只能是三种类型：int，double，Range</summary>
        public readonly Object distance;
        /// <summary>距离递推函数</summary>
        public readonly Func<double[], double> recursiveFunc;
        /// <summary>距离递推函数第一个参数的元素个数</summary>
        public readonly int recursiveFuncMaxHistory = 1;

        #endregion

        #region 迭代
        /// <summary>
        /// 迭代的时候的多线程支持
        /// </summary>
        private ThreadLocal<Object> state_next = new ThreadLocal<Object>();
       
        /// <summary>
        /// 缺省递推函数
        /// </summary>
        /// <returns></returns>
        private double _defaultRecursiveFunc()
        {
            double value = 0;
            if (cataory == Cataory.Enum)
            {
                if (!state_next.IsValueCreated)
                    state_next.Value = includeMin ? 0 : 1;

                if ((int)state_next.Value >= this.values.Count)
                    throw new ArgumentOutOfRangeException("Range");

                double r = this.values[(int)(state_next.Value)];
                state_next.Value = (int)(state_next.Value) + 1;
                return r;
            }
            else if(cataory == Cataory.EdiffSeries)
            {
                if (!state_next.IsValueCreated)
                {
                    state_next.Value = includeMin ? this.values[0]:this.values[0]+(double)this.distance;
                    return (double)state_next.Value;
                }
                value = (double)state_next.Value + (double)this.distance;
                if (this.values.Count <= 1)
                {
                    value += (double)this.distance;
                    state_next.Value = value;
                    return value;
                }
                if((includeMax && value>this.values[1]) || (!includeMax && value>=this.values[1]))
                {
                    throw new ArgumentOutOfRangeException("Range");
                }
                value += (double)this.distance;
                state_next.Value = value;
                return value;
            }else if(cataory == Cataory.EratioSeries)
            {
                if (!state_next.IsValueCreated)
                {
                    state_next.Value = includeMin ? this.values[0] : this.values[0] * (double)this.distance;
                    return (double)state_next.Value;
                }
                value = (double)state_next.Value + (double)this.distance;
                if (this.values.Count <= 1)
                {
                    value *= (double)this.distance;
                    state_next.Value = value;
                    return value;
                }
                if ((includeMax && value > this.values[1]) || (!includeMax && value >= this.values[1]))
                {
                    throw new ArgumentOutOfRangeException("Range");
                }
                value *= (double)this.distance;
                state_next.Value = value;
                return value;
            }

            if (!state_next.IsValueCreated)
            {
                state_next.Value = new Queue<double>(this.recursiveFuncMaxHistory);

            }
            double[] history = ((Queue<double>)state_next.Value).ToArray();
            value = recursiveFunc(history);
            ((Queue<double>)state_next.Value).Enqueue(value);
            return value;
        }

        IEnumerator<double> IEnumerable<double>.GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        bool IEnumerator.MoveNext()
        {
            double value = 0;
            if (cataory == Cataory.Enum)
            {
                if (!state_next.IsValueCreated)
                    return true;

                if ((int)state_next.Value >= this.values.Count)
                    return false;

                return true;
            }
            else if (cataory == Cataory.EdiffSeries)
            {
                if (!state_next.IsValueCreated)
                    return true;
                value = (double)state_next.Value + (double)this.distance;
                if (this.values.Count <= 1)
                {
                    return true;
                }
                if ((includeMax && value > this.values[1]) || (!includeMax && value >= this.values[1]))
                {
                    return false;
                }
                return true;
            }
            else if (cataory == Cataory.EratioSeries)
            {
                if (!state_next.IsValueCreated)
                {
                    return true;
                }
                value = (double)state_next.Value + (double)this.distance;
                if (this.values.Count <= 1)
                {
                    return true;
                }
                if ((includeMax && value > this.values[1]) || (!includeMax && value >= this.values[1]))
                {
                    return false;
                }
                return true;
            }

            if (!state_next.IsValueCreated)
            {
                state_next.Value = new Queue<double>(this.recursiveFuncMaxHistory);

            }
            try
            {
                double[] history = ((Queue<double>)state_next.Value).ToArray();
                value = recursiveFunc(history);
                return true;
            }
            catch (Exception e) { return false; }
        }

        void IEnumerator.Reset()
        {
            this.state_next = new ThreadLocal<object>();
        }

        void IDisposable.Dispose()
        {
            this.state_next = new ThreadLocal<object>();
        }
        public void Add(Object obj) { }
        /// <summary>
        /// 有效范围
        /// </summary>
        public double Distance
        {
            get => this.values[1] - this.values[0];
        }

        public double Step
        {
            get => (double)this.distance;
        }
        /// <summary>
        /// 最小值
        /// </summary>
        public double Min { get => this.values[0]; }
        /// <summary>
        /// 最大值
        /// </summary>
        public double Max { get => this.values[1]; }
        /// <summary>
        /// 当前值
        /// </summary>
        double IEnumerator<double>.Current
        {
            get
            {
                if (state_next.Value==null) return double.NaN;
                if (state_next.Value is double) return (double)state_next.Value;
                return ((Queue<double>)state_next.Value).ToList().Last();
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (state_next.Value == null) return double.NaN;
                if (state_next.Value is double) return (double)state_next.Value;
                return ((Queue<double>)state_next.Value).ToList().Last();
            }
        }

        static Random rng = new Random();
        public double random()
        {
            if(cataory == Cataory.Enum)
            {
                int index = rng.Next(0, this.values.Count);
                return this.values[index];
            }
            else
            {
                return rng.NextDouble() * (Max - Min) + Min;
            }
        }

        public double gaussian_random()
        {
            double center = (Max - Min) / 2;
            Gaussian gaussian = Gaussian.FromMeanAndVariance(center, 0.001);
            return Math.Max(Math.Min(Max,gaussian.Sample()),Min);
        }
        #endregion

        #region 初始化
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="value">字符串，格式包括：
        /// [0.0,0.1,0.2,0.3]  枚举序列
        /// [0.0-1.0]          范围序列，缺省100个数
        /// [0.0-1.0+0.1]      等差序列
        /// [1.0-100.0*1.2]    等比序列
        /// </param>
        public ValueRange(String value)
        {
            if (value == null) value = "[0.0-1.0]";
            value = value.Trim();

            this.includeMin = value.StartsWith("(") ? false : true;
            this.includeMax = value.EndsWith(")") ? false : true;

            value = value.Substring(1, value.Length - 2);
            int i1 = value.IndexOf(",");
            if (i1 >= 0)
            {
                this.cataory = Cataory.Enum;
                this.values = Utility.parse(value);
                return;
            }

            i1 = value.IndexOf("-");
            //测试下-左边是不是数
            double td = 0;
            if (!double.TryParse(value.Substring(0, i1).Trim(), out td))
                i1 = value.IndexOf("-", i1 + 1);
            String begin = value.Substring(0, i1).Trim();
            int j = i1 + 1;
            this.cataory = Cataory.EdiffSeries;
            for (; j < value.Length; j++)
            {
                if (value[j] == '+') { this.cataory = Cataory.EdiffSeries; break; }
                else if (value[j] == '*') { this.cataory = Cataory.EratioSeries; break; }
            }
            String end = value.Substring(i1 + 1, j - i1-1).Trim();
            this.values = new List<double>(new double[] { double.NaN,double.NaN});
            this.values[0] = double.Parse(begin);
            this.values[1] = double.Parse(end);
            if (j >= value.Length)
            {
                if (this.values[0] == (int)this.values[0]) this.distance = 1;
                else this.distance = (this.values[1] - this.values[0]) / 100;
            }else if(this.cataory == Cataory.EdiffSeries)
            {
                this.distance = value.Substring(j + 1, value.Length - j - 1);
            }else if(this.cataory == Cataory.EratioSeries)
            {
                this.distance = value.Substring(j + 1, value.Length - j - 1);
            }
        }
        /// <summary>
        /// 枚举形式的构造函数
        /// </summary>
        /// <param name="cataory">类别</param>
        /// <param name="includeMin">包括最小值</param>
        /// <param name="includeMax">包括最大值</param>
        /// <param name="values">值列表</param>
        public ValueRange(Cataory cataory, List<double> values, bool includeMin = true, bool includeMax = true)
        {
            this.cataory = cataory;
            this.includeMin = includeMin;
            this.includeMax = includeMax;
            this.values = values;
        }

        /// <summary>
        /// 范围形式的构造函数
        /// </summary>
        /// <param name="cataory">类别</param>
        /// <param name="includeMin">包括最小值</param>
        /// <param name="includeMax">包括最大值</param>
        /// <param name="values">值列表</param>
        public ValueRange(Cataory cataory, List<double> values, bool includeMin = true, bool includeMax = true, double distance = 1.0, Func<double[], double> recursiveFunc = null, int recursiveFuncMaxHistory = 1)
        {
            this.cataory = cataory;
            this.includeMin = includeMin;
            this.includeMax = includeMax;
            this.values = values;
            this.distance = distance;
            this.recursiveFunc = recursiveFunc;
            this.recursiveFuncMaxHistory = recursiveFuncMaxHistory;
        }
        #endregion

        #region 计算
        public bool In(double value)
        {
            if (cataory == Cataory.Enum)
                return this.values.Contains(value);
            return value >= Min && value <= Max;
        }
        #endregion
    }
}
