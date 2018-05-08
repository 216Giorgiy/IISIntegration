// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Xunit;

namespace Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests
{
    [Collection(IISTestSiteCollection.Name)]
    public class WebSocketsTests
    {
        private readonly string _webSocketUri;

        public WebSocketsTests(IISTestSiteFixture fixture)
        {
            _webSocketUri = fixture.BaseUri.Replace("http:", "ws:");
        }

        [ConditionalFact]
        public async Task CanSendAndReceieveData()
        {
            var data = Enumerable.Range(0, 10 * 1024 * 1024).Select(i => (byte)i).ToArray();
            var received = new byte[data.Length];

            var cws = new ClientWebSocket();
            await cws.ConnectAsync(new Uri(_webSocketUri + "WebSocketEcho"), default);
            await cws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, default);

            var offset = 0;
            WebSocketReceiveResult result;
            do
            {
                result = await cws.ReceiveAsync(new ArraySegment<byte>(received, offset, received.Length - offset), default);
                offset += result.Count;
            } while (!result.EndOfMessage);

            Assert.Equal(data.Length, offset);
            Assert.Equal(data, received);
        }
    }
}
