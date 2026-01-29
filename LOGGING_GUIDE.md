# Advanced Logging Features

## 🚀 Enhanced Features

### 1. Request Logging with Smart Sampling
HTTP requests are automatically logged with enriched context including:
- **CorrelationId** (TraceIdentifier) for distributed tracing
- **RemoteIP** for client identification
- **UserAgent** for client application tracking
- **RequestHost** and **RequestScheme**
- **QueryString** (when present)

**Smart Log Levels:**
- Health checks: `Debug` (reduces noise)
- 5xx errors: `Error`
- 4xx errors: `Warning`
- Successful requests: `Information`

### 2. Performance Monitoring
- **Thread ID**: Track concurrent request processing
- **Elapsed Time**: Request duration automatically captured

### 3. Instance Identification
Each container instance gets a unique identifier for filtering via the `InstanceId` property:
- Simple mode: `InstanceId="api-simple"` (set via INSTANCE_ID env var)
- Scaled mode: `InstanceId="<container-hostname>"` (auto-detected)

The `InstanceId` is added to all logs as a Serilog property and mapped to a Loki label via `propertiesAsLabels`.

### 4. EF Core Noise Reduction
SQL query logging reduced to Warning level only:
```json
"Microsoft.EntityFrameworkCore.Database.Command": "Warning",
"Microsoft.EntityFrameworkCore.Infrastructure": "Warning"
```

## 📊 Grafana Query Examples

### Filter by Instance
```logql
{app="SerilogDemo", InstanceId="api-simple"}
```

### Filter by Scaled Instance (using regex)
```logql
{app="SerilogDemo", InstanceId=~"serilogdemo-api-scaled-.*"}
```

### Find Errors by Controller
```logql
{app="SerilogDemo"} 
| json 
| SourceContext =~ ".*Controllers.*" 
| Level = "Error"
```

### Track Request by Correlation ID
```logql
{app="SerilogDemo"} 
| json 
| CorrelationId = "your-correlation-id"
```

### High Response Time Requests
```logql
{app="SerilogDemo API"} 
| json 
| Elapsed > 1000
```

### Errors Grouped by Instance
```logql
sum by (instance) (
  rate({app="SerilogDemo API"} |= "Error" [5m])
)
```

## 🔔 Grafana Alerts (Optional)

Alert rules are provided in `grafana-alerts.yml`:

1. **High Error Rate**: Triggers when error rate > 0.1 per second
2. **Instance Down**: Alerts when no logs received for 3 minutes
3. **High Response Time**: Warns when avg response time > 1000ms

### To Enable Alerts:
```yaml
# Add to docker-compose.yml grafana service
volumes:
  - ./grafana-alerts.yml:/etc/grafana/provisioning/alerting/alerts.yml
```

## 🛠️ Usage

### Build and Run
```bash
# Simple mode
docker compose up -d

# Scaled mode (3 instances)
docker compose --profile scaled up -d --scale api-scaled=3

# With load testing
docker compose --profile scaled --profile loadtest up -d --scale api-scaled=5
```

### View Logs with Correlation
```bash
# In your application code
using (logger.BeginScope(new Dictionary<string, object>
{
    ["OrderId"] = orderId,
    ["UserId"] = userId
}))
{
    logger.LogInformation("Processing order");
}
```

### Access Grafana
1. Navigate to http://localhost:3000
2. Go to Explore
3. Select Loki datasource
4. Use LogQL queries above

## 📈 Performance Tips

1. **Adjust MinimumLevel** in production:
   ```json
   "MinimumLevel": "Warning"
   ```

2. **Add more filters** for high-traffic endpoints:
   ```csharp
   if (httpContext.Request.Path.StartsWithSegments("/api/metrics"))
       return LogEventLevel.Debug;
   ```

3. **Use structured logging**:
   ```csharp
   // Good ✓
   logger.LogInformation("User {UserId} placed order {OrderId}", userId, orderId);
   
   // Bad ✗
   logger.LogInformation($"User {userId} placed order {orderId}");
   ```

## 🔍 Troubleshooting

**No logs in Grafana?**
- Check Loki is running: `docker logs loki`
- Verify connectivity: `curl http://localhost:3100/ready`
- Check app configuration: Environment variable `Serilog__WriteTo__3__Args__uri`

**Too many logs?**
- Increase MinimumLevel to Warning
- Add more path filters in request logging
- Adjust EF Core logging levels

**Can't filter by instance?**
- Verify INSTANCE_ID environment variable is set
- Check entrypoint.sh executed successfully
- Inspect logs: `docker logs <container-name>`
