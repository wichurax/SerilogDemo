FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["PaymentGateway/PaymentGateway.csproj", "PaymentGateway/"]
COPY ["PaymentGateway.Contracts/PaymentGateway.Contracts.csproj", "PaymentGateway.Contracts/"]
COPY ["SerilogDemo.Hosting/SerilogDemo.Hosting.csproj", "SerilogDemo.Hosting/"]
RUN dotnet restore "PaymentGateway/PaymentGateway.csproj"

COPY . .
RUN dotnet build "PaymentGateway/PaymentGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PaymentGateway/PaymentGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Logging__Console__FormatterName=Json

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PaymentGateway.dll"]