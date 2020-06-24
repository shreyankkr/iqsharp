// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Jupyter.Core;
using Microsoft.Jupyter.Core.Protocol;
using Microsoft.Quantum.IQSharp.Common;
using Microsoft.Quantum.IQSharp.Jupyter;
using Microsoft.Quantum.IQSharp.Kernel;
using Microsoft.Quantum.Simulation.Core;
using Microsoft.Quantum.Simulation.Simulators;
using Newtonsoft.Json;

namespace Microsoft.Quantum.IQSharp.Kernel
{

    public class ExecutionPathVisualizerContent : MessageContent
    {
        [JsonProperty("json")]
        public string Json { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    /// <summary>
    ///     A magic command that can be used to visualize the execution
    ///     paths of operations and functions.
    /// </summary>
    public class ViewMagic : AbstractMagic
    {
        private const string ParameterNameOperationName = "__operationName__";

        private int count = 0;

        /// <summary>
        ///     Constructs a new magic command given a resolver used to find
        ///     operations and functions, and a configuration source used to set
        ///     configuration options.
        /// </summary>
        public ViewMagic(ISymbolResolver resolver, IConfigurationSource configurationSource) : base(
            "view",
            new Documentation
            {
                Summary = "Outputs an HTML-based visualization of the execution path of a given operation."
            })
        {
            this.SymbolResolver = resolver;
            this.ConfigurationSource = configurationSource;
        }

        /// <summary>
        ///      The symbol resolver used by this magic command to find
        ///      operations or functions to be simulated.
        /// </summary>
        public ISymbolResolver SymbolResolver { get; }

        /// <summary>
        ///     The configuration source used by this magic command to control
        ///     simulation options (e.g.: dump formatting options).
        /// </summary>
        public IConfigurationSource ConfigurationSource { get; }

        /// <inheritdoc />
        public override ExecutionResult Run(string input, IChannel channel) =>
            RunAsync(input, channel).Result;

        /// <summary>
        ///     Simulates an operation given a string with its name and a JSON
        ///     encoding of its arguments.
        /// </summary>
        public async Task<ExecutionResult> RunAsync(string input, IChannel channel)
        {
            var inputParameters = ParseInputParameters(input, firstParameterInferredName: ParameterNameOperationName);

            var name = inputParameters.DecodeParameter<string>(ParameterNameOperationName);
            var symbol = SymbolResolver.Resolve(name) as IQSharpSymbol;
            if (symbol == null) throw new InvalidOperationException($"Invalid operation name: {name}");

            var tracer = new ExecutionPathTracer();

            using var qsim = new QuantumSimulator()
                .WithJupyterDisplay(channel, ConfigurationSource)
                .WithStackTraceDisplay(channel)
                .WithExecutionPathTracer(tracer);
            var value = await symbol.Operation.RunAsync(qsim, inputParameters);
            var executionPath = tracer.GetExecutionPath();

            var content = new ExecutionPathVisualizerContent
            {
                Json = executionPath.ToJson(),
                Id = $"execution-path-container-{count}",
            };
            count++;

            channel.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "render_execution_path"
                    },
                    Content = content,
                }
            );

            var res = new ExecutionPathDisplayable { Id = content.Id };


            return res.ToExecutionResult();;
        }
    }
}
