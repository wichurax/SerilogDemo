# Log Search Tools

Compare log searching performance between **file-based** and **Loki** approaches.

## Usage

```bash
cd LogSearchTools
dotnet run [command] [search-term] [iterations]
```

### Commands

| Command     | Description                                 |
| ----------- | ------------------------------------------- |
| `test`      | Quick test of both search methods (default) |
| `benchmark` | Run performance benchmark with statistics   |
| `help`      | Show help                                   |

### Examples

```bash
# Quick test with default search term
dotnet run

# Test with custom search term
dotnet run test demo-user-123

# Benchmark with 20 iterations
dotnet run benchmark demo-user-123 20
```

## Tools

### FileLogSearcher
High-performance file search using:
- Memory-mapped files for large files (>10MB)
- Parallel processing across files
- ArrayPool to minimize GC pressure

### LokiLogSearcher
Queries Loki HTTP API with:
- LogQL json parser for precise property matching
- Simple string contains for faster searches
- Configurable lookback period

## Requirements

- .NET 8.0
- Log files in `../Logs/` folder (for file search)
- Loki running at `http://localhost:3100` (for Loki search)
