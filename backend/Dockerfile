FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Yalnız API ve bağımlı projeler restore edilir (test projeleri imaja girmez; slnx'i restore etmek
# kopyalanmayan test csproj'ları yüzünden kırılıyordu). Katman önbelleği: önce csproj, sonra kaynak.
COPY XsltCraft/XsltCraft.Api.csproj XsltCraft/
COPY XsltCraft.Application/XsltCraft.Application.csproj XsltCraft.Application/
COPY XsltCraft.Domain/XsltCraft.Domain.csproj XsltCraft.Domain/
COPY XsltCraft.Infrastructure/XsltCraft.Infrastructure.csproj XsltCraft.Infrastructure/

RUN dotnet restore XsltCraft/XsltCraft.Api.csproj

# bin/obj, test projeleri ve appsettings.Development.json (.dockerignore) kopyalanmaz.
COPY . .

RUN dotnet publish XsltCraft/XsltCraft.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "XsltCraft.Api.dll"]
