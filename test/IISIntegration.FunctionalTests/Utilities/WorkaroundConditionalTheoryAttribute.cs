// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests
{
    // For some reason ConditionalTheoryAttribute from Microsoft.AspNetCore.Testing doesn't run the member data skip behavior
    // from ConditionalTheoryDiscoverer, but it works locally if we paste the theory attribute into the local project.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Microsoft.AspNetCore.Testing.xunit.ConditionalTheoryDiscoverer", "Microsoft.AspNetCore.Testing")]
    public class WorkaroundConditionalTheoryAttribute : TheoryAttribute
    {
    }
}
