FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY XsltCraft.slnx ./
COPY XsltCraft/XsltCraft.csproj XsltCraft/
COPY XsltCraft.Application/XsltCraft.Application.csproj XsltCraft.Application/
COPY XsltCraft.Domain/XsltCraft.Domain.csproj XsltCraft.Domain/
COPY XsltCraft.Infrastructure/XsltCraft.Infrastructure.csproj XsltCraft.Infrastructure/
COPY XsltCraft.Tests/XsltCraft.Tests.csproj XsltCraft.Tests/

RUN dotnet restore XsltCraft.slnx

COPY . .

RUN dotnet publish XsltCraft/XsltCraft.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "XsltCraft.dll"]
