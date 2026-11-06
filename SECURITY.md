# Security Policy

## Reporting a vulnerability

Please do not open a public issue for security problems. Report them privately to the repository owner through GitHub's private vulnerability reporting feature.

## Secrets

- Never commit `.env` files, API keys, logs, or model responses.
- The repository previously tracked a beta `.env` and a server log. Both have been removed from the tracked tree. Treat any key that appeared in git history as compromised: revoke it at the provider first, then purge history with `scripts/purge_secrets.sh`, force-push, and audit provider usage.
- The server logs prompt lengths instead of prompt bodies at INFO level. Set `LOG_LEVEL=DEBUG` only for local debugging.
- Bring-your-own-key settings live in `%APPDATA%\Vulcan\settings.json`. `ServerToken` and `LlmApiKey` are encrypted with Windows DPAPI under the current Windows user, so the file must not be copied between machines or accounts and must never be committed. The key is forwarded to the configured Vulcan server as `X-LLM-API-Key` for a single request and is not logged by the server.
- Set `VULCAN_ALLOW_CUSTOM_LLM_BASE_URL=false` on a shared server to prevent clients from redirecting prompts and keys to an arbitrary provider endpoint.

## Deployment

- Set `VULCAN_API_TOKEN` and require `Authorization: Bearer <token>` on the server.
- Keep `HOST=127.0.0.1` unless a trusted reverse proxy terminates TLS in front of the service.
- Do not enable Flask debug mode in production. The Docker image runs gunicorn with `FLASK_ENV=production`.
- Per-IP rate limiting is in-memory and per worker. Use a shared store such as Redis when running multiple workers or replicas.
