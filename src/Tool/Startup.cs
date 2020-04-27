// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Quantum.IQSharp.Jupyter;
using Utils = Quantum.Kata.Utils;

namespace Microsoft.Quantum.IQSharp
{
    /// <summary>
    /// StartUp class used when starting as a WebHost (http server)
    /// </summary>
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.Configure<Workspace.Settings>(Program.Configuration);
            services.Configure<NugetPackages.Settings>(Program.Configuration);
            services.Configure<ClientInformation>(Program.Configuration);

            services.AddSingleton(typeof(ITelemetryService), GetTelemetryServiceType());
            services.AddIQSharp();
            services.AddIQSharpKernel();

            services.AddSingleton<IReferences, References>(AddBuiltInAssemblies);

            var assembly = typeof(PackagesController).Assembly;
            services.AddControllers()
                .AddApplicationPart(assembly);
        }

        /// <summary>
        /// Adds Q# Libraries as references so they are available for compilation.
        /// </summary>
        public static References AddBuiltInAssemblies(IServiceProvider provider)
        {
            var refs = ActivatorUtilities.CreateInstance<References>(provider);
            refs.AddAssemblies(
                new AssemblyInfo(typeof(Canon.ApplyToEach<>).Assembly),
                new AssemblyInfo(typeof(Katas.KataMagic).Assembly),
                new AssemblyInfo(typeof(Utils.GetOracleCallsCount<>).Assembly),
                new AssemblyInfo(typeof(Chemistry.Magic.BroombridgeMagic).Assembly),
                new AssemblyInfo(typeof(Chemistry.JordanWigner.PrepareTrialState).Assembly),
                new AssemblyInfo(typeof(Research.Characterization.RandomWalkPhaseEstimation).Assembly),
                new AssemblyInfo(typeof(Research.Chemistry.OptimizedTrotterStepOracle).Assembly),
                new AssemblyInfo(typeof(MachineLearning.ApplySequentialClassifier).Assembly),
                new AssemblyInfo(typeof(Arithmetic.AddI).Assembly)
            );

            return refs;
        }

        private Type GetTelemetryServiceType()
        {
            return
#if TELEMETRY
                Program.TelemetryOptOut ? typeof(NullTelemetryService) : typeof(TelemetryService);
#else
                typeof(NullTelemetryService);
#endif
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

        }
    }
}
