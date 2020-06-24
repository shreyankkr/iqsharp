namespace Microsoft.Quantum.IQSharp
{
    public enum RegisterType
    {
        Qubit,
        Classical,
    }

    public class Register
    {
        public virtual RegisterType type { get; set; }

        public virtual int qId { get; set; }

        public virtual int? cId { get; set; }
    }

    public class QubitRegister : Register
    {
        public QubitRegister(int qId)
        {
            this.qId = qId;
        }

        public override RegisterType type => RegisterType.Qubit;
    }

    public class ClassicalRegister : Register
    {
        public ClassicalRegister(int qId, int cId)
        {
            this.qId = qId;
            this.cId = cId;
        }

        public override RegisterType type => RegisterType.Classical;
    }
}