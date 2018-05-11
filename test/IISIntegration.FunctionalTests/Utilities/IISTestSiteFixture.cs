// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests
{
    public class IISTestSiteFixture : IDisposable
    {
        private readonly ApplicationDeployer _deployer;

        public IISTestSiteFixture()
        {
            var deploymentParameters = new DeploymentParameters(Helpers.GetInProcessTestSitesPath(),
                ServerType.IISExpress,
                RuntimeFlavor.CoreClr,
                RuntimeArchitecture.x64)
            {
                ServerConfigTemplateContent = File.ReadAllText("AppHostConfig/Http.config"),
                SiteName = "HttpTestSite",
                TargetFramework = Tfm.NetCoreApp21,
                ApplicationType = ApplicationType.Portable,
                AncmVersion = AncmVersion.AspNetCoreModuleV2,
                HostingModel = HostingModel.InProcess,
            };

            _deployer = ApplicationDeployerFactory.Create(deploymentParameters, NullLoggerFactory.Instance);
            DeploymentResult = _deployer.DeployAsync().Result;
            Client = DeploymentResult.HttpClient;
            BaseUri = DeploymentResult.ApplicationBaseUri;
            ShutdownToken = DeploymentResult.HostShutdownToken;
        }

        public string BaseUri { get; }
        public HttpClient Client { get; }
        public CancellationToken ShutdownToken { get; }
        public DeploymentResult DeploymentResult { get; }

        public void Dispose()
        {
            _deployer.Dispose();
        }
    }
}
