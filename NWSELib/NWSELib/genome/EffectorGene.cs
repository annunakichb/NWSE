using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NWSELib.genome
{
    public class EffectorGene : NodeGene
    {
        /// <summary>
        /// ��������
        /// </summary>
        protected string group;
        /// <summary>
        /// ��������
        /// </summary>
        public String Group
        {
            get => this.group;
        }
    }
}