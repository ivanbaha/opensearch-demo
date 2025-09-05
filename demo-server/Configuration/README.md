# OpenSearch Index Configuration

This folder contains centralized configuration for OpenSearch indices, making it easy to maintain and reuse across different projects.

## Overview

The `OpenSearchIndexConfiguration.cs` file provides a centralized location for all OpenSearch index-related configurations including:

- Field mappings
- Index settings
- Aliases configuration
- Vector search parameters
- Default values and constants

## Usage

### Basic Usage

```csharp
using OpenSearchDemo.Configuration;

// Get the complete index configuration
var indexConfig = OpenSearchIndexConfiguration.GetFullIndexConfiguration();

// Create index with default configuration
var createIndexResponse = await client.Indices.CreateAsync<StringResponse>(
    "my_index",
    PostData.String(indexConfig)
);
```

### Customization Options

#### Custom Alias Name

```csharp
// Use a custom alias name instead of the default "papers"
var indexConfig = OpenSearchIndexConfiguration.GetFullIndexConfiguration("my_papers");
```

#### Custom Embedding Dimension

```csharp
// Use a different embedding model with different dimensions
var indexConfig = OpenSearchIndexConfiguration.GetIndexConfigurationWithCustomEmbeddingDimension(512, "my_papers");
```

#### Custom Settings

```csharp
// Override specific index settings
var customSettings = new {
    index = new {
        number_of_shards = 10,
        number_of_replicas = 2,
        refresh_interval = "1s"
    }
};

var indexConfig = OpenSearchIndexConfiguration.GetFullIndexConfiguration("papers", customSettings);
```

#### Individual Components

```csharp
// Get just the mappings
var mappings = OpenSearchIndexConfiguration.GetMappingsConfiguration();

// Get just the settings
var settings = OpenSearchIndexConfiguration.GetIndexSettings();

// Get just the field mapping properties
var properties = OpenSearchIndexConfiguration.GetMappingProperties();
```

## Configuration Constants

The configuration provides several useful constants:

```csharp
// Default values
OpenSearchIndexConfiguration.DefaultIndexName     // "papers_test_v1"
OpenSearchIndexConfiguration.DefaultAliasName     // "papers"
OpenSearchIndexConfiguration.EmbeddingDimension   // 768

// Index settings
OpenSearchIndexConfiguration.IndexSettings.MaxResultWindow          // 50000
OpenSearchIndexConfiguration.IndexSettings.NumberOfShards           // 30
OpenSearchIndexConfiguration.IndexSettings.NumberOfReplicas         // 1
OpenSearchIndexConfiguration.IndexSettings.RefreshInterval          // "-1"
OpenSearchIndexConfiguration.IndexSettings.KnnEnabled               // true
OpenSearchIndexConfiguration.IndexSettings.KnnAlgoParamEfSearch     // 128

// Vector search settings
OpenSearchIndexConfiguration.VectorSearchSettings.EfConstruction    // 128
OpenSearchIndexConfiguration.VectorSearchSettings.MParameter        // 24
OpenSearchIndexConfiguration.VectorSearchSettings.SpaceType         // "cosinesimil"
OpenSearchIndexConfiguration.VectorSearchSettings.Engine           // "lucene"
OpenSearchIndexConfiguration.VectorSearchSettings.Method           // "hnsw"
```

## Field Mappings

The configuration includes comprehensive field mappings for academic papers:

### Core Fields

- `id`, `oipubId`, `doi` - Document identifiers
- `title`, `abstract`, `openSummary` - Text content with analyzer configurations
- `journal`, `publisher` - Publication metadata
- `authors` - Nested object with name, ORCID, and sequence

### Metrics and Scores

- `publicationHotScore`, `publicationHotScore6m` - Trending metrics
- `pageRank` - Authority score
- `citationsCount`, `voteScore` - Engagement metrics

### Topics

- `topics` - Nested objects with relevance, top, and hot scores

### System Fields

- `hasAbstract`, `hasOpenSummary` - Boolean flags
- `publishedAt` - Publication date
- `embeddingVector` - KNN vector for semantic search (768 dimensions)
- `contextualContent` - Multi-analyzer text for contextual search

## Migration from Hardcoded Configuration

If you're migrating from hardcoded configuration in your service classes:

1. **Add the using statement:**

   ```csharp
   using OpenSearchDemo.Configuration;
   ```

2. **Replace hardcoded index names:**

   ```csharp
   // Before
   var indexName = "papers";

   // After
   var indexName = OpenSearchIndexConfiguration.DefaultAliasName;
   ```

3. **Replace hardcoded index creation:**

   ```csharp
   // Before
   var mapping = GetHardcodedMapping();

   // After
   var mapping = OpenSearchIndexConfiguration.GetFullIndexConfiguration();
   ```

4. **Replace hardcoded constants:**

   ```csharp
   // Before
   const int EXPECTED_DIMENSION = 768;

   // After
   const int EXPECTED_DIMENSION = OpenSearchIndexConfiguration.EmbeddingDimension;
   ```

## Cross-Project Reuse

To reuse this configuration in other projects:

1. Copy the `Configuration` folder to your new project
2. Update the namespace to match your project
3. Customize the field mappings as needed for your domain
4. Update the constants to match your requirements

## Best Practices

1. **Centralization**: Keep all index-related configuration in this single location
2. **Documentation**: Document any custom fields or settings you add
3. **Testing**: Test configuration changes in development before production
4. **Versioning**: Consider versioning your index configurations for schema evolution
5. **Environment-specific**: Use different configurations for development, staging, and production environments

## Schema Evolution

When evolving the schema:

1. Create new methods for new versions (e.g., `GetFullIndexConfigurationV2()`)
2. Maintain backward compatibility for existing indices
3. Plan migration strategies for production data
4. Document breaking changes and migration steps
