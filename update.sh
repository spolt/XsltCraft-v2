#!/bin/bash
set -e

cd /opt/xsltcraft/XsltCraft-v2

echo "==> Config yedekleniyor..."
cp backend/XsltCraft/appsettings.Production.json /tmp/appsettings.Production.json.bak
cp docker-compose.prod.yml /tmp/docker-compose.prod.yml.bak

echo "==> Kod güncelleniyor..."
git pull origin main

echo "==> Config geri yükleniyor..."
cp /tmp/appsettings.Production.json.bak backend/XsltCraft/appsettings.Production.json
cp /tmp/docker-compose.prod.yml.bak docker-compose.prod.yml

echo "==> Build ediliyor..."
docker compose -f docker-compose.prod.yml up -d --build

echo ""
echo "✓ Deploy tamamlandı"
docker ps --format "table {{.Names}}\t{{.Status}}"
