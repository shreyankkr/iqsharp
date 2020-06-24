using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Quantum.Simulation.Core;

#nullable enable

namespace Microsoft.Quantum.IQSharp
{
    public class ExecutionPathTracer
    {
        private int currDepth = 0;
        private int renderDepth;
        private IDictionary<int, QubitRegister> qubitRegisters = new Dictionary<int, QubitRegister>();
        private IDictionary<int, List<ClassicalRegister>> classicalRegisters = new Dictionary<int, List<ClassicalRegister>>();
        private List<Operation> operations = new List<Operation>();
        private string[] nestedTypes = new string[]
        {
            typeof(Microsoft.Quantum.Canon.ApplyToEach<Qubit>).ToString(),
            typeof(Microsoft.Quantum.Canon.ApplyToEachC<Qubit>).ToString(),
            typeof(Microsoft.Quantum.Canon.ApplyToEachA<Qubit>).ToString(),
            typeof(Microsoft.Quantum.Canon.ApplyToEachCA<Qubit>).ToString(),
        };

        public ExecutionPathTracer(int depth = 1) => this.renderDepth = depth + 1;

        public ExecutionPath GetExecutionPath()
        {
            var qubits = this.qubitRegisters.Keys
                .OrderBy(k => k)
                .Select(k =>
                {
                    var qubitDecl = new QubitDeclaration(k);
                    if (this.classicalRegisters.ContainsKey(k))
                    {
                        qubitDecl.numChildren = this.classicalRegisters[k].Count;
                    }

                    return qubitDecl;
                })
                .ToArray();

            return new ExecutionPath(qubits, this.operations.ToArray());
        }

        public void OnOperationStartHandler(ICallable operation, IApplyData arguments)
        {
            if (this.nestedTypes.Contains(operation.GetType().ToString())) return;
            this.currDepth++;
            if (this.currDepth == this.renderDepth)
            {
                var operationMetadata = this.OperationToMetadata(operation, arguments);
                if (operationMetadata != null)
                {
                    this.operations.Add(operationMetadata);
                }
            }
        }

        public void OnOperationEndHandler(ICallable operation, IApplyData result)
        {
            if (this.nestedTypes.Contains(operation.GetType().ToString())) return;
            this.currDepth--;
        }

        private static bool IsPartialApplication(ICallable operation)
        {
            var t = operation.GetType();
            if (t == null) return false;

            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(OperationPartial<,,>);
        }

        private QubitRegister GetQubitRegister(Qubit qubit)
        {
            if (!this.qubitRegisters.ContainsKey(qubit.Id))
            {
                this.qubitRegisters[qubit.Id] = new QubitRegister(qubit.Id);
            }

            return this.qubitRegisters[qubit.Id];
        }

        private List<QubitRegister> GetQubitRegisters(IEnumerable<Qubit> qubits)
        {
            return qubits.Select(this.GetQubitRegister).ToList();
        }

        private ClassicalRegister CreateClassicalRegister(Qubit measureQubit)
        {
            var qId = measureQubit.Id;
            if (!this.classicalRegisters.ContainsKey(qId))
            {
                this.classicalRegisters[qId] = new List<ClassicalRegister>();
            }

            var cId = this.classicalRegisters[qId].Count;
            ClassicalRegister register = new ClassicalRegister(qId, cId);
            this.classicalRegisters[qId].Add(register);
            return register;
        }

        private ClassicalRegister GetClassicalRegister(Qubit controlQubit)
        {
            var qId = controlQubit.Id;
            if (!this.classicalRegisters.ContainsKey(qId) || this.classicalRegisters[qId].Count == 0)
            {
                throw new Exception("No classical registers found for qubit {qId}.");
            }

            // Get most recent measurement on given control qubit
            var cId = this.classicalRegisters[qId].Count - 1;
            return this.classicalRegisters[qId][cId];
        }

        private string[] ExtractArgs(Type t, object value)
        {
            List<string?> fields = new List<string?>();

            foreach (var f in t.GetFields())
            {
                if (f.FieldType.IsTuple())
                {
                    var nestedArgs = f.GetValue(value);
                    if (nestedArgs != null)
                    {
                        var nestedFields = this.ExtractArgs(f.FieldType, nestedArgs);

                        // Format tuple args as a tuple string
                        var tupleStr = $"({string.Join(",", nestedFields)})";
                        fields.Add(tupleStr);
                    }
                }
                else if (!f.FieldType.IsQubitsContainer())
                {
                    fields.Add(f.GetValue(value)?.ToString());
                }
            }

            return fields.WhereNotNull().ToArray();
        }

        private Operation? OperationToMetadata(ICallable operation, IApplyData arguments)
        {
            // If operation is a partial application, perform on baseOp recursively.
            if (IsPartialApplication(operation))
            {
                dynamic partialOp = operation;
                dynamic partialOpArgs = arguments;

                // Recursively get base operation operations
                var baseOp = partialOp.BaseOp;
                var baseArgs = baseOp.__dataIn(partialOpArgs.Value);
                return this.OperationToMetadata(baseOp, baseArgs);
            }

            var controlled = operation.Variant == OperationFunctor.Controlled ||
                            operation.Variant == OperationFunctor.ControlledAdjoint;
            var adjoint = operation.Variant == OperationFunctor.Adjoint ||
                          operation.Variant == OperationFunctor.ControlledAdjoint;

            // If operation is controlled, perform on baseOp recursively and mark as controlled.
            if (controlled)
            {
                dynamic ctrlOp = operation;
                dynamic ctrlOpArgs = arguments;

                var ctrls = ctrlOpArgs.Value.Item1;
                var controlRegs = this.GetQubitRegisters(ctrls);

                // Recursively get base operation operations
                var baseOp = ctrlOp.BaseOp;
                var baseArgs = baseOp.__dataIn(ctrlOpArgs.Value.Item2);
                var baseMetadata = this.OperationToMetadata(baseOp, baseArgs);

                baseMetadata.controlled = true;
                baseMetadata.adjoint = adjoint;
                baseMetadata.controls.InsertRange(0, controlRegs);

                return baseMetadata;
            }

            // Handle operation based on type
            switch (operation)
            {
                // Handle CNOT operations as a Controlled X
                case Microsoft.Quantum.Intrinsic.CNOT cnot:
                case Microsoft.Quantum.Intrinsic.CCNOT ccnot:
                    var ctrlRegs = new List<Register>();
                    var targetRegs = new List<Register>();

                    switch (arguments.Value)
                    {
                        case ValueTuple<Qubit, Qubit> cnotQs:
                            var (ctrl, cnotTarget) = cnotQs;
                            ctrlRegs.Add(this.GetQubitRegister(ctrl));
                            targetRegs.Add(this.GetQubitRegister(cnotTarget));
                            break;
                        case ValueTuple<Qubit, Qubit, Qubit> ccnotQs:
                            var (ctrl1, ctrl2, ccnotTarget) = ccnotQs;
                            ctrlRegs.Add(this.GetQubitRegister(ctrl1));
                            ctrlRegs.Add(this.GetQubitRegister(ctrl2));
                            targetRegs.Add(this.GetQubitRegister(ccnotTarget));
                            break;
                    }

                    return new Operation
                    {
                        gate = "X",
                        controlled = true,
                        adjoint = adjoint,
                        controls = ctrlRegs,
                        targets = targetRegs,
                    };

                // Measurement operations
                case Microsoft.Quantum.Intrinsic.M m:
                case Microsoft.Quantum.Measurement.MResetX mx:
                case Microsoft.Quantum.Measurement.MResetY my:
                case Microsoft.Quantum.Measurement.MResetZ mz:
                    var measureQubit = arguments.GetQubits().ElementAt(0);
                    var measureReg = this.GetQubitRegister(measureQubit);
                    var clsReg = this.CreateClassicalRegister(measureQubit);

                    return new Operation
                    {
                        gate = "measure",
                        controlled = false,
                        adjoint = adjoint,
                        controls = new List<Register>() { measureReg },
                        targets = new List<Register>() { clsReg },
                    };

                // Operations to ignore
                case Microsoft.Quantum.Intrinsic.Reset reset:
                case Microsoft.Quantum.Intrinsic.ResetAll resetAll:
                    return null;

                // General operations
                default:
                    Type t = arguments.Value.GetType();
                    var fields = this.ExtractArgs(t, arguments.Value);
                    var argStr = fields.Any() ? $"({string.Join(",", fields)})" : null;
                    var qubitRegs = this.GetQubitRegisters(arguments.GetQubits());

                    return new Operation
                    {
                        gate = operation.Name,
                        argStr = argStr,
                        controlled = false,
                        adjoint = adjoint,
                        controls = new List<Register>(),
                        targets = qubitRegs.Cast<Register>().ToList(),
                    };
            }
        }
    }
}