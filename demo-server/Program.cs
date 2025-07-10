using OpenSearch.Net;
using MongoDB.Driver;
using OpenSearchDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure OpenSearch client
builder.Services.AddSingleton<IOpenSearchLowLevelClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
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
        config.ServerCertificateValidationCallback((o, certificate, chain, errors) => true);
    }

    return new OpenSearchLowLevelClient(config);
});

// Configure MongoDB client
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var mongoConfig = configuration.GetSection("MongoDB");

    var connectionString = mongoConfig["ConnectionString"] ?? throw new InvalidOperationException("MongoDB connection string is not configured");

    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var mongoConfig = configuration.GetSection("MongoDB");
    var client = serviceProvider.GetRequiredService<IMongoClient>();

    var databaseName = mongoConfig["DatabaseName"] ?? throw new InvalidOperationException("MongoDB database name is not configured");

    return client.GetDatabase(databaseName);
});

// Register services
builder.Services.AddScoped<IOpenSearchService, OpenSearchService>();
builder.Services.AddScoped<IMongoDbService, MongoDbService>();
builder.Services.AddScoped<IPapersService, PapersService>();
builder.Services.AddScoped<IHealthService, HealthService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
