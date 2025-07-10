# OpenSearch Demo API

This project demonstrates how to interact with OpenSearch using a .NET web API.

## Prerequisites

- .NET 9.0 or later
- Docker and Docker Compose (for running OpenSearch)

## Setup

1. Start OpenSearch using Docker Compose:

   ```bash
   docker-compose up -d
   ```

2. Build and run the API server:

   ```bash
   cd demo-server
   dotnet run
   ```

3. The server will start on `https://localhost:5001` (or the port shown in the console output)

## API Endpoints

### POST /api/demo

Demonstrates basic OpenSearch operations:

- Creates a sample document in the "demo-index" index
- Searches for documents containing "test"

**Example request:**

```bash
curl -X POST https://localhost:5001/api/demo \
     -H "Content-Type: application/json" \
     -k
```

**Example response:**

```json
{
  "indexResponse": {
    "_index": "demo-index",
    "_id": "...",
    "_version": 1,
    "result": "created",
    "_shards": {
      "total": 2,
      "successful": 1,
      "failed": 0
    }
  },
  "searchResponse": {
    "took": 5,
    "timed_out": false,
    "_shards": {
      "total": 1,
      "successful": 1,
      "skipped": 0,
      "failed": 0
    },
    "hits": {
      "total": {
        "value": 1,
        "relation": "eq"
      },
      "max_score": 0.2876821,
      "hits": [
        {
          "_index": "demo-index",
          "_id": "...",
          "_score": 0.2876821,
          "_source": {
            "title": "Hello OpenSearch",
            "content": "This is a test document."
          }
        }
      ]
    }
  }
}
```

## Swagger Documentation

When running in development mode, you can access the Swagger UI at:
`https://localhost:5001/swagger`

## Configuration

The application uses `appsettings.json` for configuration. Key settings:

- `OpenSearch.Url`: OpenSearch server URL
- `OpenSearch.Username`: Authentication username
- `OpenSearch.Password`: Authentication password
- `OpenSearch.TrustSelfSignedCertificate`: Whether to trust self-signed certificates

## OpenSearch Dashboard

Access the OpenSearch Dashboard at: `http://localhost:5601`
