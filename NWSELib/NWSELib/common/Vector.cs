using System.Collections.Generic;
using System.Linq;

namespace NWSELib.common
{
    /// <summary>
    /// ����
    /// </summary>
    public class Vector
    {
        #region ������Ϣ
        /// <summary>
        /// ����ֵ
        /// </summary>
        private readonly List<double> values = new List<double>();
        /// <summary>
        /// �������
        /// </summary>
        private int maxCapacity;
        /// <summary>
        /// ��С���ܸı�
        /// </summary>
        private bool fixedSize;

        /// <summary>
        /// �������
        /// </summary>
        public int MaxCapacity { get => maxCapacity; set => maxCapacity = value; }

        /// <summary>
        /// ��С���ܸı�
        /// </summary>
        public bool FixedSize { get => fixedSize; set => fixedSize = value; }

        #endregion

        #region ��ʼ��
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
        #endregion

        #region ֵ����
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
        #endregion
    }
    public static class VectorExtender
    {

        #region ������
        public static int maxSize(this List<Vector> vs)
        {
            return vs.ConvertAll<int>(v => v.Size).Max();
        }
        /// <summary>
        /// ȡ��ĳ�е�ֵ
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
        /// ������������ƽ��ֵ
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
        #endregion


    }
}