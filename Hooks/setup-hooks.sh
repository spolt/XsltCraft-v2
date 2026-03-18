#!/usr/bin/env bash
# ============================================================
# XsltCraft — Hook kurulum scripti
# Çalıştır: bash Hooks/setup-hooks.sh
# ============================================================

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m'

REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null || {
  echo -e "${RED}HATA: Git reposu bulunamadı.${NC}"
  exit 1
})

HOOKS_SOURCE="$REPO_ROOT/Hooks"
HOOKS_TARGET="$REPO_ROOT/.git/hooks"

echo -e "\n${CYAN}XsltCraft Git Hook Kurulumu${NC}"
echo -e "Kaynak : $HOOKS_SOURCE"
echo -e "Hedef  : $HOOKS_TARGET\n"

HOOKS=("pre-commit" "pre-push")

for hook in "${HOOKS[@]}"; do
  src="$HOOKS_SOURCE/$hook"
  dst="$HOOKS_TARGET/$hook"

  if [[ ! -f "$src" ]]; then
    echo -e "${RED}✗ $hook bulunamadı: $src${NC}"
    continue
  fi

  cp "$src" "$dst"
  chmod +x "$dst"
  echo -e "${GREEN}✓ $hook kuruldu → $dst${NC}"
done

echo -e "\n${GREEN}Kurulum tamamlandı.${NC}"
echo -e "Test etmek için: git commit --allow-empty -m 'test: hook kontrolü'\n"
