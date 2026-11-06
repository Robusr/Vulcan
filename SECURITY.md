# Security Policy

## Reporting a vulnerability

Please do not open a public issue for security problems. Report them privately to the repository owner through GitHub's private vulnerability reporting feature.

## Secrets

- Never commit `.env` files, API keys, logs, or model responses.
- The repository previously tracked a beta `.env` and a server log. Both have been removed from the tracked tree. Treat any key that appeared in git history as compromised: revoke it at the provider first, then purge history with `scripts/purge_secrets.sh`, force-push, and audit provider usage.
- The server logs prompt lengths instead of prompt bodies at INFO level. Set `LOG_LEVEL=DEBUG` only for local debugging.

## Deployment

- Set `VULCAN_API_TOKEN` and require `Authorization: Bearer <token>` on the server.
- Keep `HOST=127.0.0.1` unless a trusted reverse proxy terminates TLS in front of the service.
- Do not enable Flask debug mode in production. The Docker image runs gunicorn with `FLASK_ENV=production`.
- Per-IP rate limiting is in-memory and per worker. Use a shared store such as Redis when running multiple workers or replicas.
