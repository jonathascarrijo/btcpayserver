﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Payments.Lightning.Lnd
{
    public partial class LndSwaggerClient
    {
        public LndSwaggerClient(LndRestSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            _LndSettings = settings;
            _Authentication = settings.CreateLndAuthentication();
            BaseUrl = settings.Uri.AbsoluteUri.TrimEnd('/');
            _httpClient = CreateHttpClient(settings);
            _settings = new System.Lazy<Newtonsoft.Json.JsonSerializerSettings>(() =>
            {
                var json = new Newtonsoft.Json.JsonSerializerSettings();
                UpdateJsonSerializerSettings(json);
                return json;
            });
        }
        LndRestSettings _LndSettings;
        LndAuthentication _Authentication;

        partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
        {
            _Authentication.AddAuthentication(request);
        }

        internal static HttpClient CreateHttpClient(LndRestSettings settings)
        {
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12
            };

            var expectedThumbprint = settings.CertificateThumbprint?.ToArray();
            if (expectedThumbprint != null)
            {
                handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                {
                    var actualCert = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
                    var hash = actualCert.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                    return hash.SequenceEqual(expectedThumbprint);
                };
            }

            if (settings.AllowInsecure)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            else
            {
                if (settings.Uri.Scheme == "http")
                    throw new InvalidOperationException("AllowInsecure is set to false, but the URI is not using https");
            }
            return new HttpClient(handler);
        }

        internal HttpClient CreateHttpClient()
        {
            return LndSwaggerClient.CreateHttpClient(_LndSettings);
        }

        internal T Deserialize<T>(string str)
        {
            return JsonConvert.DeserializeObject<T>(str, _settings.Value);
        }
    }
}
