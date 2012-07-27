﻿using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using NSubstitute;
using NUnit.Framework;
using OAuth2.Client;
using OAuth2.Infrastructure;
using OAuth2.Models;
using OAuth2.Parameters;
using RestSharp;
using FluentAssertions;

namespace OAuth2.Tests.Client
{
    [TestFixture]
    public class ClientTests
    {
        [Test]
        public void Should_ReturnCorrectAccessCodeRequestUri()
        {
            // arrange
            var client = Substitute.For<IRestClient>();
            var request = Substitute.For<IRestRequest>();
            var config = Substitute.For<IConfiguration>();
            config.GetSection(Arg.Any<Type>(), Arg.Any<bool>()).Returns(config);
            config.Get<AccessCodeRequestParameters>().Returns(new AccessCodeRequestParameters
            {
                ClientId = "id",
                RedirectUri = "uri",
                Scope = "scope",
                State = "state"
            });
            var descendant = new ClientDescendant(client, request, config);

            // act
            var uri = descendant.GetAccessCodeRequestUri();

            // assert
            uri.Should().Be("https://base.com/resource?response_type=code&client_id=id&redirect_uri=uri&scope=scope&state=state");
        }

        [Test] // TODO: split into several tests
        public void Should_IssueCorrectRequestForAccessToken()
        {
            // arrange
            var request = Substitute.For<IRestRequest>();

            var response = Substitute.For<IRestResponse>();
            response.Content.Returns("access_token=token");

            var client = Substitute.For<IRestClient>();
            client.Execute(Arg.Any<IRestRequest>()).Returns(response);

            var config = Substitute.For<IConfiguration>();
            config.GetSection(Arg.Any<Type>(), Arg.Any<bool>()).Returns(config);
            config.Get<AccessTokenRequestParameters>().Returns(new AccessTokenRequestParameters()
            {
                ClientId = "id",
                RedirectUri = "uri",
                ClientSecret = "secret",
                Code = null
            });

            var descendant = new ClientDescendant(client, request, config);

            // act
            var token = descendant.GetAccessToken("code", string.Empty);

            // assert
            client.BaseUrl.Should().Be("https://base.com");
            request.Resource.Should().Be("/resource");
            request.Method.Should().Be(Method.POST);

            request.AddParameter(Arg.Is("client_id"), Arg.Is("id")).Received(1);
            request.AddParameter(Arg.Is("redirect_uri"), Arg.Is("uri")).Received(1);
            request.AddParameter(Arg.Is("client_secret"), Arg.Is("secret")).Received(1);
            request.AddParameter(Arg.Is("code"), Arg.Is("code")).Received(1);
            request.AddParameter(Arg.Is("grant_type"), Arg.Is("authorization_code")).Received(1);

            client.Execute(Arg.Is(request)).Received(1);

            token.Should().Be("token");
        }

        [Test]
        public void Should_ThrowException_WhenAccessTokenIsRequestedAndErrorIsNotEmpty()
        {
            // arrange
            var request = Substitute.For<IRestRequest>();

            var response = Substitute.For<IRestResponse>();
            response.Content.Returns("access_token=token");

            var client = Substitute.For<IRestClient>();
            client.Execute(Arg.Any<IRestRequest>()).Returns(response);

            var config = Substitute.For<IConfiguration>();
            config.GetSection(Arg.Any<Type>(), Arg.Any<bool>()).Returns(config);
            config.Get<AccessTokenRequestParameters>().Returns(new AccessTokenRequestParameters()
            {
                ClientId = "id",
                RedirectUri = "uri",
                ClientSecret = "secret",
                Code = null
            });

            var descendant = new ClientDescendant(client, request, config);

            // act & assert
            descendant.Invoking(x => x.GetAccessToken("code", "error")).ShouldThrow<ApplicationException>()
                .WithMessage("error");
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        public void ShouldNot_ThrowException_WhenAccessTokenIsRequestedAndErrorIsEmpty(string error)
        {
            // arrange
            var request = Substitute.For<IRestRequest>();

            var response = Substitute.For<IRestResponse>();
            response.Content.Returns("access_token=token");

            var client = Substitute.For<IRestClient>();
            client.Execute(Arg.Any<IRestRequest>()).Returns(response);

            var config = Substitute.For<IConfiguration>();
            config.GetSection(Arg.Any<Type>(), Arg.Any<bool>()).Returns(config);
            config.Get<AccessTokenRequestParameters>().Returns(new AccessTokenRequestParameters()
            {
                ClientId = "id",
                RedirectUri = "uri",
                ClientSecret = "secret",
                Code = null
            });

            var descendant = new ClientDescendant(client, request, config);

            // act & assert
            descendant.Invoking(x => x.GetAccessToken("code", error)).ShouldNotThrow();
        }

        [Test]
        public void Should_IssueCorrectRequestForUserInfo()
        {
            // arrange
            var request = Substitute.For<IRestRequest>();
            request.Parameters.Returns(new List<Parameter>
            {
                new Parameter {Name = "param1", Type = ParameterType.GetOrPost, Value = "value1"}
            });

            var response = Substitute.For<IRestResponse>();
            response.Content.Returns("response");

            var client = Substitute.For<IRestClient>();
            client.Execute(Arg.Any<IRestRequest>()).Returns(response);

            var config = Substitute.For<IConfiguration>();
            config.GetSection(Arg.Any<Type>(), Arg.Any<bool>()).Returns(config);

            var descendant = new ClientDescendant(client, request, config);

            // act
            var info = descendant.GetUserInfo("token");

            // assert
            client.BaseUrl.Should().Be("https://base.com");
            request.Resource.Should().Be("/resource");
            request.Method.Should().Be(Method.GET);

            request.AddParameter(Arg.Is("access_token"), Arg.Is("token")).Received(1);

            client.Execute(Arg.Is(request)).Received(1);

            info.Id.Should().Be("response");
            info.Email.Should().Be("Email1");
        }

        [Test]
        public void Should_OverwritePreviousAccessToken()
        {
            // arrange
            var request = Substitute.For<IRestRequest>();
            request.Parameters.Returns(new List<Parameter>
            {
                new Parameter {Name = "access_token", Type = ParameterType.GetOrPost, Value = "wrong"}
            });

            var response = Substitute.For<IRestResponse>();
            response.Content.Returns("response");

            var client = Substitute.For<IRestClient>();
            client.Execute(Arg.Any<IRestRequest>()).Returns(response);

            var config = Substitute.For<IConfiguration>();
            config.GetSection(Arg.Any<Type>(), Arg.Any<bool>()).Returns(config);

            var descendant = new ClientDescendant(client, request, config);

            // act
            descendant.GetUserInfo("token");

            // assert
            request.Parameters.Should().Contain(x => x.Name == "access_token" && (string) x.Value == "token");
        }

        class ClientDescendant : OAuth2.Client.Client
        {
            private readonly Endpoint endpoint = new Endpoint
            {
                BaseUri = "https://base.com",
                Resource = "/resource"
            };

            public ClientDescendant(IRestClient client, IRestRequest request, IConfiguration configuration) : base(client, request, configuration)
            {
            }

            protected override Endpoint AccessCodeServiceEndpoint
            {
                get { return endpoint; }
            }

            protected override Endpoint AccessTokenServiceEndpoint
            {
                get { return endpoint; }
            }

            protected override Endpoint UserInfoServiceEndpoint
            {
                get { return endpoint; }
            }

            protected override UserInfo ParseUserInfo(string content)
            {
                return Builder<UserInfo>.CreateNew()
                    .With(x => x.Id = content)
                    .Build();
            }
        }
    }
}