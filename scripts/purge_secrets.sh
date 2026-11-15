#!/usr/bin/env bash
set -euo pipefail

# WARNING: this rewrites git history. Coordinate with every collaborator first.
# Rotate any exposed credential before running this script.

if ! command -v git-filter-repo >/dev/null 2>&1; then
  echo "git-filter-repo is required: pip install git-filter-repo" >&2
  exit 1
fi

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

# Historical and current paths. filter-repo rewrites all refs, including tags.
git filter-repo --force \
  --path .env \
  --path client/.env \
  --path rebuild/server/.env \
  --path beta/server-python-beta/.env \
  --path archive/beta/server-python-beta/.env \
  --path release/server/vulcan_server.log \
  --path server/vulcan_server.log \
  --path rebuild/server/vulcan_server.log \
  --invert-paths

cat <<'EOF'
History rewritten locally.

Next steps:
1. Re-add the origin remote if git-filter-repo removed it.
2. Force-push every branch and tag:
   git push --force --all
   git push --force --tags
3. Ask collaborators to re-clone. Old clones and forks may still contain the secret.
4. Rotate the exposed key at the provider. History rewriting does not make a leaked key safe.
EOF
