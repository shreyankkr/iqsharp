using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Quantum.IQSharp
{
    public class ExecutionPath
    {
        public ExecutionPath(QubitDeclaration[] qubits, Operation[] operations)
        {
            this.qubits = qubits;
            this.operations = operations;
        }

        public QubitDeclaration[] qubits { get; set; }

        public Operation[] operations { get; set; }

        public string ToJson(bool prettyPrint = false)
        {
            var options = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                WriteIndented = prettyPrint,
            };
            return JsonSerializer.Serialize(this, options);
        }
    }

    public class QubitDeclaration
    {
        public QubitDeclaration(int id, int? numChildren = null)
        {
            this.id = id;
            this.numChildren = numChildren;
        }

        public int id { get; set; }

        public int? numChildren { get; set; }
    }

    public class Operation
    {
        public string gate { get; set; }

        public string argStr { get; set; }

        public List<List<Operation>> children { get; set; }

        public bool controlled { get; set; }

        public bool adjoint { get; set; }

        public List<Register> controls { get; set; } = new List<Register>();

        public List<Register> targets { get; set; } = new List<Register>();
    }
}
