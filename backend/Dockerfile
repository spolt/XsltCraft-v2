# syntax=docker/dockerfile:1.7
#
# XsltCraft backend imajı: Api + izole XSLT worker'ı (Saxon-HE 12.10 / IKVM, ADR 007/008).
#
# Önbellek: BuildKit cache mount'ları (Compose v2 / `docker build` varsayılanı BuildKit).
#   - NuGet  → /root/.nuget/packages
#   - Maven  → /root/.m2        (IKVM.Maven.Sdk yerel deposu; Saxon/xmlresolver jar'ları)
#   - IKVM   → /tmp/ikvm/cache  (IKVM.MSBuild IkvmCacheDir varsayılanı: GetTempPath()/ikvm/cache/1/ —
#                                jar → .NET dönüşüm önbelleği, ~2 dk'lık ikvmc işini atlatır)
#   Cache mount içeriği imaj katmanına girmez; BuildKit GC mount'u katmandan önce silebilir. Bu yüzden
#   publish adımı restore'u yeniden çalıştırır (paketler yerindeyse no-op, değilse yeniden indirir).
#
# Tedarik zinciri (ADR 008 §9): restore `--locked-mode` (lock dosyası olan projeler, şu an Xslt.Saxon;
# lock RID'li — RuntimeIdentifiers=linux-x64); Maven jar'ları her derlemede maven-hashes.txt'e karşı
# SHA-256 ile doğrulanır (MavenHashVerification.targets) — cache mount'tan gelen jar da dahil.
#
# RID: restore ve publish aynı RID'le (linux-x64) yapılır; aksi halde publish --no-restore NETSDK1047 verir.
# Worker, Api publish'ine KOPYALANMAZ (XsltWorker.targets yalnız Build'de kopyalar) → ayrı publish edilir
# ve Api publish'i -p:BuildXsltWorker=false ile worker ProjectReference'ı olmadan yapılır.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

# ── Restore katmanı: yalnız proje dosyaları + değerlendirmede içe aktarılan MSBuild dosyaları ve kilitler.
# Test projeleri imaja girmez (slnx restore edilmez).
COPY build/XsltWorker.targets build/
COPY XsltCraft/XsltCraft.Api.csproj XsltCraft/
COPY XsltCraft.Application/XsltCraft.Application.csproj XsltCraft.Application/
COPY XsltCraft.Domain/XsltCraft.Domain.csproj XsltCraft.Domain/
COPY XsltCraft.Infrastructure/XsltCraft.Infrastructure.csproj XsltCraft.Infrastructure/
COPY XsltCraft.XsltWorker.Contracts/XsltCraft.XsltWorker.Contracts.csproj XsltCraft.XsltWorker.Contracts/
COPY XsltCraft.XsltWorker/XsltCraft.XsltWorker.csproj XsltCraft.XsltWorker/
COPY XsltCraft.Xslt.Saxon/XsltCraft.Xslt.Saxon.csproj \
     XsltCraft.Xslt.Saxon/packages.lock.json \
     XsltCraft.Xslt.Saxon/maven-hashes.txt \
     XsltCraft.Xslt.Saxon/MavenHashVerification.targets \
     XsltCraft.Xslt.Saxon/

RUN --mount=type=cache,id=xsltcraft-nuget,target=/root/.nuget/packages \
    dotnet restore XsltCraft.XsltWorker/XsltCraft.XsltWorker.csproj -r linux-x64 --locked-mode \
 && dotnet restore XsltCraft/XsltCraft.Api.csproj -r linux-x64 --locked-mode -p:BuildXsltWorker=false

# bin/obj, test projeleri, spike ve yerel sırlar (.dockerignore) kopyalanmaz.
COPY . .

# Önce worker (→ /app/publish/xsltworker, XsltWorkerLocator varsayılan yolu), sonra Api.
RUN --mount=type=cache,id=xsltcraft-nuget,target=/root/.nuget/packages \
    --mount=type=cache,id=xsltcraft-m2,target=/root/.m2 \
    --mount=type=cache,id=xsltcraft-ikvm,target=/tmp/ikvm/cache \
    dotnet restore XsltCraft.XsltWorker/XsltCraft.XsltWorker.csproj -r linux-x64 --locked-mode \
 && dotnet restore XsltCraft/XsltCraft.Api.csproj -r linux-x64 --locked-mode -p:BuildXsltWorker=false \
 && dotnet publish XsltCraft.XsltWorker/XsltCraft.XsltWorker.csproj -c Release -r linux-x64 --self-contained false \
      --no-restore -o /app/publish/xsltworker \
 && dotnet publish XsltCraft/XsltCraft.Api.csproj -c Release -r linux-x64 --self-contained false \
      --no-restore -p:BuildXsltWorker=false -o /app/publish \
 && test -x /app/publish/xsltworker/XsltCraft.XsltWorker \
 && test -f /app/publish/xsltworker/Saxon.HE.dll

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
# Worker apphost'u (framework-dependent) çalışma zamanını buradan bulur; Api bu değişkeni worker'ın
# temizlenmiş ortamına aynen geçirir (WorkerProcessStartInfoFactory). Varsayılan konumla aynı — açık tutulur.
ENV DOTNET_ROOT=/usr/share/dotnet

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "XsltCraft.Api.dll"]
