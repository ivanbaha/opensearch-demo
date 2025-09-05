# Papers Index AI Context File

## Overview

This document provides comprehensive context for the `papers` OpenSearch index used in the OpenSearch Demo project. Use this context to generate APIs, queries, and integrations for academic paper search and retrieval.

## Index Configuration

### Index Name

- **Primary Index**: `papers_v3`
- **Alias**: `papers` (write index)

### Index Settings

```json
{
  "max_result_window": 50000,
  "number_of_shards": 30,
  "number_of_replicas": 1,
  "refresh_interval": "-1",
  "knn": true,
  "knn.algo_param.ef_search": 128
}
```

## Field Schema and Data Types

### Core Paper Fields

#### Identifiers

- **`id`** (keyword) - Primary document identifier
- **`oipubId`** (keyword) - OpenAlex publication ID
- **`doi`** (keyword) - Digital Object Identifier

#### Content Fields

- **`title`** (text + keyword)

  - Type: `text` with `standard` analyzer
  - Multi-field: `title.keyword` for exact matching
  - Boost factor: `^3` in multi-match queries

- **`abstract`** (text)

  - Type: `text` with `standard` analyzer
  - Boost factor: `^2` in multi-match queries

- **`openSummary`** (text)
  - Type: `text` with `standard` analyzer
  - AI-generated summary content

#### Publication Metadata

- **`journal`** (text + keyword)

  - Type: `text` with multi-field support
  - Multi-field: `journal.keyword` for filtering

- **`publisher`** (text + keyword)

  - Type: `text` with multi-field support
  - Multi-field: `publisher.keyword` for filtering

- **`publishedAt`** (date)

  - Type: `date`
  - Null value: `"0001-01-01T00:00:00Z"`
  - Format: ISO 8601

- **`publicationDateParts`** (integer)
  - Publication date components array

#### Authors (Nested Object)

```json
{
  "type": "nested",
  "properties": {
    "name": { "type": "keyword" },
    "ORCID": { "type": "keyword" },
    "sequence": { "type": "keyword" }
  }
}
```

#### Scoring and Ranking Fields

- **`publicationHotScore`** (double, null_value: 0.0)
- **`publicationHotScore6m`** (double, null_value: 0.0)
- **`pageRank`** (double, null_value: 0.0)
- **`citationsCount`** (integer, null_value: 0.0)
- **`voteScore`** (integer, null_value: 0.0)

#### Topics (Nested Object)

```json
{
  "type": "nested",
  "properties": {
    "name": { "type": "keyword" },
    "relevanceScore": { "type": "double", "null_value": 0.0 },
    "topScore": { "type": "double", "null_value": 0.0 },
    "hotScore": { "type": "double", "null_value": 0.0 },
    "hotScore6m": { "type": "double", "null_value": 0.0 }
  }
}
```

### System and Search Fields

#### Content Flags

- **`hasAbstract`** (boolean) - Whether paper has abstract content
- **`hasOpenSummary`** (boolean) - Whether paper has AI-generated summary

#### Full-Text Search

- **`fullTextContent`** (text)
  - Type: `text` with `standard` analyzer
  - Store: `false` (for indexing only)
  - Used for keyword-based fallbacks and hybrid search

#### Semantic Search

- **`embeddingVector`** (knn_vector)
  - Dimension: 768 (for 'M2-BERT-Retrieval-32k' model)
  - Method: HNSW with cosine similarity
  - Engine: Lucene
  - Parameters:
    - `ef_construction`: 128
    - `m`: 24

#### Contextual Search

- **`contextualContent`** (text with multi-field)
  - Type: `text` with `standard` analyzer
  - Multi-fields:
    - `contextualContent.english` (english analyzer)
    - `contextualContent.keyword` (keyword type)

## Common Query Patterns

### 1. Basic Text Search

```json
{
  "query": {
    "multi_match": {
      "query": "machine learning",
      "fields": ["title^3", "abstract^2", "authors", "journal"],
      "type": "best_fields",
      "fuzziness": "AUTO"
    }
  }
}
```

### 2. Author Search

```json
{
  "query": {
    "nested": {
      "path": "authors",
      "query": {
        "match": {
          "authors.name": "John Smith"
        }
      }
    }
  }
}
```

### 3. Topic-Based Search

```json
{
  "query": {
    "nested": {
      "path": "topics",
      "query": {
        "term": {
          "topics.name": "artificial-intelligence"
        }
      }
    }
  }
}
```

### 4. Date Range Filtering

```json
{
  "query": {
    "bool": {
      "filter": [
        {
          "range": {
            "publishedAt": {
              "gte": "2020-01-01",
              "lte": "2024-12-31"
            }
          }
        }
      ]
    }
  }
}
```

### 5. Content Availability Filtering

```json
{
  "query": {
    "bool": {
      "filter": [
        {
          "term": {
            "hasAbstract": true
          }
        }
      ]
    }
  }
}
```

## Sorting Options

### Available Sort Fields

- **`_score`** - Relevance score (default for searches)
- **`publicationHotScore`** - Current hot/trending score
- **`pageRank`** - Authority/importance ranking
- **`publishedAt`** - Publication date
- **`citationsCount`** - Citation count
- **`voteScore`** - Community vote score

### Sort Aliases

- `"hot"` → `publicationHotScore` desc
- `"top"` → `pageRank` desc
- `"latest"` → `publishedAt` desc
- `"relevance"` → `_score` desc

### Topic-Specific Sorting

For topic-based queries, use script-based sorting:

```json
{
  "sort": [
    {
      "_script": {
        "type": "number",
        "script": {
          "source": "if (params._source.topics != null) { for (topic in params._source.topics) { if (topic.name == params.topicName) { return topic.hotScore != null ? topic.hotScore : 0; } } } return 0;",
          "params": { "topicName": "artificial-intelligence" }
        },
        "order": "desc"
      }
    }
  ]
}
```

## API Design Recommendations

### Search Endpoints

1. **General Search**: `/api/papers/search`
2. **Topic Search**: `/api/papers/topics/{topicName}`
3. **Author Search**: `/api/papers/authors/{authorName}`
4. **Semantic Search**: `/api/papers/semantic-search`
5. **Browse/List**: `/api/papers`

### Common Query Parameters

- `q` or `query`: Search query string
- `author`: Author name filter
- `journal`: Journal name filter
- `from_date`, `to_date`: Date range filters
- `topics`: Comma-separated topic names
- `has_abstract`: Boolean filter for abstract availability
- `sort`: Sort method (hot, top, latest, relevance)
- `page`, `per_page` or `from`, `size`: Pagination
- `fields`: Field selection for response

### Response Format

```json
{
  "total": 1000,
  "page": 1,
  "per_page": 10,
  "sort": "latest",
  "papers": [
    {
      "id": "paper123",
      "title": "Paper Title",
      "abstract": "Abstract content...",
      "authors": [{ "name": "Author Name", "ORCID": "0000-0000-0000-0000" }],
      "journal": "Journal Name",
      "publishedAt": "2024-01-01T00:00:00Z",
      "topics": [{ "name": "topic-name", "relevanceScore": 0.95 }],
      "scores": {
        "hotScore": 0.85,
        "pageRank": 0.75,
        "citationsCount": 42
      }
    }
  ]
}
```

## Performance Considerations

### Excluded Fields in Responses

Always exclude these heavy fields from search results:

- `embeddingVector`
- `contextualContent`
- `fullTextContent`

### Pagination Limits

- Max `size`: 50 (recommended)
- Max `from`: 50000 (index setting)

### Search Performance

- Use `track_total_hits: true` for accurate counts
- Consider using `scroll` API for large result sets
- Use `_source` filtering to reduce response size

## Integration Points

### External Services

- **TogetherAI Service**: For semantic search embeddings
- **MongoDB**: For paper metadata storage
- **Background Sync**: For data updates

### Related APIs

- Health check: `/health`
- Index management: `/admin/index/*`
- Sync operations: `/sync/*`

## Example Use Cases

1. **Academic Research**: Find papers by topic, author, or keyword
2. **Trend Analysis**: Use hot scores to identify trending research
3. **Citation Analysis**: Leverage pageRank and citation counts
4. **Content Discovery**: Semantic search for related papers
5. **Author Profiles**: Aggregate papers by author with ORCID
6. **Journal Analytics**: Analyze publication patterns by journal

## Notes for AI Code Generation

- Always include proper error handling for OpenSearch client calls
- Use structured logging with meaningful context
- Implement proper pagination validation
- Consider caching for frequently accessed data
- Use async/await patterns for all OpenSearch operations
- Validate input parameters before constructing queries
- Handle JSON serialization/deserialization errors gracefully
