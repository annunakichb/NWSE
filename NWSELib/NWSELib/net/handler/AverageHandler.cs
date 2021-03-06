﻿using NWSELib.common;
using NWSELib.genome;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NWSELib.net.handler
{
    public class AverageHandler : Handler
    {
        public AverageHandler(NodeGene gene, Network net) : base(gene,net)
        {
        }
        public override Object activate(Network net, int time, Object value = null)
        {
            List<Node> inputs = net.getInputNodes(this.Id);
            if (!inputs.All(n => n.IsActivate(time)))
                return null;
            int t = time;


            List<Vector> vs = inputs.ConvertAll(node => node.GetValue(time));
            Vector result = vs.average();
            base.activate(net, time, result);
            return result;
        }
        
    }
}

