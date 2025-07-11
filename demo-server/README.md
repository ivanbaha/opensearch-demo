# OpenSearch Demo Application

A .NET 9 minimal API application demonstrating OpenSearch and MongoDB integration with a clean, maintainable architecture using controllers and services.

## Architecture

The application follows a clean architecture pattern with:

- **Controllers**: Handle HTTP requests and responses
- **Services**: Business logic and data access
- **Models**: Data structures and DTOs
- **Configuration**: Application settings and dependency injection

### Project Structure

```
demo-server/
├── Controllers/
│   ├── HealthController.cs       # Health checks
│   ├── MongoDbController.cs      # MongoDB operations
│   ├── OpenSearchController.cs   # OpenSearch operations
│   └── PapersController.cs       # Papers sync and search
├── Services/
│   ├── IHealthService.cs         # Health service interface
│   ├── HealthService.cs          # Health service implementation
│   ├── IMongoDbService.cs        # MongoDB service interface
│   ├── MongoDbService.cs         # MongoDB service implementation
│   ├── IOpenSearchService.cs     # OpenSearch service interface
│   ├── OpenSearchService.cs      # OpenSearch service implementation
│   ├── IPapersService.cs         # Papers service interface
│   └── PapersService.cs          # Papers service implementation
├── Program.cs                    # Application entry point
├── appsettings.json             # Development configuration
└── appsettings.Production.json  # Production configuration
```

## Features

- **OpenSearch Integration**: Full-text search capabilities
- **MongoDB Integration**: Document database operations
- **Health Monitoring**: Service availability checks
- **Data Synchronization**: MongoDB to OpenSearch sync
- **Advanced Search**: Multi-field search with filters and sorting
- **Swagger Documentation**: API documentation and testing
- **Logging**: Comprehensive logging throughout the application
- **Error Handling**: Robust error handling and responses

## API Endpoints

### Health Check

- **GET** `/api/health` - Check the health status of OpenSearch and MongoDB services

### OpenSearch Demo

- **POST** `/api/opensearch/demo` - Demonstrate OpenSearch indexing and searching

### OpenSearch Index Management

- **GET** `/api/opensearch/index/{indexName}` - Get comprehensive index information including:
  - Index mappings and field definitions
  - Index settings (shards, replicas, refresh interval, etc.)
  - Index statistics (document count, size, performance metrics)
  - Detailed storage and search statistics
- **DELETE** `/api/opensearch/index/{indexName}` - Delete an OpenSearch index by name
  - Includes validation to prevent deletion of system indices (`.opensearch`, `.security`, `.kibana`, etc.)
  - Returns success/failure status with detailed error messages
- **GET** `/api/opensearch/duplicates/{indexName?}` - Check for duplicate documents in an index
  - Default index: `papers`
  - Uses aggregation to detect documents with the same ID
  - Returns detailed analysis of duplicates found and total document count
  - Essential for data quality verification

### MongoDB Operations

- **POST** `/api/mongodb/check` - List available MongoDB collections

### Papers Management

- **POST** `/api/papers/sync` - Sync papers data from MongoDB to OpenSearch with optimized bulk processing
  - **Performance Optimized**: Uses bulk MongoDB queries instead of one-by-one document retrieval
  - **Data Integrity**: Maintains unique DOI-based document IDs with automatic upsert handling
  - **Batch Processing**: Processes documents in configurable batches for optimal memory usage
- **GET** `/api/papers/search` - Search papers with advanced filtering and sorting
- **GET** `/api/papers/list` - Get paginated list of papers with sorting options
- **GET** `/api/papers/topics/{topicName}` - Get paginated list of papers filtered by a specific topic with topic-specific sorting

#### Sync Parameters (`/api/papers/sync`)

- `size`: Number of papers to process (default: 1000, minimum: 1, maximum: 10000)

#### Sync Performance Optimization

The papers sync process has been optimized for maximum performance:

- **Bulk Database Queries**: Retrieves all crossref documents in a single MongoDB query instead of individual lookups
- **Reduced Database Round Trips**: Eliminates N+1 query problem for large datasets
- **Memory Efficient**: Processes documents in configurable batches
- **Performance Metrics**: Detailed timing breakdown in response:

  ```json
  {
    "timing": {
      "mongoRetrievalTimeMs": 3813.159,
      "crossrefBulkRetrievalTimeMs": 820.491,
      "openSearchIndexingTimeMs": 924.184,
      "totalProcessingTimeMs": 4746.923
    }
  }
  ```

#### Search Parameters (`/api/papers/search`)

- `query`: Full-text search query
- `author`: Filter by author name
- `journal`: Filter by journal name
- `fromDate`: Filter by publication date (from)
- `toDate`: Filter by publication date (to)
- `topics`: Filter by topics (comma-separated)
- `sortBy`: Sort results (`hotscore`, `pagerank`, `date`, or relevance)
- `from`: Pagination offset (default: 0)
- `size`: Number of results per page (default: 10)

#### List Parameters (`/api/papers/list`)

- `page`: Page number (default: 1, minimum: 1)
- `perPage`: Results per page (default: 10, minimum: 1, maximum: 100)
- `sort`: Sort order
  - `latest`: Sort by publication date (newest first) - default
  - `hot`: Sort by publication hot score (highest first)
  - `top`: Sort by page rank (highest first)

#### Topic-Specific List Parameters (`/api/papers/topics/{topicName}`)

- `topicName`: **Required** - The name of the topic to filter by (e.g., "machine learning", "ensemble", "microfinance")
- `page`: Page number (default: 1, minimum: 1)
- `perPage`: Results per page (default: 10, minimum: 1, maximum: 100)
- `sort`: Sort order based on topic-specific scores
  - `hot`: Sort by topic hot score (highest first) - default
  - `top`: Sort by topic top score (highest first)
  - `relevance`: Sort by topic relevance score (highest first)
  - `latest`: Sort by publication date (newest first)

**Response Structure:**

```json
{
  "page": 1,
  "perPage": 10,
  "totalItems": 12345,
  "totalPages": 1235,
  "hasNextPage": true,
  "data": [
    {
      "id": "paper-id",
      "title": "Paper Title",
      "abstract": "Paper abstract...",
      "journal": "Journal Name",
      "publisher": "Publisher Name",
      "authors": "Author1, Author2",
      "publicationHotScore": 62.5,
      "pageRank": 0.001,
      "publishedAt": "2025-01-01T00:00:00",
      "topics": [...],
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    }
  ]
}
```

## Configuration

### MongoDB Configuration

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "your_database_name"
  }
}
```

### OpenSearch Configuration

```json
{
  "OpenSearch": {
    "Url": "https://localhost:9200",
    "Username": "admin",
    "Password": "admin",
    "TrustSelfSignedCertificate": "true"
  }
}
```

## Development

### Prerequisites

- .NET 9 SDK
- MongoDB instance
- OpenSearch instance
- Docker (optional, for containerized services)

### Running the Application

1. **Configure Services**: Update `appsettings.json` with your MongoDB and OpenSearch connection details

2. **Start Dependencies**:

   ```bash
   docker-compose up -d
   ```

3. **Run the Application**:

   ```bash
   dotnet run
   ```

4. **Access Swagger UI**: Navigate to `https://localhost:5001/swagger`

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Docker Support

The application includes Docker Compose configuration for easy development:

```bash
# Start all services
docker-compose up -d

# Stop all services
docker-compose down

# View logs
docker-compose logs -f
```

## Data Flow

1. **MongoDB**: Stores raw publication data in `publicationStatistics` and `crossref_raw_data` collections
2. **Data Sync**: The sync endpoint processes MongoDB documents and indexes them in OpenSearch
3. **OpenSearch**: Provides fast full-text search capabilities with the processed data
4. **Search API**: Offers advanced search with filtering, sorting, and pagination

## Example API Calls

### Health Check

```bash
curl -X GET https://localhost:5001/api/health -k
```

### OpenSearch Demo

```bash
curl -X POST https://localhost:5001/api/opensearch/demo -k
```

### OpenSearch Index Management

```bash
# Get index information (metadata, settings, statistics)
curl -X GET "https://localhost:5001/api/opensearch/index/papers" -k

# Get information for a specific index
curl -X GET "https://localhost:5001/api/opensearch/index/demo-index" -k

# Delete an index (with protection against system indices)
curl -X DELETE "https://localhost:5001/api/opensearch/index/demo-index" -k

# Attempting to delete a system index (will be rejected)
curl -X DELETE "https://localhost:5001/api/opensearch/index/.opensearch-test" -k

# Check for duplicate documents in papers index (default)
curl -X GET "https://localhost:5001/api/opensearch/duplicates" -k

# Check for duplicates in a specific index
curl -X GET "https://localhost:5001/api/opensearch/duplicates/papers" -k
```

### MongoDB Check

```bash
curl -X POST https://localhost:5001/api/mongodb/check -k
```

### Sync Papers

```bash
# Default sync (1000 papers)
curl -X POST https://localhost:5001/api/papers/sync -k

# Sync specific number of papers
curl -X POST "https://localhost:5001/api/papers/sync?size=5000" -k
```

### Search Papers

```bash
# Basic search
curl -X GET "https://localhost:5001/api/papers/search?query=machine%20learning" -k

# Advanced search with filters
curl -X GET "https://localhost:5001/api/papers/search?query=neural%20networks&journal=Nature&sortBy=hotscore&size=20" -k
```

### List Papers

```bash
# Default list (latest papers, page 1, 10 per page)
curl -X GET "https://localhost:5001/api/papers/list" -k

# Get second page with 5 papers per page, sorted by hot score
curl -X GET "https://localhost:5001/api/papers/list?page=2&perPage=5&sort=hot" -k

# Get top-ranked papers
curl -X GET "https://localhost:5001/api/papers/list?sort=top&perPage=20" -k
```

### List Papers by Topic

```bash
# Get papers for "ensemble" topic sorted by hot score (default)
curl -X GET "https://localhost:5001/api/papers/topics/ensemble" -k

# Get papers for "machine learning" topic with pagination and relevance sorting
curl -X GET "https://localhost:5001/api/papers/topics/machine%20learning?page=2&perPage=5&sort=relevance" -k

# Get latest papers for "microfinance" topic
curl -X GET "https://localhost:5001/api/papers/topics/microfinance?sort=latest&perPage=15" -k

# Get top-scored papers for a topic with URL encoding for spaces
curl -X GET "https://localhost:5001/api/papers/topics/neural%20networks?sort=top" -k
```

## Error Handling

The application implements comprehensive error handling:

- Service-level exceptions are caught and logged
- HTTP responses include appropriate status codes
- Detailed error messages for debugging (development mode)
- Graceful degradation for service unavailability

## Logging

The application uses structured logging with different levels:

- **Information**: General application flow
- **Warning**: Potential issues or missing data
- **Error**: Exceptions and failures
- **Debug**: Detailed diagnostic information

## Performance Considerations

- **Batch Processing**: Documents are indexed in batches of 1000 for optimal performance
- **Async Operations**: All database operations are asynchronous
- **Connection Pooling**: Efficient connection management for both MongoDB and OpenSearch
- **Pagination**: Search results are paginated to prevent large response payloads

## Security Notes

- **Self-signed Certificates**: The application is configured to trust self-signed certificates for development
- **Authentication**: Basic authentication is used for OpenSearch
- **Configuration**: Sensitive settings should be stored in environment variables or secure configuration providers for production

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes following the existing architecture patterns
4. Add tests for new functionality
5. Submit a pull request

## License

This project is licensed under the MIT License.
