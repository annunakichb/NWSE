namespace NWSELib.genome
{
    public class EffectorGene : NodeGene
    {
        public override T clone<T>() 
        {
            return new EffectorGene(this.owner).copy<T>(this);
            
        }
        /// <summary>
        /// ���캯��
        /// </summary>
        /// <param name="genome">Ⱦɫ��</param>
        public EffectorGene(NWSEGenome genome):base(genome)
        {
           
        }


    }
}