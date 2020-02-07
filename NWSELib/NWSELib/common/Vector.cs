using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.common
{
    /// <summary>
    /// 向量
    /// </summary>
    public class Vector
    {
        #region 基本信息
        /// <summary>
        /// 向量值
        /// </summary>
        private readonly List<double> values = new List<double>();
        /// <summary>
        /// 最大容量
        /// </summary>
        private int maxCapacity;
        /// <summary>
        /// 大小不能改变
        /// </summary>
        private bool fixedSize;

        /// <summary>
        /// 最大容量
        /// </summary>
        public int MaxCapacity { get => maxCapacity; set => maxCapacity = value; }

        /// <summary>
        /// 大小不能改变
        /// </summary>
        public bool FixedSize { get => fixedSize; set => fixedSize = value; }

        #endregion

        #region 初始化
        public Vector setCapacitySize(bool fixedSize = false, int maxCapacity = 0)
        {
            this.fixedSize = fixedSize;
            this.maxCapacity = maxCapacity;
            return this;
        }

        public Vector(bool fixedSize = false, int maxCapacity = 0)
        {
            this.MaxCapacity = maxCapacity;
            this.FixedSize = fixedSize;
            this.initSize();

        }
        public Vector(bool fixedSize, int maxCapacity = 0, params double[] values)
        {
            if (values != null)
                this.values.AddRange(values);
            this.MaxCapacity = maxCapacity;
            this.FixedSize = fixedSize;
            this.initSize();
        }

        public Vector(params double[] values)
        {
            this.values.AddRange(values);
            this.initSize();
        }
        public Vector(List<double> values, bool fixedSize = false, int maxCapacity = 0)
        {
            if (values != null)
                this.values.AddRange(values);
            this.MaxCapacity = maxCapacity;
            this.FixedSize = fixedSize;
            this.initSize();
        }


        private void initSize()
        {
            if (FixedSize && this.MaxCapacity <= 0) this.MaxCapacity = 1;
            if (this.FixedSize)
            {
                for (int i = this.values.Count; i < MaxCapacity; i++)
                    this.values.Add(0.0);
            }
        }

        public Vector clone()
        {
            Vector v = new Vector();
            v.fixedSize = this.fixedSize;
            v.maxCapacity = this.maxCapacity;
            v.values.AddRange(this.values);
            return v;
        }

        public override String ToString()
        {
            Vector v = this;
            if (v == null || v.Size <= 0) return "";
            return v.ToList().ConvertAll(x => x.ToString("F4"))
                .Aggregate((a, b) => a + "," + b);
        }
        #endregion

        #region 值运算
        public List<double> ToList()
        {
            return new List<double>(this.values);
        }
        public double[] ToArray()
        {
            return this.values.ToArray();
        }
        public double this[int index]
        {
            get => this.values[index];
            set => this.values[index] = value;
        }
        public double Value
        {
            get => this.values[0];
            set => this.values[0] = value;
        }

        public int Size
        {
            get => this.values.Count;
        }
        public static implicit operator double(Vector v)
        {
            return v.values[0];
        }
        public static implicit operator Vector(double v)
        {
            return new Vector(true, 1, new double[] { v });
        }
        public static implicit operator List<double>(Vector v)
        {
            return v.ToList();
        }
        public static implicit operator Vector(List<double> v)
        {
            return new Vector(v).setCapacitySize(true, v.Count);
        }
        public double average()
        {
            return this.values.Average();
        }

        public double distance(Vector v)
        {
            int size = VectorExtender.maxSize(this,v);
            double dis = 0;
            for(int i=0;i<size;i++)
            {
                double d1 = this.Size <= i ? 0 : this[i];
                double d2 = v.Size <= i ? 0 : v[i];
                dis += (d1-d2) * (d1 - d2);
            }
            return System.Math.Sqrt(dis);
        }
        public double length()
        {
            return System.Math.Sqrt(this.values.ToList().ConvertAll(v => v * v).Aggregate((x, y) => x + y));
        }

        public Vector normalize()
        {
            Vector v = this.clone();
            double len = this.length();
            for (int i = 0; i < this.Size; i++)
            {
                v[i] = this[i] / len;
            }
            return v;
        }

        public static Vector operator -(Vector v1, Vector v2)
        {
            int size = VectorExtender.maxSize();
            Vector v = new Vector(true, size);
            for (int i = 0; i < size; i++)
            {
                if (v1.Size <= i)
                {
                    v[i] = -v2[i];
                    break;
                }
                else if (v2.Size <= i)
                {
                    v[i] = v1[i];
                }
                else
                {
                    v[i] = v1[i] - v2[i];
                }

            }
            return v;
        }
        /// <summary>
        /// 计算向量组的协方差矩阵
        /// </summary>
        /// <param name="vs"></param>
        /// <returns></returns>
        public static double[,] covariance(params Vector[] vs)
        {
            int size = VectorExtender.maxSize(vs);
            List<Vector> cv = new List<Vector>();
            List<double> avg = new List<double>();

            for (int i = 0; i < size; i++)
            {
                cv.Add(VectorExtender.column(vs.ToList(), i));
                avg.Add(cv[i].average());
            }

            int dim = avg.Count;
            int n = cv[0].Size;
            Vector r = new Vector(true, dim * (dim + 1) / 2);
            double[,] result = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    if (i > j) { result[i, j] = result[j, i]; continue; }
                    double sum = 0.0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += (cv[i][k] - avg[i]) * (cv[j][k] - avg[j]);
                    }
                    sum /= (n - 1);
                    result[i, j] = sum;
                }
            }
            return result;
        }
        
        #endregion

        #region 统计
        public (double,double) avg_variance()
        {
            double avg = this.values.Average();
            double s = 0.0;
            for(int i=0;i<this.values.Count;i++)
            {
                s += (this.values[i] - avg) * (this.values[i] - avg);
            }
            return (avg, System.Math.Sqrt(s));
        }
        public double manhantan_distance(Vector v)
        {
            int size = Math.Max(this.Size, v.Size);
            double d = 0;
            for(int i=0;i<size;i++)
            {
                if (i >= this.Size) d += v[i];
                else if (i >= v.Size) d += this[i];
                else d += Math.Abs(v[i]-this[i]);
            }
            return d;
        }
        public double max_manhantan_distance(Vector v)
        {
            int size = Math.Min(this.Size, v.Size);
            double d = double.MinValue;
            for (int i = 0; i < size; i++)
            {
                if (d < Math.Abs(v[i] - this[i]))
                    d = Math.Abs(v[i] - this[i]);
            }
            return d;
        }
        public static double manhantan_distance(List<Vector> v1, List<Vector> v2)
        {
            return v1.flatten().Item1.manhantan_distance(v2.flatten().Item1);
        }
        public static double max_manhantan_distance(List<Vector> v1, List<Vector> v2)
        {
            return v1.flatten().Item1.max_manhantan_distance(v2.flatten().Item1);
        }


        public static bool equals(Vector v1,Vector v2)
        {
            if (v1 == v2) return true;
            if (v1 == null || v2 == null) return false;
            if (v1.Size != v2.Size) return false;
            for(int i=0;i<v1.Size;i++)
            {
                if (v1[i] != v2[i]) return false;
            }
            return true;
        }

        public static bool equals(List<Vector> v1, List<Vector> v2)
        {
            if (v1 == v2) return true;
            if (v1 == null || v2 == null) return false;
            if (v1.Count != v2.Count) return false;
            for (int i = 0; i < v1.Count; i++)
            {
                if (!equals(v1[i],v2[i])) return false;
            }
            return true;
        }



        #endregion
    }
    public static class VectorExtender
    {
        public static int maxSize(params Vector[] vs)
        {
            if (vs == null || vs.Length <= 0) return 0;
            return vs.ToList().ConvertAll<int>(v => v.Size).Max();
        }
        #region 向量集
        public static int maxSize(this List<Vector> vs)
        {
            return vs.ConvertAll<int>(v => v.Size).Max();
        }
        public static int size(this List<Vector> vs)
        {
            return vs.ConvertAll(v => v==null?0:v.Size).Sum();
        }
        /// <summary>
        /// 取得某列的值
        /// </summary>
        /// <param name="vs"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        public static Vector column(this List<Vector> vs, int column)
        {
            Vector v = new Vector(true, vs.Count);
            for (int i = 0; i < vs.Count; i++)
                v[i] = vs[i][column];
            return v;
        }
        /// <summary>
        /// 计算向量集的平均值
        /// </summary>
        /// <param name="vs"></param>
        /// <returns></returns>
        public static Vector average(this List<Vector> vs)
        {
            int size = maxSize(vs);
            Vector v = new Vector(true, size);
            for (int i = 0; i < size; i++)
            {
                v[i] = column(vs, i).average();
            }
            return v;
        }

        /// <summary>
        /// 将向量集摊平成一个向量
        /// </summary>
        /// <param name="vs"></param>
        /// <returns></returns>
        public static (Vector,List<int>) flatten(this List<Vector> vs)
        {
            int size = vs.ConvertAll(v1 => v1==null?0:v1.Size).Sum();
            Vector v = new Vector(true, size);
            List<int> sizes = new List<int>();
            int k = 0;
            for(int i=0;i<vs.Count;i++)
            {
                for (int j = 0; j < vs[i].Size; j++)
                    v[k++] = vs[i][j];
                sizes.Add(vs[i].Size);
            }
            return (v, sizes);
        }
        
        /// <summary>
        /// 将向量分割成向量集
        /// </summary>
        /// <param name="v"></param>
        /// <param name="sizes"></param>
        /// <returns></returns>
        public static List<Vector> split(this Vector v,List<int> sizes)
        {
            List<Vector> r = new List<Vector>();
            int k = 0;
            for(int i=0;i<sizes.Count;i++)
            {
                Vector temp = new Vector(true, sizes[i]);
                for(int j=0;j<sizes[i];j++)
                {
                    temp[j] = v[k++];
                }
                r.Add(temp);
            }
            return r;
        }

        /// <summary>
        /// 距离:将向量集摊平成一个向量，然后计算两个向量集的距离
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public static double distance(this List<Vector> v1,List<Vector> v2)
        {
            (Vector vv1,List<int> size1) = v1.flatten();
            (Vector vv2, List<int> size2) = v2.flatten();
            return vv1.distance(vv2);

        }

        public static String toString(this List<Vector> vs)
        {
            if (vs == null || vs.Count <= 0) return "";
            return vs.ConvertAll(v => v.ToString()).Aggregate((a, b) => a + ";" + b);
        }

        
        #endregion

        #region 转换

        public static double[] toDoubleArray(this List<Vector> vs)
        {
            List<double> r = new List<double>();
            vs.ForEach(v => r.AddRange(v.ToArray()));
            return r.ToArray();
        }
        public static List<double> trim(List<double> vs,int begin,int count)
        {
            List<double> r = new List<double>();
            for (int i = 0; i < count; i++)
                r.Add(vs[begin+i]);
            return r;
        }
        public static Microsoft.ML.Probabilistic.Math.Vector toMathVector(this List<Vector> vs)
        {
            return Microsoft.ML.Probabilistic.Math.Vector.FromArray(vs.toDoubleArray());
        }
        public static List<Vector> fromMathVector(this Microsoft.ML.Probabilistic.Math.Vector v,List<int> dimension)
        {
            Vector[] vs = new Vector[dimension.Count];
            int pos = 0;
            List<double> values = v.ToList();
            for(int i=0;i<dimension.Count;i++)
            {
                vs[i] = new Vector(trim(values,pos,dimension[i]));
                pos += dimension[i];
            }
            return new List<Vector>(vs);
        }
        #endregion

        
    }
}