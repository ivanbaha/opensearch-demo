using System;
using System.IO;
using OpenSearch.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

class Program
{
    static void Main(string[] args)
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Get OpenSearch configuration
        var openSearchConfig = configuration.GetSection("OpenSearch");
        var url = openSearchConfig["Url"] ?? throw new InvalidOperationException("OpenSearch URL is not configured");
        var username = openSearchConfig["Username"] ?? throw new InvalidOperationException("OpenSearch Username is not configured");
        var password = openSearchConfig["Password"] ?? throw new InvalidOperationException("OpenSearch Password is not configured");
        var trustSelfSigned = bool.Parse(openSearchConfig["TrustSelfSignedCertificate"] ?? "false");

        var node = new Uri(url);
        var config = new ConnectionConfiguration(node)
            .BasicAuthentication(username, password);

        if (trustSelfSigned)
        {
            config.ServerCertificateValidationCallback((o, certificate, chain, errors) => true); // Trust self-signed cert
        }

        var client = new OpenSearchLowLevelClient(config);

        var indexName = "demo-index";
        var document = new { title = "Hello OpenSearch", content = "This is a test document." };

        // Index a document
        var indexResponse = client.Index<StringResponse>(indexName, PostData.Serializable(document));
        Console.WriteLine("Index Response: " + indexResponse.Body);

        // Search for the document
        var searchJson = @"{
            ""query"": {
                ""match"": {
                    ""content"": ""test""
                }
            }
        }";

        var searchResponse = client.Search<StringResponse>(indexName, searchJson);
        Console.WriteLine("Search Response: " + searchResponse.Body);
    }
}
