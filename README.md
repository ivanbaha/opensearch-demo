# OpenSearch Demo

Demo project for deploying and interacting with OpenSearch via ASP.NET server

## Architecture

This demo features a **multi-node OpenSearch cluster** with:

- **2 OpenSearch nodes** for high availability and load distribution
- **OpenSearch Dashboards** for data visualization and cluster management
- **.NET 9 API server** with advanced search capabilities
- **MongoDB integration** for data synchronization

## Multi-Node OpenSearch Cluster

### Cluster Configuration

- **opensearch-node1**: Primary node (port 9200, 9600)
- **opensearch-node2**: Secondary node (port 9201, 9601)
- **Cluster name**: `opensearch-cluster`
- **High availability**: Each shard has replicas on both nodes
- **Load balancing**: Client requests are automatically distributed

### Cluster Features

- **Automatic failover**: If one node fails, the other continues serving
- **Data replication**: All data is replicated across both nodes
- **Load distribution**: Search queries are distributed across nodes
- **Horizontal scaling**: Easy to add more nodes to the cluster

## Services

- **OpenSearch Cluster**: `localhost:9200` (node1), `localhost:9201` (node2)
- **OpenSearch Dashboards**: `localhost:5601`
- **API Server**: `localhost:5000`
- **MongoDB**: External connection to reference database

## Quick Start

1. **Start the cluster**:

   ```bash
   docker compose up -d
   ```

2. **Verify cluster health**:

   ```bash
   curl -k -u admin:password "https://localhost:9200/_cluster/health"
   ```

3. **Start the API server**:

   ```bash
   cd demo-server
   dotnet run
   ```

4. **Test the API**:
   ```bash
   curl "http://localhost:5000/api/health"
   ```

## Advanced Features

- **Topic-based search**: Filter papers by specific topics with topic-specific scoring
- **Advanced pagination**: Support for large datasets with optimized pagination
- **Multi-field search**: Search across titles, abstracts, authors, and journals
- **Bulk data sync**: Efficient MongoDB to OpenSearch synchronization
- **Index management**: Create, delete, and monitor OpenSearch indices
- **Duplicate detection**: Identify and manage duplicate documents

For detailed API documentation, see [demo-server/README.md](demo-server/README.md).
