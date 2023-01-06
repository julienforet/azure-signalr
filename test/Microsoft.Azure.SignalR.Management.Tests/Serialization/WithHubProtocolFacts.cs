﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class WithHubProtocolFacts
    {
        private static readonly JsonHubProtocol Json = new JsonHubProtocol(Options.Create(new JsonHubProtocolOptions()
        {
            PayloadSerializerOptions = new() { WriteIndented = true }
        }));
        private static readonly MessagePackHubProtocol MessagePack = new MessagePackHubProtocol();
        public static IEnumerable<object[]> AddProtocolTestData()
        {
            yield return new object[] { Json };
            yield return new object[] { MessagePack };
            yield return new object[] { MessagePack, Json };
        }

        [Theory]
        [MemberData(nameof(AddProtocolTestData))]
        public async Task WithProtocolTest(params IHubProtocol[] hubProtocols)
        {
            var mockConnectionContainer = new Mock<IServiceConnectionContainer>();
            mockConnectionContainer.Setup(c => c.WriteAsync(It.IsAny<BroadcastDataMessage>()))
                .Callback<ServiceMessage>(message =>
                {
                    var m = message as BroadcastDataMessage;
                    Assert.Equal(hubProtocols.Length, m.Payloads.Count);
                    foreach (var hubProtocol in hubProtocols)
                    {
                        var expectedMessageBytes = hubProtocol.GetMessageBytes(new InvocationMessage("target", new object[] { "argument" }));
                        Assert.True(expectedMessageBytes.Span.SequenceEqual(m.Payloads[hubProtocol.Name].Span));
                    }
                });
            var hubContext = await new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
                        o.ServiceTransportType = ServiceTransportType.Persistent;
                    })
                    .WithHubProtocols(hubProtocols)
                    .ConfigureServices(services => services.AddSingleton(mockConnectionContainer.Object))
                    .BuildServiceManager()
                    .CreateHubContextAsync("hub", default);
            await hubContext.Clients.All.SendAsync("target", "argument");
        }

        [Fact]
        public async Task AddMessagePackTest()
        {
            var mockConnectionContainer = new Mock<IServiceConnectionContainer>();
            var expectedHubProtocols = new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() };
            mockConnectionContainer.Setup(c => c.WriteAsync(It.IsAny<BroadcastDataMessage>()))
                .Callback<ServiceMessage>(message =>
                {
                    var m = message as BroadcastDataMessage;
                    Assert.Equal(2, m.Payloads.Count);
                    foreach (var hubProtocol in expectedHubProtocols)
                    {
                        var expectedMessageBytes = hubProtocol.GetMessageBytes(new InvocationMessage("target", new object[] { "argument" }));
                        Assert.True(expectedMessageBytes.Span.SequenceEqual(m.Payloads[hubProtocol.Name].Span));
                    }
                });
            var hubContext = await new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                })
                .AddHubProtocol(new MessagePackHubProtocol())
                .ConfigureServices(services => services.AddSingleton(mockConnectionContainer.Object))
                .BuildServiceManager()
                .CreateHubContextAsync("hub", default);
            await hubContext.Clients.All.SendAsync("target", "argument");
        }

        [Fact]
        public async Task AddMessagePack_ThenWithNewtonsoftTest()
        {
            var hubContext = await new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                })
                .AddHubProtocol(new MessagePackHubProtocol())
                .WithNewtonsoftJson()
                .BuildServiceManager()
                .CreateHubContextAsync("hub", default);
            var allProtocols = (hubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IHubProtocolResolver>().AllProtocols;
            Assert.Equal(2, allProtocols.Count);
            Assert.Contains(allProtocols, p => p is MessagePackHubProtocol);
            Assert.IsType<NewtonsoftJsonObjectSerializer>((allProtocols.First(p => p is JsonObjectSerializerHubProtocol) as JsonObjectSerializerHubProtocol).ObjectSerializer);
        }
    }
}