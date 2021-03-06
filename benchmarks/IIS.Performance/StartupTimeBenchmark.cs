// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Server.IIS.Performance
{
    [AspNetCoreBenchmark(typeof(FirstRequestConfig))]
    public class StartupTimeBenchmark
    {
        private IApplicationDeployer _deployer;
        public HttpClient _client;

        [IterationSetup]
        public void Setup()
        {
            var deploymentParameters = new DeploymentParameters(Path.Combine(TestPathUtilities.GetSolutionRootDirectory("IISIntegration"), "test/Websites/InProcessWebSite"),
                ServerType.IISExpress,
                RuntimeFlavor.CoreClr,
                RuntimeArchitecture.x64)
            {
                ServerConfigTemplateContent = File.ReadAllText("Http.config"),
                SiteName = "HttpTestSite",
                TargetFramework = "netcoreapp2.1",
                ApplicationType = ApplicationType.Portable,
                ANCMVersion = ANCMVersion.AspNetCoreModuleV2
            };
            _deployer = ApplicationDeployerFactory.Create(deploymentParameters, NullLoggerFactory.Instance);
            _client = _deployer.DeployAsync().Result.HttpClient;
        }

        [IterationCleanup]
        public void Cleanup()
        {
            _deployer.Dispose();
        }

        [Benchmark]
        public async Task SendFirstRequest()
        {
            var response = await _client.GetAsync("");
        }
    }
}
