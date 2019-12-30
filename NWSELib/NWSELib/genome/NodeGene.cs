﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NWSELib.genome
{
    public class NodeGene
    {
        /// <summary>
        /// 对应感受器名称
        /// </summary>
        protected String name;

        /// <summary>生成的进化年代</summary>
        protected int generation;

        /// <summary>
        /// 分段数
        /// </summary>
        protected int sectionCount;

        /// <summary>
        /// 对应感受器名称
        /// </summary>
        public string Name { get => name; set => name = value; }
        /// <summary>
        /// 生成的进化年代
        /// </summary>
        public int Generation { get => generation; set => generation = value; }

        /// <summary>
        /// 每层的分段数
        /// </summary>
        public int SectionCount { get => sectionCount; set => sectionCount = value; }
    }
}